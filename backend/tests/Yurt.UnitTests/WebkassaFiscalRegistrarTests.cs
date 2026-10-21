using System.Net;
using System.Text;
using System.Text.Json;
using Microsoft.Extensions.Logging.Abstractions;
using Yurt.Application.Common.Interfaces;
using Yurt.Application.Features.Fiscal;
using Yurt.Domain.Entities;
using Yurt.Domain.Enums;
using Yurt.Infrastructure.Fiscal;

namespace Yurt.UnitTests;

/// <summary>The Webkassa adapter's flow (login, check, Z report, token refresh) and the receipt it builds from an order.</summary>
public class WebkassaFiscalRegistrarTests
{
    private sealed class StubHandler(Func<string, JsonElement, (HttpStatusCode, string)> answer) : HttpMessageHandler
    {
        public List<(string Path, JsonElement Body, string? ApiKey)> Calls { get; } = [];

        protected override async Task<HttpResponseMessage> SendAsync(HttpRequestMessage request, CancellationToken ct)
        {
            var body = JsonDocument.Parse(await request.Content!.ReadAsStringAsync(ct)).RootElement.Clone();
            var path = request.RequestUri!.AbsolutePath;
            Calls.Add((path, body, request.Headers.TryGetValues("X-API-KEY", out var v) ? v.Single() : null));
            var (status, json) = answer(path, body);
            return new HttpResponseMessage(status) { Content = new StringContent(json, Encoding.UTF8, "application/json") };
        }
    }

    private static readonly FiscalCredentials Creds = new("cashier@cafe.kz", "pw", "partner-key");

    private static WebkassaFiscalRegistrar Registrar(HttpMessageHandler handler) => new(
        new HttpClient(handler), new WebkassaOptions { BaseUrl = "https://devkkm.webkassa.kz" }, new WebkassaTokenCache(),
        NullLogger<WebkassaFiscalRegistrar>.Instance);

    private static FiscalReceiptRequest Receipt(FiscalReceiptKind kind = FiscalReceiptKind.Sale, int? vat = null) => new(
        "SWK00001", kind, "abc-s",
        [new FiscalLine("Latte", 2, 1000m, 100m)],
        [new FiscalPayment(PaymentMethod.Cash, 500m), new FiscalPayment(PaymentMethod.KaspiBank, 1000m), new FiscalPayment(PaymentMethod.Card, 400m)],
        vat);

    private const string LoginOk = """{"Data":{"Token":"tok-1"}}""";
    private const string CheckOk = """{"Data":{"CheckNumber":"923811","TicketUrl":"https://ofd.kz/t?i=1","ShiftNumber":7}}""";

    [Fact]
    public async Task Sale_LogsInOnce_SendsTheCheck_AndReturnsTheTicket()
    {
        var handler = new StubHandler((path, _) => path.EndsWith("Authorize") ? (HttpStatusCode.OK, LoginOk) : (HttpStatusCode.OK, CheckOk));
        var registrar = Registrar(handler);

        var first = await registrar.SendReceiptAsync(Creds, Receipt());
        var second = await registrar.SendReceiptAsync(Creds, Receipt());

        Assert.True(first.Succeeded);
        Assert.Equal("923811", first.CheckNumber);
        Assert.Equal("https://ofd.kz/t?i=1", first.TicketUrl);
        Assert.True(second.Succeeded);
        Assert.Equal(["/api/Authorize", "/api/Check", "/api/Check"], handler.Calls.Select(c => c.Path));
        Assert.All(handler.Calls, c => Assert.Equal("partner-key", c.ApiKey));

        var login = handler.Calls[0].Body;
        Assert.Equal("cashier@cafe.kz", login.GetProperty("Login").GetString());
        var check = handler.Calls[1].Body;
        Assert.Equal("tok-1", check.GetProperty("Token").GetString());
        Assert.Equal("SWK00001", check.GetProperty("CashboxUniqueNumber").GetString());
        Assert.Equal(WebkassaFiscalRegistrar.OperationSale, check.GetProperty("OperationType").GetInt32());
        Assert.Equal("abc-s", check.GetProperty("ExternalCheckNumber").GetString());
        var position = check.GetProperty("Positions")[0];
        Assert.Equal(2, position.GetProperty("Count").GetInt32());
        Assert.Equal(100m, position.GetProperty("Discount").GetDecimal());
        Assert.Equal(WebkassaFiscalRegistrar.TaxNone, position.GetProperty("TaxType").GetInt32());
        // Card and "other card-like" methods share one Webkassa payment line.
        var payments = check.GetProperty("Payments").EnumerateArray()
            .ToDictionary(p => p.GetProperty("PaymentType").GetInt32(), p => p.GetProperty("Sum").GetDecimal());
        Assert.Equal(500m, payments[WebkassaFiscalRegistrar.PaymentCash]);
        Assert.Equal(1000m, payments[WebkassaFiscalRegistrar.PaymentMobile]);
        Assert.Equal(400m, payments[WebkassaFiscalRegistrar.PaymentCard]);
    }

