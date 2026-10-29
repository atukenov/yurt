using System.Net;
using System.Text.Json;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging.Abstractions;
using NSubstitute;
using Serilog.Events;
using Serilog.Parsing;
using Yurt.Infrastructure.Monitoring;

namespace Yurt.UnitTests;

public class ErrorAlertTests
{
    private sealed class CaptureHandler : HttpMessageHandler
    {
        public List<(string Url, string Text, string ChatId)> Sent { get; } = [];

        protected override async Task<HttpResponseMessage> SendAsync(HttpRequestMessage request, CancellationToken ct)
        {
            using var doc = JsonDocument.Parse(await request.Content!.ReadAsStringAsync(ct));
            Sent.Add((request.RequestUri!.ToString(), doc.RootElement.GetProperty("text").GetString()!,
                doc.RootElement.GetProperty("chat_id").GetString()!));
            return new HttpResponseMessage(HttpStatusCode.OK);
        }
    }

    private sealed class FixedClock(DateTimeOffset now) : TimeProvider
    {
        public DateTimeOffset Now { get; set; } = now;
        public override DateTimeOffset GetUtcNow() => Now;
    }

    private static readonly DateTimeOffset T0 = new(2026, 10, 28, 12, 0, 0, TimeSpan.Zero);

    private static AlertOptions Options() => new() { TelegramBotToken = "123:abc", TelegramChatId = "-100", ServerName = "production" };

    private static LogEvent Event(string template, LogEventLevel level = LogEventLevel.Error, Exception? ex = null,
        params (string Name, object? Value)[] props)
    {
        var parsed = new MessageTemplateParser().Parse(template);
        return new LogEvent(T0, level, ex, parsed,
            props.Select(p => new LogEventProperty(p.Name, new ScalarValue(p.Value))));
    }

    private static (ErrorAlertService Service, CaptureHandler Handler, FixedClock Clock) Create(AlertOptions? options = null)
    {
        options ??= Options();
        var handler = new CaptureHandler();
        var clock = new FixedClock(T0);
        var env = Substitute.For<IHostEnvironment>();
        env.EnvironmentName.Returns("Production");
        var service = new ErrorAlertService(new ErrorAlertSink(options), options, env,
            NullLogger<ErrorAlertService>.Instance, handler, clock);
        return (service, handler, clock);
    }

    [Fact]
    public async Task Error_is_sent_to_the_configured_chat_with_cafe_and_exception()
    {
        var (service, handler, _) = Create();
        var ex = new InvalidOperationException("Stock went negative");

        await service.HandleAsync(Event("Unhandled exception for {Method} {Path}", ex: ex,
            props: [("Method", "POST"), ("Path", "/api/v1/orders"), ("Tenant", "blue-cafe"), ("CorrelationId", "abc123")]), default);

        var (url, text, chat) = Assert.Single(handler.Sent);
        Assert.Equal("https://api.telegram.org/bot123:abc/sendMessage", url);
        Assert.Equal("-100", chat);
        Assert.Contains("Yurt error [production]", text);
        Assert.Contains("Unhandled exception for \"POST\" \"/api/v1/orders\"", text);
        Assert.Contains("Cafe: blue-cafe", text);
        Assert.Contains("Request id: abc123", text);
        Assert.Contains("InvalidOperationException: Stock went negative", text);
    }

    [Fact]
    public async Task Missing_cafe_is_left_out_instead_of_printing_null()
    {
        var (service, handler, _) = Create();
        await service.HandleAsync(Event("Boom", props: [("Tenant", null)]), default);
        Assert.DoesNotContain("Cafe:", Assert.Single(handler.Sent).Text);
    }

