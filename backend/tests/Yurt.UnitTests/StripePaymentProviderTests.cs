using System.Net;
using System.Text;
using System.Text.Json;
using Microsoft.Extensions.Logging.Abstractions;
using NSubstitute;
using Yurt.Application.Common.Interfaces;
using Yurt.Application.Features.Payments.DTOs;
using Yurt.Domain.Entities;
using Yurt.Domain.Enums;
using Yurt.Infrastructure.Payments;

namespace Yurt.UnitTests;

/// <summary>The Stripe adapter: what it sends to Stripe, Stripe's webhook signature scheme, and the events it reads.</summary>
public class StripePaymentProviderTests
{
    private const string Secret = "whsec_unit_test_secret";

    private sealed class StubHandler(Func<string, (HttpStatusCode, string)> answer) : HttpMessageHandler
    {
        public List<(string Path, string Form, HttpRequestMessage Request)> Calls { get; } = [];

        protected override async Task<HttpResponseMessage> SendAsync(HttpRequestMessage request, CancellationToken ct)
        {
            var form = request.Content is null ? "" : await request.Content.ReadAsStringAsync(ct);
            Calls.Add((request.RequestUri!.AbsolutePath, WebUtility.UrlDecode(form), request));
            var (status, json) = answer(request.RequestUri.AbsolutePath);
            return new HttpResponseMessage(status) { Content = new StringContent(json, Encoding.UTF8, "application/json") };
        }
    }

    private static StripePaymentProvider Provider(HttpMessageHandler handler, string? secretKey = "sk_test_123", bool allowLive = false)
    {
        var secrets = Substitute.For<ITenantSecrets>();
        secrets.GetAsync("Stripe:SecretKey", Arg.Any<CancellationToken>()).Returns(secretKey);
        secrets.GetAsync("Stripe:WebhookSecret", Arg.Any<CancellationToken>()).Returns(Secret);
        return new StripePaymentProvider(new HttpClient(handler), new StripeOptions { AllowLiveKeys = allowLive }, secrets,
            NullLogger<StripePaymentProvider>.Instance);
    }

    private static Dictionary<string, string> Signed(string body, DateTimeOffset? at = null, string secret = Secret)
    {
        var ts = (at ?? DateTimeOffset.UtcNow).ToUnixTimeSeconds();
        return new(StringComparer.OrdinalIgnoreCase) { ["Stripe-Signature"] = $"t={ts},v1={StripePaymentProvider.Sign(body, ts, secret)}" };
    }

    private static string Event(string type, object obj)
        => JsonSerializer.Serialize(new { id = "evt_1", type, data = new { @object = obj } });

    [Fact]
    public async Task CreateInvoice_OpensACheckoutSession_InTiyn_WithTheCafeKeyAndAnIdempotencyKey()
    {
        var handler = new StubHandler(_ => (HttpStatusCode.OK,
            """{"id":"cs_test_1","url":"https://checkout.stripe.com/c/pay/cs_test_1","expires_at":1893456000}"""));
        var paymentId = Guid.NewGuid();
        var orderId = Guid.NewGuid();

        var invoice = await Provider(handler).CreateInvoiceAsync(new CreatePaymentInvoiceRequest(
            paymentId, orderId, "ABCD1234", 1250.50m, Currency.KZT, "https://a/ok", "https://a/back", "cafe"));

        Assert.Equal("cs_test_1", invoice.InvoiceId);
        Assert.Equal("https://checkout.stripe.com/c/pay/cs_test_1", invoice.PaymentUrl);
        Assert.Equal(DateTimeOffset.FromUnixTimeSeconds(1893456000).UtcDateTime, invoice.ExpiresAt);
        var (path, form, request) = Assert.Single(handler.Calls);
        Assert.Equal("/v1/checkout/sessions", path);
        Assert.Contains("line_items[0][price_data][unit_amount]=125050", form);
        Assert.Contains("line_items[0][price_data][currency]=kzt", form);
        Assert.Contains("mode=payment", form);
        Assert.Contains($"client_reference_id={orderId}", form);
        Assert.Contains("line_items[0][price_data][product_data][name]=Order ABCD1234", form);
        // No fixed payment method list: card, Apple Pay and Google Pay come from the cafe's Stripe dashboard.
        Assert.DoesNotContain("payment_method_types", form);
        Assert.Equal("sk_test_123", request.Headers.Authorization!.Parameter);
        Assert.Equal($"checkout-{paymentId}", request.Headers.GetValues("Idempotency-Key").Single());
        Assert.Equal("2024-06-20", request.Headers.GetValues("Stripe-Version").Single());
        // Stripe refuses expires_at less than 30 minutes after it receives the request, so exactly 30 is too tight.
        var expiresAt = long.Parse(form.Split('&').Single(p => p.StartsWith("expires_at=")).Split('=')[1]);
        Assert.True(expiresAt - DateTimeOffset.UtcNow.ToUnixTimeSeconds() > 30 * 60);
    }

    [Fact]
    public async Task StripeError_BecomesAProviderException_WithStripesMessage()
    {
        var handler = new StubHandler(_ => (HttpStatusCode.BadRequest, """{"error":{"message":"Amount must be at least 50 cents"}}"""));

        var ex = await Assert.ThrowsAsync<PaymentProviderException>(() => Provider(handler).CreateInvoiceAsync(
            new CreatePaymentInvoiceRequest(Guid.NewGuid(), Guid.NewGuid(), "X", 1m, Currency.KZT, "https://a", "https://b", "c")));
        Assert.Contains("Amount must be at least 50 cents", ex.Message);
    }