    [Fact]
    public async Task ExpiredToken_LogsInAgain_AndRetriesOnce()
    {
        var checks = 0;
        var handler = new StubHandler((path, _) =>
        {
            if (path.EndsWith("Authorize")) return (HttpStatusCode.OK, LoginOk);
            return ++checks == 1
                ? (HttpStatusCode.OK, """{"Errors":[{"Code":2,"Text":"Срок действия сессии истек"}]}""")
                : (HttpStatusCode.OK, CheckOk);
        });

        var result = await Registrar(handler).SendReceiptAsync(Creds, Receipt());

        Assert.True(result.Succeeded);
        Assert.Equal(["/api/Authorize", "/api/Check", "/api/Authorize", "/api/Check"], handler.Calls.Select(c => c.Path));
    }

    [Fact]
    public async Task ProviderError_And_BadLogin_ComeBackAsPlainFailures()
    {
        var handler = new StubHandler((path, _) => path.EndsWith("Authorize")
            ? (HttpStatusCode.OK, LoginOk)
            : (HttpStatusCode.OK, """{"Errors":[{"Code":11,"Text":"Смена превысила 24 часа"}]}"""));
        var error = await Registrar(handler).SendReceiptAsync(Creds, Receipt());
        Assert.False(error.Succeeded);
        Assert.Contains("Смена превысила 24 часа", error.Error);
        Assert.Contains("code 11", error.Error);

        var badLogin = new StubHandler((_, _) => (HttpStatusCode.OK, """{"Errors":[{"Code":1,"Text":"Неверный логин или пароль"}]}"""));
        var login = await Registrar(badLogin).SendReceiptAsync(Creds, Receipt());
        Assert.False(login.Succeeded);
        Assert.Contains("login failed", login.Error);
        Assert.Single(badLogin.Calls);
    }

    [Fact]
    public async Task NetworkFailure_DoesNotThrow()
    {
        var registrar = Registrar(new ThrowingHandler());
        var result = await registrar.SendReceiptAsync(Creds, Receipt());
        Assert.False(result.Succeeded);
        Assert.Contains("did not answer", result.Error);
    }

    private sealed class ThrowingHandler : HttpMessageHandler
    {
        protected override Task<HttpResponseMessage> SendAsync(HttpRequestMessage request, CancellationToken ct)
            => throw new HttpRequestException("no route");
    }

    [Fact]
    public async Task ZReport_ClosesTheRegistersShift()
    {
        var handler = new StubHandler((path, _) => path.EndsWith("Authorize") ? (HttpStatusCode.OK, LoginOk) : (HttpStatusCode.OK, CheckOk));
        var result = await Registrar(handler).CloseShiftAsync(Creds, "SWK00001");
        Assert.True(result.Succeeded);
        Assert.Equal("/api/ZReport", handler.Calls[1].Path);
        Assert.Equal("SWK00001", handler.Calls[1].Body.GetProperty("CashboxUniqueNumber").GetString());
    }

    [Fact]
    public void Return_And_Vat_AreInTheCheck()
    {
        var check = JsonSerializer.SerializeToElement(WebkassaFiscalRegistrar.BuildCheck("t", Receipt(FiscalReceiptKind.Return, vat: 16)));
        Assert.Equal(WebkassaFiscalRegistrar.OperationSaleReturn, check.GetProperty("OperationType").GetInt32());
        var position = check.GetProperty("Positions")[0];
        Assert.Equal(WebkassaFiscalRegistrar.TaxVat, position.GetProperty("TaxType").GetInt32());
        Assert.Equal(16, position.GetProperty("TaxPercent").GetInt32());
        Assert.Equal(262.07m, position.GetProperty("Tax").GetDecimal()); // 1900 * 16 / 116
    }

    [Fact]
    public void BuildRequest_SpreadsDiscountAndPoints_SoTheTotalIsWhatWasPaid()
    {
        var order = new Order { Subtotal = 1000m, DiscountAmount = 100m, Total = 900m, LoyaltyPointsSpent = 1m, PaymentMethod = PaymentMethod.Card };
        var t0 = DateTime.UtcNow;
        foreach (var (n, price) in new[] { (1, 333m), (1, 333m), (1, 334m) })
            order.Items.Add(new OrderItem { MenuItemName = $"Item {price}", Quantity = n, UnitPrice = price, LineTotal = price, CreatedAt = t0 = t0.AddSeconds(1) });

        var r = FiscalReceiptService.BuildRequest(order, null, FiscalReceiptKind.Sale, "SWK1", null, "x");

        Assert.Equal(899m, r.Lines.Sum(l => l.UnitPrice * l.Quantity - l.Discount));
        Assert.Equal(101m, r.Lines.Sum(l => l.Discount));
        Assert.Equal(new FiscalPayment(PaymentMethod.Card, 899m), Assert.Single(r.Payments));

        var split = FiscalReceiptService.BuildRequest(order,
            [new OrderPayment { Method = PaymentMethod.Cash, Amount = 400m }, new OrderPayment { Method = PaymentMethod.KaspiBank, Amount = 499m }],
            FiscalReceiptKind.Sale, "SWK1", null, "x");
        Assert.Equal(2, split.Payments.Count);
    }

    [Theory]
    [InlineData(1, 1)]
    [InlineData(2, 2)]
    [InlineData(4, 8)]
    [InlineData(6, 30)]
    [InlineData(50, 30)]
    public void Pause_Grows_UpToHalfAnHour(int attempts, int minutes)
        => Assert.Equal(TimeSpan.FromMinutes(minutes), FiscalReceiptService.Pause(attempts));
}
