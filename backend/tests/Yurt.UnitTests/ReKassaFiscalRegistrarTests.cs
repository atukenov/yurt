using System.Net;
using System.Text;
using System.Text.Json;
using Microsoft.Extensions.Logging.Abstractions;
using Yurt.Application.Common.Interfaces;
using Yurt.Domain.Entities;
using Yurt.Domain.Enums;
using Yurt.Infrastructure.Fiscal;

namespace Yurt.UnitTests;

/// <summary>The ReKassa adapter's flow (register login, ticket, shift close, token refresh) and the ticket it builds.</summary>
public class ReKassaFiscalRegistrarTests
{
    private sealed class StubHandler(Func<HttpMethod, string, JsonElement, (HttpStatusCode, string)> answer) : HttpMessageHandler
    {
        public List<(HttpMethod Method, string PathAndQuery, JsonElement Body, string? Bearer, string? RequestId)> Calls { get; } = [];

        protected override async Task<HttpResponseMessage> SendAsync(HttpRequestMessage request, CancellationToken ct)
        {
            var text = request.Content is null ? "" : await request.Content.ReadAsStringAsync(ct);
            var body = string.IsNullOrEmpty(text) ? default : JsonDocument.Parse(text).RootElement.Clone();
            var path = request.RequestUri!.AbsolutePath;
            Calls.Add((request.Method, request.RequestUri.PathAndQuery, body, request.Headers.Authorization?.Parameter,
                request.Headers.TryGetValues("X-Request-ID", out var v) ? v.Single() : null));
            var (status, json) = answer(request.Method, path, body);
            return new HttpResponseMessage(status) { Content = new StringContent(json, Encoding.UTF8, "application/json") };
        }
    }

    private static readonly FiscalCredentials Creds = new("", "app-pw", null, "1234");

    private static ReKassaFiscalRegistrar Registrar(HttpMessageHandler handler, string? serverKey = "yurt-key") => new(
        new HttpClient(handler), new ReKassaOptions { BaseUrl = "https://test.rekassa.example", ApiKey = serverKey },
        new ReKassaSessionCache(), NullLogger<ReKassaFiscalRegistrar>.Instance);

    private static FiscalReceiptRequest Receipt(FiscalReceiptKind kind = FiscalReceiptKind.Sale, int? vat = null) => new(
        "RK0001", kind, "abc-s",
        [new FiscalLine("Latte", 2, 1000m, 100.5m)],
        [new FiscalPayment(PaymentMethod.Cash, 499.5m), new FiscalPayment(PaymentMethod.KaspiBank, 1000m), new FiscalPayment(PaymentMethod.Card, 400m)],
        vat);

    private const string LoginOk = """{"token":"tok-1"}""";
    private const string Registers = """[{"id":"77","serialNumber":"OTHER"},{"id":"42","serialNumber":"RK0001"}]""";
    private const string TicketOk = """{"id":"9","ticketNumber":"923811","qrCode":"https://ofd.kz/t?i=1"}""";

    private static (HttpStatusCode, string) Happy(HttpMethod method, string path, JsonElement _) =>
        path.EndsWith("auth/login") ? (HttpStatusCode.OK, LoginOk)
        : method == HttpMethod.Get ? (HttpStatusCode.OK, Registers)
        : (HttpStatusCode.OK, TicketOk);

