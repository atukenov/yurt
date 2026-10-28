using System.Net;
using System.Net.Http.Json;
using System.Text.Json;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Yurt.Application.Common.Interfaces;
using Yurt.Domain.Entities;
using Yurt.Domain.Enums;
using Yurt.IntegrationTests.Helpers;

namespace Yurt.IntegrationTests.Tests;

/// <summary>Online payment of an order through Stripe Checkout (test mode, with Stripe played by <see cref="FakeStripe"/>):
/// opening the payment page, webhooks, refunds, and each cafe using only its own Stripe account.</summary>
[Collection("Integration")]
public class StripePaymentTests(YurtWebAppFactory factory)
{
    private static readonly Guid LocationId = Guid.Parse("11111111-0000-0000-0000-000000000001");
    private const string DefaultSlug = Tenant.DefaultSlug;

    private async Task<HttpClient> OperatorAsync()
    {
        var token = await ApiHelpers.CreateAdminTokenAsync(factory.Services, factory.CreateClient(),
            username: "platform-owner", password: "PlatformOwner@123", role: AdminRole.SuperAdmin);
        var client = factory.CreateClient();
        ApiHelpers.Authorize(client, token);
        return client;
    }

    private async Task<HttpClient> StaffAsync()
    {
        var client = factory.CreateClient();
        ApiHelpers.Authorize(client, await ApiHelpers.CreateAdminTokenAsync(factory.Services, client));
        return client;
    }

    /// <summary>The first cafe takes payments through Stripe with the server-wide test keys.</summary>
    private async Task TurnOnStripeForDefaultCafeAsync()
    {
        var op = await OperatorAsync();
        (await op.PutAsJsonAsync("/api/v1/platform/payments", new { provider = "stripe" })).EnsureSuccessStatusCode();
    }

    private async Task<(HttpClient Customer, Guid OrderId, decimal Total)> PlaceOrderAsync()
    {
        var phone = "+7708" + Random.Shared.Next(1_000_000, 9_999_999);
        var client = factory.CreateClient();
        var (token, _) = await ApiHelpers.CreateCustomerAsync(client, phone);
        ApiHelpers.Authorize(client, token);
        var (itemId, _, _) = await ApiHelpers.GetMenuItemAsync(factory.Services, "Espresso");
        var res = await client.PostAsJsonAsync("/api/v1/orders", new
        {
            locationId = LocationId,
            paymentMethod = "Card",
            items = new[] { new { menuItemId = itemId, quantity = 2, toppings = Array.Empty<object>() } },
        });
        res.EnsureSuccessStatusCode();
        var order = (await res.Content.ReadFromJsonAsync<ApiHelpers.OrderResult>(ApiHelpers.JsonOpts))!;
        return (client, order.Id, order.Total);
    }

    private static async Task<ApiHelpers.PaymentResult> PayAsync(HttpClient customer, Guid orderId, string? returnUrl = null)
    {
        var res = await customer.PostAsJsonAsync("/api/v1/payments/create", new { orderId, returnUrl });
        Assert.True(res.IsSuccessStatusCode, await res.Content.ReadAsStringAsync());
        return (await res.Content.ReadFromJsonAsync<ApiHelpers.PaymentResult>(ApiHelpers.JsonOpts))!;
    }

    private async Task<HttpStatusCode> SendWebhookAsync(string body, string slug = DefaultSlug, string? secret = null, DateTimeOffset? at = null)
        => (await factory.CreateClient().SendAsync(StripeWebhooks.Request(slug, body, secret ?? YurtWebAppFactory.StripeWebhookSecret, at))).StatusCode;

    private async Task<Payment> PaymentAsync(string invoiceId)
    {
        await using var scope = factory.Services.CreateAsyncScope();
        var db = scope.ServiceProvider.GetRequiredService<IApplicationDbContext>();
        return await db.Payments.AsNoTracking().Include(p => p.Order).SingleAsync(p => p.InvoiceId == invoiceId);
    }