    [Fact]
    public async Task Same_error_repeats_are_counted_not_resent()
    {
        var (service, handler, clock) = Create();

        await service.HandleAsync(Event("Order {Id} failed", props: [("Id", 1)]), default);
        clock.Now = T0.AddMinutes(1);
        await service.HandleAsync(Event("Order {Id} failed", props: [("Id", 2)]), default);
        await service.HandleAsync(Event("Order {Id} failed", props: [("Id", 3)]), default);
        Assert.Single(handler.Sent);

        clock.Now = T0.AddMinutes(11);
        await service.HandleAsync(Event("Order {Id} failed", props: [("Id", 4)]), default);
        Assert.Equal(2, handler.Sent.Count);
        Assert.Contains("Happened 2 more time(s)", handler.Sent[1].Text);
    }

    [Fact]
    public async Task Different_errors_each_get_a_message()
    {
        var (service, handler, _) = Create();
        await service.HandleAsync(Event("Payment failed"), default);
        await service.HandleAsync(Event("Printer offline"), default);
        Assert.Equal(2, handler.Sent.Count);
    }

    [Fact]
    public async Task Hourly_cap_stops_a_flood_and_reports_what_was_held_back()
    {
        var options = Options();
        options.MaxPerHour = 3;
        var (service, handler, clock) = Create(options);

        for (var i = 0; i < 10; i++)
            await service.HandleAsync(Event($"Error number {i}"), default);
        Assert.Equal(3, handler.Sent.Count);

        clock.Now = T0.AddHours(1);
        await service.HandleAsync(Event("After the hour"), default);
        Assert.Equal(4, handler.Sent.Count);
        Assert.Contains("7 other alert(s) were held back", handler.Sent[3].Text);
    }

    [Fact]
    public void Throttle_forgets_old_sends_after_an_hour()
    {
        var throttle = new AlertThrottle(repeatMinutes: 10, maxPerHour: 1);
        Assert.True(throttle.Check("a", T0).Send);
        Assert.False(throttle.Check("b", T0.AddMinutes(30)).Send);
        Assert.True(throttle.Check("b", T0.AddMinutes(61)).Send);
    }

    [Fact]
    public void Sink_takes_only_errors_and_only_when_configured()
    {
        var sink = new ErrorAlertSink(Options());
        sink.Emit(Event("Just info", LogEventLevel.Information));
        sink.Emit(Event("A warning", LogEventLevel.Warning));
        sink.Emit(Event("Broken", LogEventLevel.Error));
        sink.Emit(Event("Dead", LogEventLevel.Fatal));
        Assert.Equal(2, sink.Reader.Count);

        var off = new ErrorAlertSink(new AlertOptions { TelegramBotToken = "123:abc" }); // no chat id
        off.Emit(Event("Broken"));
        Assert.Equal(0, off.Reader.Count);
    }

    [Fact]
    public void Sink_skips_the_request_log_duplicate_and_its_own_failures()
    {
        var sink = new ErrorAlertSink(Options());
        sink.Emit(Event("HTTP {Method} responded 500", props: [("SourceContext", "Serilog.AspNetCore.RequestLoggingMiddleware")]));
        sink.Emit(Event("Alert failed", props: [("SourceContext", typeof(ErrorAlertService).FullName)]));
        Assert.Equal(0, sink.Reader.Count);
    }

    [Fact]
    public void Long_messages_fit_in_one_Telegram_message()
    {
        var (service, _, _) = Create();
        var text = service.Format(Event(new string('x', 6000)), new AlertThrottle.Decision(true, 0, 0));
        Assert.True(text.Length <= 4001);
    }

    [Fact]
    public async Task Nothing_runs_when_alerts_are_not_configured()
    {
        var (service, handler, _) = Create(new AlertOptions());
        await service.StartAsync(default);
        await service.StopAsync(default);
        Assert.Empty(handler.Sent);
    }

    [Fact]
    public async Task Start_message_is_sent_once_configured()
    {
        var (service, handler, _) = Create();
        await service.StartAsync(default);
        for (var i = 0; i < 50 && handler.Sent.Count == 0; i++) await Task.Delay(20);
        await service.StopAsync(default);
        Assert.Contains("Yurt server started [production]", Assert.Single(handler.Sent).Text);
    }
}
