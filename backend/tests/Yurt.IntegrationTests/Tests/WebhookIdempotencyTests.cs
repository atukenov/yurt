using System.Net;
using System.Net.Http.Json;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Yurt.Application.Common.Interfaces;
using Yurt.IntegrationTests.Helpers;

namespace Yurt.IntegrationTests.Tests;

[Collection("Integration")]
public class WebhookIdempotencyTests(YurtWebAppFactory factory)
{
    private static readonly Guid LocationId = Guid.Parse("11111111-0000-0000-0000-000000000001");

    // Place an order and immediately create a payment invoice; returns (invoiceId, amount).
    private async Task<(string InvoiceId, decimal Amount)> CreatePaymentAsync(HttpClient client, string phone)
    {
        var (token, _) = await ApiHelpers.CreateCustomerAsync(client, phone);
        ApiHelpers.Authorize(client, token);

        var (itemId, _, _) = await ApiHelpers.GetMenuItemAsync(factory.Services, "Espresso");

        var orderResp = await client.PostAsJsonAsync("/api/v1/orders", new
        {
            locationId    = LocationId,
            paymentMethod = "Cash",
            items         = new[] { new { menuItemId = itemId, quantity = 1, toppings = Array.Empty<object>() } }
        });
        orderResp.EnsureSuccessStatusCode();
        var order = await orderResp.Content.ReadFromJsonAsync<ApiHelpers.OrderResult>(ApiHelpers.JsonOpts);

        var payResp = await client.PostAsJsonAsync("/api/v1/payments/create", new
        {
            orderId         = order!.Id,
            provider        = "KaspiSandbox",
            sandboxBehavior = "Default"
        });
        payResp.EnsureSuccessStatusCode();
        var invoice = await payResp.Content.ReadFromJsonAsync<InvoiceResult>(ApiHelpers.JsonOpts);

        return (invoice!.InvoiceId, invoice.Amount);
    }

    [Fact]
    public async Task DuplicateWebhook_CreatesTwoLogEntries_OrderPreparedExactlyOnce()
    {
        var client = factory.CreateClient();
        var (invoiceId, amount) = await CreatePaymentAsync(client, "+77005000001");

        // Build one signed webhook message, then send it twice
        var msg1 = WebhookHelpers.BuildMessage(invoiceId, "paid", amount, YurtWebAppFactory.WebhookSecret);
        var msg2 = WebhookHelpers.BuildMessage(invoiceId, "paid", amount, YurtWebAppFactory.WebhookSecret);

        var resp1 = await client.SendAsync(msg1);
        var resp2 = await client.SendAsync(msg2);

        Assert.Equal(HttpStatusCode.OK, resp1.StatusCode);
        Assert.Equal(HttpStatusCode.OK, resp2.StatusCode);

        // Both calls must have created a log entry and both must be Processed=true
        await using var scope = factory.Services.CreateAsyncScope();
        var db = scope.ServiceProvider.GetRequiredService<IApplicationDbContext>();

        var logs = await db.PaymentWebhookLogs.ToListAsync();
        var relevant = logs.Where(l => l.Payload.Contains(invoiceId)).ToList();
        Assert.Equal(2, relevant.Count);
        Assert.All(relevant, l => Assert.True(l.Processed));

        // The payment record must be Paid
        var payment = await db.Payments.Include(p => p.Order)
            .FirstOrDefaultAsync(p => p.InvoiceId == invoiceId);
        Assert.NotNull(payment);
        Assert.Equal("Paid", payment.Status.ToString());
        Assert.Equal("Paid", payment.Order.PaymentStatus.ToString());

        // Order must be in Preparing status (changed exactly once)
        Assert.Equal("Preparing", payment.Order.Status.ToString());
    }

    [Fact]
    public async Task DuplicateWebhook_SecondCallDoesNotChangeOrderStatus()
    {
        var client = factory.CreateClient();
        var (invoiceId, amount) = await CreatePaymentAsync(client, "+77005000002");

        // First call processes the payment
        var msg1 = WebhookHelpers.BuildMessage(invoiceId, "paid", amount, YurtWebAppFactory.WebhookSecret);
        (await client.SendAsync(msg1)).EnsureSuccessStatusCode();

        // Manually verify order is Preparing
        await using var scope1 = factory.Services.CreateAsyncScope();
        var db1 = scope1.ServiceProvider.GetRequiredService<IApplicationDbContext>();
        var paymentAfterFirst = await db1.Payments.Include(p => p.Order)
            .FirstOrDefaultAsync(p => p.InvoiceId == invoiceId);
        Assert.Equal("Preparing", paymentAfterFirst!.Order.Status.ToString());

        // Second call — idempotent, must NOT re-process
        var msg2 = WebhookHelpers.BuildMessage(invoiceId, "paid", amount, YurtWebAppFactory.WebhookSecret);
        var resp2 = await client.SendAsync(msg2);
        Assert.Equal(HttpStatusCode.OK, resp2.StatusCode);

        await using var scope2 = factory.Services.CreateAsyncScope();
        var db2 = scope2.ServiceProvider.GetRequiredService<IApplicationDbContext>();
        var paymentAfterSecond = await db2.Payments.Include(p => p.Order)
            .FirstOrDefaultAsync(p => p.InvoiceId == invoiceId);
        // Status unchanged
        Assert.Equal("Preparing", paymentAfterSecond!.Order.Status.ToString());
        Assert.Equal("Paid", paymentAfterSecond.Status.ToString());
    }

    [Fact]
    public async Task Webhook_InvalidSignature_Returns400()
    {
        var client = factory.CreateClient();
        var (invoiceId, amount) = await CreatePaymentAsync(client, "+77005000003");

        // Build message with wrong secret — signature won't match
        var msg = WebhookHelpers.BuildMessage(invoiceId, "paid", amount, "wrong-secret-that-is-definitely-wrong-1234567");
        var resp = await client.SendAsync(msg);

        // Validator returns 401 but PaymentService wrapper coerces to 400
        Assert.Equal(HttpStatusCode.BadRequest, resp.StatusCode);
    }

    [Fact]
    public async Task Webhook_ExpiredTimestamp_Returns400()
    {
        var client = factory.CreateClient();
        var (invoiceId, amount) = await CreatePaymentAsync(client, "+77005000004");

        // Build message with a timestamp 10 minutes in the past (outside the 5-min window)
        var expiredAt = DateTimeOffset.UtcNow.AddMinutes(-10);
        var msg = WebhookHelpers.BuildMessage(
            invoiceId, "paid", amount,
            YurtWebAppFactory.WebhookSecret,
            at: expiredAt);

        var resp = await client.SendAsync(msg);
        Assert.Equal(HttpStatusCode.BadRequest, resp.StatusCode);
    }

    // ── Local response shape ────────────────────────────────────────────────

    private record InvoiceResult(
        Guid    PaymentId,
        string  InvoiceId,
        string  PaymentUrl,
        decimal Amount,
        string  Status);
}