    [Fact]
    public async Task PayingAnOrder_OpensTheCafesStripeCheckout_AndTheWebhookMarksItPaid()
    {
        await TurnOnStripeForDefaultCafeAsync();
        var (customer, orderId, total) = await PlaceOrderAsync();

        var config = await customer.GetFromJsonAsync<JsonElement>("/api/v1/payments/config");
        Assert.Equal("stripe", config.GetProperty("provider").GetString());

        var invoice = await PayAsync(customer, orderId, "https://www.altyncup.kz/cart");
        Assert.StartsWith("cs_test_", invoice.InvoiceId);
        Assert.Equal($"https://checkout.stripe.com/c/pay/{invoice.InvoiceId}", invoice.PaymentUrl);

        var call = factory.Stripe.CallsFor(orderId.ToString()).Single(c => c.Path == "/v1/checkout/sessions");
        Assert.Equal(YurtWebAppFactory.StripeSecretKey, call.ApiKey);
        Assert.Equal("kzt", call.Form["line_items[0][price_data][currency]"]);
        Assert.Equal(((long)(total * 100)).ToString(), call.Form["line_items[0][price_data][unit_amount]"]);
        // The app's own address (one of the allowed ones) is where Stripe sends the customer back.
        Assert.Equal($"https://www.altyncup.kz/orders/{orderId}?payment=success", call.Form["success_url"]);
        Assert.Equal($"https://www.altyncup.kz/orders/{orderId}?payment=cancelled", call.Form["cancel_url"]);
        Assert.Equal(DefaultSlug, call.Form["metadata[cafe]"]);
        Assert.NotNull(call.IdempotencyKey);

        // Opening it again while it is valid gives the same page, not a second one.
        var again = await PayAsync(customer, orderId);
        Assert.Equal(invoice.InvoiceId, again.InvoiceId);
        Assert.Single(factory.Stripe.CallsFor(orderId.ToString()), c => c.Path == "/v1/checkout/sessions");

        var completed = StripeWebhooks.SessionCompleted(invoice.InvoiceId, "pi_test_paid_1");
        Assert.Equal(HttpStatusCode.OK, await SendWebhookAsync(completed));
        // Stripe resends events; the second one changes nothing.
        Assert.Equal(HttpStatusCode.OK, await SendWebhookAsync(completed));

        var payment = await PaymentAsync(invoice.InvoiceId);
        Assert.Equal(PaymentRecordStatus.Paid, payment.Status);
        Assert.Equal("pi_test_paid_1", payment.ProviderPaymentId);
        Assert.Equal(PaymentStatus.Paid, payment.Order.PaymentStatus);
        // A paid order still waits for staff to accept it, like any other order.
        Assert.Equal(OrderStatus.Created, payment.Order.Status);

        var status = await customer.GetFromJsonAsync<JsonElement>($"/api/v1/payments/status/{invoice.InvoiceId}");
        Assert.Equal("Paid", status.GetProperty("status").GetString());

        // A paid order cannot be paid twice.
        Assert.Equal(HttpStatusCode.Conflict,
            (await customer.PostAsJsonAsync("/api/v1/payments/create", new { orderId })).StatusCode);
    }

    [Fact]
    public async Task UnknownReturnAddress_FallsBackToTheConfiguredAppAddress()
    {
        await TurnOnStripeForDefaultCafeAsync();
        var (customer, orderId, _) = await PlaceOrderAsync();

        await PayAsync(customer, orderId, "https://evil.example/steal");

        var call = factory.Stripe.CallsFor(orderId.ToString()).Single(c => c.Path == "/v1/checkout/sessions");
        Assert.Equal($"https://app.test.invalid/orders/{orderId}?payment=success", call.Form["success_url"]);
    }

    [Fact]
    public async Task AnotherCustomer_CannotPayOrReadSomeoneElsesPayment()
    {
        await TurnOnStripeForDefaultCafeAsync();
        var (customer, orderId, _) = await PlaceOrderAsync();
        var invoice = await PayAsync(customer, orderId);

        var (other, _, _) = await PlaceOrderAsync();
        Assert.Equal(HttpStatusCode.Forbidden, (await other.PostAsJsonAsync("/api/v1/payments/create", new { orderId })).StatusCode);
        Assert.Equal(HttpStatusCode.NotFound, (await other.GetAsync($"/api/v1/payments/status/{invoice.InvoiceId}")).StatusCode);
    }