    [Fact]
    public async Task Sale_LogsTheRegisterInOnce_SendsTheTicket_AndReturnsTheLink()
    {
        var handler = new StubHandler(Happy);
        var registrar = Registrar(handler);

        var first = await registrar.SendReceiptAsync(Creds, Receipt());
        var second = await registrar.SendReceiptAsync(Creds, Receipt());

        Assert.True(first.Succeeded, first.Error);
        Assert.Equal("923811", first.CheckNumber);
        Assert.Equal("https://ofd.kz/t?i=1", first.TicketUrl);
        Assert.True(second.Succeeded);
        Assert.Equal(["/api/auth/login?apiKey=yurt-key", "/api/crs", "/api/crs/42/tickets", "/api/crs/42/tickets"],
            handler.Calls.Select(c => c.PathAndQuery));

        var login = handler.Calls[0].Body;
        Assert.Equal("RK0001", login.GetProperty("number").GetString());
        Assert.Equal("app-pw", login.GetProperty("password").GetString());
        Assert.Equal("tok-1", handler.Calls[2].Bearer);
        Assert.Equal("abc-s", handler.Calls[2].RequestId);

        var ticket = handler.Calls[2].Body;
        Assert.Equal(ReKassaFiscalRegistrar.OperationSell, ticket.GetProperty("operation").GetString());
        var commodity = ticket.GetProperty("items")[0].GetProperty("commodity");
        Assert.Equal(2000, commodity.GetProperty("quantity").GetInt32());
        Assert.Equal(1000, commodity.GetProperty("price").GetProperty("bills").GetInt64());
        Assert.Equal(1899, commodity.GetProperty("sum").GetProperty("bills").GetInt64());
        Assert.Equal(50, commodity.GetProperty("sum").GetProperty("coins").GetInt32());
        Assert.False(commodity.TryGetProperty("taxes", out _));
        // Card and "other card-like" methods share one ReKassa payment line.
        var payments = ticket.GetProperty("payments").EnumerateArray()
            .ToDictionary(p => p.GetProperty("type").GetString()!, p => p.GetProperty("sum").GetProperty("bills").GetInt64());
        Assert.Equal(499, payments[ReKassaFiscalRegistrar.PaymentCash]);
        Assert.Equal(1000, payments[ReKassaFiscalRegistrar.PaymentMobile]);
        Assert.Equal(400, payments[ReKassaFiscalRegistrar.PaymentCard]);
        Assert.Equal(1899, ticket.GetProperty("amounts").GetProperty("total").GetProperty("bills").GetInt64());
    }

    [Fact]
    public async Task CafesOwnPartnerKey_WinsOverTheServerKey()
    {
        var handler = new StubHandler(Happy);
        await Registrar(handler).SendReceiptAsync(Creds with { ApiKey = "cafe-key" }, Receipt());
        Assert.Equal("/api/auth/login?apiKey=cafe-key", handler.Calls[0].PathAndQuery);
    }

    [Fact]
    public async Task NoPartnerKey_FailsWithoutCallingReKassa()
    {
        var handler = new StubHandler(Happy);
        var result = await Registrar(handler, serverKey: null).SendReceiptAsync(Creds, Receipt());
        Assert.False(result.Succeeded);
        Assert.Contains("partner key", result.Error);
        Assert.Empty(handler.Calls);
    }

    [Fact]
    public async Task ExpiredToken_LogsInAgain_AndRetriesOnce()
    {
        var tickets = 0;
        var handler = new StubHandler((method, path, body) =>
            path.EndsWith("/tickets") && ++tickets == 1 ? (HttpStatusCode.Unauthorized, "") : Happy(method, path, body));

        var result = await Registrar(handler).SendReceiptAsync(Creds, Receipt());

        Assert.True(result.Succeeded, result.Error);
        Assert.Equal(["/api/auth/login", "/api/crs", "/api/crs/42/tickets", "/api/auth/login", "/api/crs", "/api/crs/42/tickets"],
            handler.Calls.Select(c => c.PathAndQuery.Split('?')[0]));
    }