    [Fact]
    public async Task LiveKeys_AreIgnored_UntilAllowed()
    {
        var handler = new StubHandler(_ => (HttpStatusCode.OK, "{}"));
        Assert.False(await Provider(handler, "sk_live_abc").IsConfiguredAsync());
        Assert.True(await Provider(handler, "sk_live_abc", allowLive: true).IsConfiguredAsync());
        Assert.True(await Provider(handler, "rk_test_abc").IsConfiguredAsync());
        Assert.False(await Provider(handler, null).IsConfiguredAsync());
    }

    [Fact]
    public async Task Refund_SendsThePaymentIntent_AndReportsAFailedRefund()
    {
        var ok = new StubHandler(_ => (HttpStatusCode.OK, """{"id":"re_1","status":"succeeded"}"""));
        var payment = new Payment { OrderId = Guid.NewGuid(), ProviderPaymentId = "pi_123", InvoiceId = "cs_1", PaymentUrl = "", QrCode = "" };

        Assert.True((await Provider(ok).RefundAsync(payment)).Succeeded);
        var (path, form, request) = Assert.Single(ok.Calls);
        Assert.Equal("/v1/refunds", path);
        Assert.Contains("payment_intent=pi_123", form);
        Assert.Equal($"refund-{payment.Id}", request.Headers.GetValues("Idempotency-Key").Single());

        var failed = new StubHandler(_ => (HttpStatusCode.OK, """{"id":"re_1","status":"failed"}"""));
        Assert.False((await Provider(failed).RefundAsync(payment)).Succeeded);

        payment.ProviderPaymentId = null;
        Assert.Equal(422, (await Provider(ok).RefundAsync(payment)).StatusCode);
    }

    [Fact]
    public void Signature_MatchesStripesScheme()
    {
        // Same steps as Stripe's documentation: HMAC-SHA256 of "timestamp.body" with the whsec_ secret, as lowercase hex.
        var body = """{"id":"evt_1"}""";
        var now = DateTimeOffset.FromUnixTimeSeconds(1700000000);
        var sig = StripePaymentProvider.Sign(body, 1700000000, Secret);

        Assert.Matches("^[0-9a-f]{64}$", sig);
        Assert.Null(StripePaymentProvider.VerifySignature(body, $"t=1700000000,v1={sig}", Secret, now, 300));
        // Stripe can list several v1 signatures while a secret is being rolled.
        Assert.Null(StripePaymentProvider.VerifySignature(body, $"t=1700000000,v1={new string('0', 64)},v1={sig},v0=old", Secret, now, 300));
        Assert.NotNull(StripePaymentProvider.VerifySignature(body + " ", $"t=1700000000,v1={sig}", Secret, now, 300));
        Assert.NotNull(StripePaymentProvider.VerifySignature(body, $"t=1700000000,v1={sig}", Secret, now.AddMinutes(6), 300));
        Assert.NotNull(StripePaymentProvider.VerifySignature(body, "v1=abc", Secret, now, 300));
    }

    [Theory]
    [InlineData("checkout.session.completed", "paid", PaymentWebhookKind.Paid)]
    [InlineData("checkout.session.async_payment_succeeded", "paid", PaymentWebhookKind.Paid)]
    [InlineData("checkout.session.async_payment_failed", "unpaid", PaymentWebhookKind.Failed)]
    [InlineData("checkout.session.expired", "unpaid", PaymentWebhookKind.Expired)]
    public async Task CheckoutEvents_AreRead(string type, string paymentStatus, PaymentWebhookKind kind)
    {
        var body = Event(type, new { id = "cs_test_9", payment_status = paymentStatus, payment_intent = "pi_9" });

        var result = await Provider(new StubHandler(_ => (HttpStatusCode.OK, "{}"))).ParseWebhookAsync(body, Signed(body));

        Assert.True(result.Succeeded);
        Assert.Equal(kind, result.Data!.Kind);
        Assert.Equal("cs_test_9", result.Data.InvoiceId);
    }

    [Fact]
    public async Task OtherEvents_AreValidButIgnored_AndAnUnpaidCompletedSessionWaits()
    {
        var provider = Provider(new StubHandler(_ => (HttpStatusCode.OK, "{}")));
        var unpaid = Event("checkout.session.completed", new { id = "cs_1", payment_status = "unpaid" });
        var other = Event("invoice.paid", new { id = "in_1" });
        var partial = Event("charge.refunded", new { id = "ch_1", refunded = false, payment_intent = "pi_1" });

        foreach (var body in new[] { unpaid, other, partial })
        {
            var result = await provider.ParseWebhookAsync(body, Signed(body));
            Assert.True(result.Succeeded);
            Assert.Null(result.Data);
        }
    }

    [Fact]
    public async Task FullRefund_IsReadByPaymentIntent_EvenWhenExpanded()
    {
        var body = Event("charge.refunded", new { id = "ch_1", refunded = true, payment_intent = new { id = "pi_77" } });

        var result = await Provider(new StubHandler(_ => (HttpStatusCode.OK, "{}"))).ParseWebhookAsync(body, Signed(body));

        Assert.Equal(PaymentWebhookKind.Refunded, result.Data!.Kind);
        Assert.Equal("pi_77", result.Data.ProviderPaymentId);
        Assert.Null(result.Data.InvoiceId);
    }

    [Fact]
    public async Task Webhook_SignedWithAnotherSecret_IsRejected()
    {
        var body = Event("checkout.session.completed", new { id = "cs_1", payment_status = "paid" });

        var result = await Provider(new StubHandler(_ => (HttpStatusCode.OK, "{}")))
            .ParseWebhookAsync(body, Signed(body, secret: "whsec_other"));

        Assert.False(result.Succeeded);
        Assert.Equal(400, result.StatusCode);
    }
}