    [Fact]
    public async Task ExpiredCheckout_MarksThePaymentExpired_AndANewPageCanBeOpened()
    {
        await TurnOnStripeForDefaultCafeAsync();
        var (customer, orderId, _) = await PlaceOrderAsync();
        var invoice = await PayAsync(customer, orderId);

        Assert.Equal(HttpStatusCode.OK, await SendWebhookAsync(StripeWebhooks.SessionExpired(invoice.InvoiceId)));
        Assert.Equal(PaymentRecordStatus.Expired, (await PaymentAsync(invoice.InvoiceId)).Status);

        var second = await PayAsync(customer, orderId);
        Assert.NotEqual(invoice.InvoiceId, second.InvoiceId);
    }

    [Fact]
    public async Task Webhook_WithAWrongSignatureOrOldTimestamp_IsRejected()
    {
        await TurnOnStripeForDefaultCafeAsync();
        var (customer, orderId, _) = await PlaceOrderAsync();
        var invoice = await PayAsync(customer, orderId);
        var body = StripeWebhooks.SessionCompleted(invoice.InvoiceId, "pi_test_forged");

        Assert.Equal(HttpStatusCode.BadRequest, await SendWebhookAsync(body, secret: "whsec_someone_else"));
        Assert.Equal(HttpStatusCode.BadRequest, await SendWebhookAsync(body, at: DateTimeOffset.UtcNow.AddMinutes(-10)));
        var unsigned = new HttpRequestMessage(HttpMethod.Post, $"/api/v1/payments/stripe/webhook/{DefaultSlug}")
        {
            Content = new StringContent(body),
        };
        Assert.Equal(HttpStatusCode.BadRequest, (await factory.CreateClient().SendAsync(unsigned)).StatusCode);

        Assert.Equal(PaymentRecordStatus.Pending, (await PaymentAsync(invoice.InvoiceId)).Status);
    }

    [Fact]
    public async Task DecliningAPaidOrder_GivesTheMoneyBack()
    {
        await TurnOnStripeForDefaultCafeAsync();
        var (customer, orderId, _) = await PlaceOrderAsync();
        var invoice = await PayAsync(customer, orderId);
        await SendWebhookAsync(StripeWebhooks.SessionCompleted(invoice.InvoiceId, "pi_test_declined"));

        var staff = await StaffAsync();
        (await staff.PostAsJsonAsync($"/api/v1/admin/orders/{orderId}/decline", new { reason = "Out of milk" })).EnsureSuccessStatusCode();

        var refund = Assert.Single(factory.Stripe.CallsFor(orderId.ToString()), c => c.Path == "/v1/refunds");
        Assert.Equal("pi_test_declined", refund.Form["payment_intent"]);
        var payment = await PaymentAsync(invoice.InvoiceId);
        Assert.Equal(PaymentRecordStatus.Refunded, payment.Status);
        Assert.Equal(PaymentStatus.Refunded, payment.Order.PaymentStatus);

        // Stripe then reports the refund; nothing changes a second time.
        Assert.Equal(HttpStatusCode.OK, await SendWebhookAsync(StripeWebhooks.ChargeRefunded("pi_test_declined")));
        Assert.Single(factory.Stripe.CallsFor(orderId.ToString()), c => c.Path == "/v1/refunds");
    }

    [Fact]
    public async Task CancellingBeforePaying_ClosesTheCheckoutPage()
    {
        await TurnOnStripeForDefaultCafeAsync();
        var (customer, orderId, _) = await PlaceOrderAsync();
        var invoice = await PayAsync(customer, orderId);

        (await customer.PostAsync($"/api/v1/orders/{orderId}/cancel", null)).EnsureSuccessStatusCode();

        Assert.Contains(factory.Stripe.Calls, c => c.Path == $"/v1/checkout/sessions/{invoice.InvoiceId}/expire");
        Assert.Equal(PaymentRecordStatus.Expired, (await PaymentAsync(invoice.InvoiceId)).Status);
    }