    [Fact]
    public async Task ProviderError_BadLogin_AndUnknownRegister_ComeBackAsPlainFailures()
    {
        var error = new StubHandler((method, path, body) => path.EndsWith("/tickets")
            ? (HttpStatusCode.BadRequest, """{"code":"SHIFT_EXPIRED","message":"Смена превысила 24 часа"}""")
            : Happy(method, path, body));
        var result = await Registrar(error).SendReceiptAsync(Creds, Receipt());
        Assert.False(result.Succeeded);
        Assert.Contains("Смена превысила 24 часа", result.Error);
        Assert.Contains("400", result.Error);

        var badLogin = new StubHandler((_, _, _) => (HttpStatusCode.Unauthorized, """{"message":"Неверный пароль"}"""));
        var login = await Registrar(badLogin).SendReceiptAsync(Creds, Receipt());
        Assert.False(login.Succeeded);
        Assert.Contains("login failed for register RK0001", login.Error);
        Assert.Contains("Неверный пароль", login.Error);
        Assert.Single(badLogin.Calls);

        var unknown = new StubHandler((method, path, body) => method == HttpMethod.Get
            ? (HttpStatusCode.OK, """[{"id":"1","serialNumber":"A"},{"id":"2","serialNumber":"B"}]""")
            : Happy(method, path, body));
        var missing = await Registrar(unknown).SendReceiptAsync(Creds, Receipt());
        Assert.False(missing.Succeeded);
        Assert.Contains("no register with serial number RK0001", missing.Error);
    }

    [Fact]
    public async Task NetworkFailure_DoesNotThrow()
    {
        var result = await Registrar(new ThrowingHandler()).SendReceiptAsync(Creds, Receipt());
        Assert.False(result.Succeeded);
        Assert.Contains("did not answer", result.Error);
    }

    private sealed class ThrowingHandler : HttpMessageHandler
    {
        protected override Task<HttpResponseMessage> SendAsync(HttpRequestMessage request, CancellationToken ct)
            => throw new HttpRequestException("no route");
    }

    [Fact]
    public async Task CloseShift_SendsThePin_AndNeedsOne()
    {
        var handler = new StubHandler((method, path, body) => path.EndsWith("/close")
            ? (HttpStatusCode.OK, """{"shiftNumber":7}""")
            : Happy(method, path, body));
        var result = await Registrar(handler).CloseShiftAsync(Creds, "RK0001");
        Assert.True(result.Succeeded, result.Error);
        Assert.Equal("7", result.CheckNumber);
        Assert.Equal("/api/crs/42/shifts/close", handler.Calls[2].PathAndQuery);
        Assert.Equal("1234", handler.Calls[2].Body.GetProperty("pincode").GetString());

        var noPin = new StubHandler(Happy);
        var failed = await Registrar(noPin).CloseShiftAsync(Creds with { Pin = null }, "RK0001");
        Assert.False(failed.Succeeded);
        Assert.Contains("Fiscal:Pin", failed.Error);
        Assert.Empty(noPin.Calls);
    }

    [Fact]
    public void Return_Vat_And_LocalTime_AreInTheTicket()
    {
        var ticket = JsonSerializer.SerializeToElement(ReKassaFiscalRegistrar.BuildTicket(
            Receipt(FiscalReceiptKind.Return, vat: 16), new DateTime(2026, 10, 28, 20, 30, 5, DateTimeKind.Utc)));
        Assert.Equal(ReKassaFiscalRegistrar.OperationSellReturn, ticket.GetProperty("operation").GetString());
        var tax = ticket.GetProperty("items")[0].GetProperty("commodity").GetProperty("taxes")[0];
        Assert.Equal(16000, tax.GetProperty("percent").GetInt32());
        Assert.Equal(262, tax.GetProperty("sum").GetProperty("bills").GetInt64()); // 1899.50 * 16 / 116 = 262.00
        var date = ticket.GetProperty("dateTime").GetProperty("date");
        Assert.Equal(29, date.GetProperty("day").GetInt32()); // 20:30 UTC is 01:30 next day in Kazakhstan
        Assert.Equal(1, ticket.GetProperty("dateTime").GetProperty("time").GetProperty("hour").GetInt32());
    }

    [Fact]
    public void Money_SplitsTengeAndTiyn()
    {
        var m = JsonSerializer.SerializeToElement(ReKassaFiscalRegistrar.Money(1234.567m));
        Assert.Equal(1234, m.GetProperty("bills").GetInt64());
        Assert.Equal(57, m.GetProperty("coins").GetInt32());
    }
}
