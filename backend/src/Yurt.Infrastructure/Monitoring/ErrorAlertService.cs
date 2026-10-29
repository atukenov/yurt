using System.Net.Http.Json;
using System.Text;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
using Serilog.Events;

namespace Yurt.Infrastructure.Monitoring;

/// <summary>
/// Sends server errors to a Telegram chat (see docs/monitoring.md). Off until the bot token and chat id are set.
/// Uses its own HttpClient on purpose: the shared client factory logs request URLs, and Telegram's URL holds the token.
/// </summary>
public sealed class ErrorAlertService(
    ErrorAlertSink sink,
    AlertOptions options,
    IHostEnvironment env,
    ILogger<ErrorAlertService> logger,
    HttpMessageHandler? handler = null,
    TimeProvider? clock = null) : BackgroundService
{
    private const int TelegramMaxLength = 4000;

    private readonly HttpClient _http = new(handler ?? new SocketsHttpHandler(), disposeHandler: handler is null)
    {
        Timeout = TimeSpan.FromSeconds(15),
    };
    private readonly TimeProvider _clock = clock ?? TimeProvider.System;
    private readonly AlertThrottle _throttle = new(options.RepeatMinutes, options.MaxPerHour);

    private string ServerName => string.IsNullOrWhiteSpace(options.ServerName) ? env.EnvironmentName : options.ServerName;

    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        if (!options.Enabled) return;

        if (options.NotifyOnStart)
            await SendAsync($"✅ Yurt server started [{ServerName}]", stoppingToken);

        try
        {
            await foreach (var e in sink.Reader.ReadAllAsync(stoppingToken))
                await HandleAsync(e, stoppingToken);
        }
        catch (OperationCanceledException) when (stoppingToken.IsCancellationRequested) { }
    }

    public async Task HandleAsync(LogEvent e, CancellationToken ct)
    {
        var decision = _throttle.Check(KeyOf(e), _clock.GetUtcNow());
        if (decision.Send) await SendAsync(Format(e, decision), ct);
    }

    /// <summary>Same place, same exception type: the same problem, even if ids in the message differ.</summary>
    public static string KeyOf(LogEvent e) => $"{e.MessageTemplate.Text}|{e.Exception?.GetType().FullName}";

    public string Format(LogEvent e, AlertThrottle.Decision decision)
    {
        var sb = new StringBuilder();
        sb.Append(e.Level == LogEventLevel.Fatal ? "🛑" : "🔴").Append(" Yurt error [").Append(ServerName).AppendLine("]");
        sb.AppendLine(e.RenderMessage());
        foreach (var name in new[] { "Tenant", "CorrelationId" })
            if (e.Properties.TryGetValue(name, out var v) && v is ScalarValue { Value: { } value })
                sb.Append(name == "Tenant" ? "Cafe: " : "Request id: ").AppendLine(value.ToString());
        if (e.Exception is { } ex)
        {
            sb.AppendLine().Append(ex.GetType().Name).Append(": ").AppendLine(ex.Message);
            var frames = (ex.StackTrace ?? "").Split('\n', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries).Take(4);
            foreach (var frame in frames) sb.AppendLine(frame);
            if (ex.InnerException is { } inner)
                sb.Append("Caused by ").Append(inner.GetType().Name).Append(": ").AppendLine(inner.Message);
        }
        sb.AppendLine().Append(e.Timestamp.UtcDateTime.ToString("yyyy-MM-dd HH:mm:ss")).Append(" UTC");
        if (decision.RepeatsSinceLast > 0)
            sb.AppendLine().Append($"Happened {decision.RepeatsSinceLast} more time(s) since the last alert.");
        if (decision.SkippedOverCap > 0)
            sb.AppendLine().Append($"{decision.SkippedOverCap} other alert(s) were held back (limit {options.MaxPerHour} per hour). Check the server logs.");

        var text = sb.ToString();
        return text.Length <= TelegramMaxLength ? text : text[..TelegramMaxLength] + "…";
    }

    private async Task SendAsync(string text, CancellationToken ct)
    {
        try
        {
            var url = $"{options.TelegramApiBaseUrl.TrimEnd('/')}/bot{options.TelegramBotToken}/sendMessage";
            using var res = await _http.PostAsJsonAsync(url, new
            {
                chat_id = options.TelegramChatId,
                text,
                disable_web_page_preview = true,
            }, ct);
            if (!res.IsSuccessStatusCode)
                logger.LogWarning("Telegram alert was not delivered: HTTP {Status}", (int)res.StatusCode);
        }
        catch (OperationCanceledException) when (ct.IsCancellationRequested) { throw; }
        catch (Exception ex)
        {
            // Warning, not Error: an error here would become another alert. No exception object, its text could hold the URL.
            logger.LogWarning("Telegram alert was not delivered: {Reason}", ex.GetType().Name);
        }
    }

    public override void Dispose()
    {
        _http.Dispose();
        base.Dispose();
    }
}