    [Fact]
    public async Task PayingAfterTheOrderWasDeclined_IsRefundedAtOnce()
    {
        await TurnOnStripeForDefaultCafeAsync();
        var (customer, orderId, _) = await PlaceOrderAsync();
        var invoice = await PayAsync(customer, orderId);

        // The page could not be closed in time (here: the customer paid between the decline and the webhook).
        await using (var scope = factory.Services.CreateAsyncScope())
        {
            var db = scope.ServiceProvider.GetRequiredService<IApplicationDbContext>();
            var order = await db.Orders.SingleAsync(o => o.Id == orderId);
            order.Status = OrderStatus.Declined;
            await db.SaveChangesAsync();
        }

        Assert.Equal(HttpStatusCode.OK, await SendWebhookAsync(StripeWebhooks.SessionCompleted(invoice.InvoiceId, "pi_test_late")));

        Assert.Single(factory.Stripe.CallsFor(orderId.ToString()), c => c.Path == "/v1/refunds");
        Assert.Equal(PaymentRecordStatus.Refunded, (await PaymentAsync(invoice.InvoiceId)).Status);
    }

    [Fact]
    public async Task StaffRefund_GivesTheMoneyBack_AndOnlyForOrdersPaidOnline()
    {
        await TurnOnStripeForDefaultCafeAsync();
        var (customer, orderId, _) = await PlaceOrderAsync();
        var staff = await StaffAsync();

        // Nothing paid online yet.
        Assert.Equal(HttpStatusCode.UnprocessableEntity, (await staff.PostAsync($"/api/v1/admin/orders/{orderId}/refund", null)).StatusCode);

        var invoice = await PayAsync(customer, orderId);
        await SendWebhookAsync(StripeWebhooks.SessionCompleted(invoice.InvoiceId, "pi_test_staff"));
        (await staff.PostAsJsonAsync($"/api/v1/admin/orders/{orderId}/accept", new { etaMinutes = 5 })).EnsureSuccessStatusCode();

        var res = await staff.PostAsync($"/api/v1/admin/orders/{orderId}/refund", null);
        Assert.Equal(HttpStatusCode.OK, res.StatusCode);
        Assert.Equal("Refunded", (await res.Content.ReadFromJsonAsync<JsonElement>()).GetProperty("paymentStatus").GetString());
        Assert.Equal(PaymentRecordStatus.Refunded, (await PaymentAsync(invoice.InvoiceId)).Status);

        // Already given back.
        Assert.Equal(HttpStatusCode.UnprocessableEntity, (await staff.PostAsync($"/api/v1/admin/orders/{orderId}/refund", null)).StatusCode);
    }

    [Fact]
    public async Task RefundFromTheStripeDashboard_IsMirroredOnTheOrder()
    {
        await TurnOnStripeForDefaultCafeAsync();
        var (customer, orderId, _) = await PlaceOrderAsync();
        var invoice = await PayAsync(customer, orderId);
        await SendWebhookAsync(StripeWebhooks.SessionCompleted(invoice.InvoiceId, "pi_test_dashboard"));

        Assert.Equal(HttpStatusCode.OK, await SendWebhookAsync(StripeWebhooks.ChargeRefunded("pi_test_dashboard")));

        var payment = await PaymentAsync(invoice.InvoiceId);
        Assert.Equal(PaymentRecordStatus.Refunded, payment.Status);
        Assert.Equal(PaymentStatus.Refunded, payment.Order.PaymentStatus);
    }

    [Fact]
    public async Task UnknownEvents_AreAcceptedAndIgnored()
    {
        await TurnOnStripeForDefaultCafeAsync();
        Assert.Equal(HttpStatusCode.OK, await SendWebhookAsync(StripeWebhooks.Event("customer.created", new { id = "cus_1" })));
        Assert.Equal(HttpStatusCode.OK, await SendWebhookAsync(StripeWebhooks.SessionCompleted("cs_test_not_ours", "pi_x")));
    }

    // ── Each cafe uses its own Stripe account ───────────────────────────────

    private async Task<(Guid Id, string Slug)> NewCafeAsync(HttpClient op, string? secretKey, string? webhookSecret)
    {
        var id = Guid.NewGuid().ToString("N")[..8];
        var res = await op.PostAsJsonAsync("/api/v1/platform/cafes",
            new { slug = $"stripe-{id}", name = $"Stripe {id}", domain = (string?)null, adminUsername = $"owner-{id}", adminPassword = "FirstLogin@123" });
        var cafeId = (await res.Content.ReadFromJsonAsync<JsonElement>()).GetProperty("id").GetGuid();
        if (secretKey is not null)
            await op.PutAsJsonAsync($"/api/v1/platform/cafes/{cafeId}/secrets/Stripe:SecretKey", new { value = secretKey });
        if (webhookSecret is not null)
            await op.PutAsJsonAsync($"/api/v1/platform/cafes/{cafeId}/secrets/Stripe:WebhookSecret", new { value = webhookSecret });
        return (cafeId, $"stripe-{id}");
    }

    [Fact]
    public async Task CafeWebhook_IsCheckedWithThatCafesSecretOnly()
    {
        var op = await OperatorAsync();
        var (_, slug) = await NewCafeAsync(op, "sk_test_cafe_own", "whsec_cafe_own_secret");
        var body = StripeWebhooks.SessionCompleted("cs_test_unknown", "pi_unknown");

        Assert.Equal(HttpStatusCode.OK, await SendWebhookAsync(body, slug, "whsec_cafe_own_secret"));
        Assert.Equal(HttpStatusCode.BadRequest, await SendWebhookAsync(body, slug, YurtWebAppFactory.StripeWebhookSecret));
    }

    [Fact]
    public async Task CafeWithoutAWebhookSecret_AcceptsNoWebhook_AndDoesNotUseTheServerWideOne()
    {
        var op = await OperatorAsync();
        var (_, slug) = await NewCafeAsync(op, null, null);

        var status = await SendWebhookAsync(StripeWebhooks.SessionCompleted("cs_test_x", "pi_x"), slug, YurtWebAppFactory.StripeWebhookSecret);
        Assert.Equal(HttpStatusCode.ServiceUnavailable, status);
        Assert.Equal(HttpStatusCode.NotFound,
            await SendWebhookAsync(StripeWebhooks.SessionCompleted("cs_test_x", "pi_x"), "no-such-cafe"));
    }

    [Fact]
    public async Task PlatformScreen_ShowsTheWebhookAddressAndKeys_AndRefusesLiveKeys()
    {
        var op = await OperatorAsync();
        var (cafeId, slug) = await NewCafeAsync(op, "sk_live_real_money", "whsec_live");
        op.DefaultRequestHeaders.Add("X-Tenant", slug);

        var bad = await op.PutAsJsonAsync("/api/v1/platform/payments", new { provider = "kaspi" });
        Assert.Equal(HttpStatusCode.BadRequest, bad.StatusCode);

        var saved = await op.PutAsJsonAsync("/api/v1/platform/payments", new { provider = "stripe" });
        saved.EnsureSuccessStatusCode();
        var settings = await saved.Content.ReadFromJsonAsync<JsonElement>();
        Assert.Equal("stripe", settings.GetProperty("provider").GetString());
        Assert.True(settings.GetProperty("secretKeySet").GetBoolean());
        Assert.True(settings.GetProperty("webhookSecretSet").GetBoolean());
        Assert.False(settings.GetProperty("testMode").GetBoolean());
        Assert.EndsWith($"/api/v1/payments/stripe/webhook/{slug}", settings.GetProperty("webhookUrl").GetString());

        // Live keys are off on this server, so the cafe's customers are not offered online payment.
        await using var scope = factory.Services.CreateAsyncScope();
        scope.ServiceProvider.GetRequiredService<ITenantContext>().Set(cafeId, slug);
        var provider = scope.ServiceProvider.GetRequiredService<IPaymentProviderFactory>().GetProvider(PaymentProvider.Stripe);
        Assert.False(await provider.IsConfiguredAsync());
    }
}
