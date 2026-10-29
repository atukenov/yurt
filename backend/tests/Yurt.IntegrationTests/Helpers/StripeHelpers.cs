using System.Collections.Concurrent;
using System.Net;
using System.Text;
using System.Text.Json;
using Yurt.Infrastructure.Payments;

namespace Yurt.IntegrationTests.Helpers;

/// <summary>Answers the few Stripe API calls Yurt makes and remembers them, so tests can check what was sent.</summary>
public sealed class FakeStripe : HttpMessageHandler
{
    public sealed record Call(string Path, Dictionary<string, string> Form, string? ApiKey, string? IdempotencyKey);

    public ConcurrentQueue<Call> Calls { get; } = new();

    /// <summary>Set to make the next refund fail like Stripe would.</summary>
    public bool FailRefunds { get; set; }

    public IEnumerable<Call> CallsFor(string orderId) => Calls.Where(c => c.Form.Values.Contains(orderId));

    protected override async Task<HttpResponseMessage> SendAsync(HttpRequestMessage request, CancellationToken ct)
    {
        var raw = request.Content is null ? "" : await request.Content.ReadAsStringAsync(ct);
        var form = raw.Split('&', StringSplitOptions.RemoveEmptyEntries)
            .Select(p => p.Split('=', 2))
            .ToDictionary(p => WebUtility.UrlDecode(p[0]), p => p.Length > 1 ? WebUtility.UrlDecode(p[1]) : "");
        var path = request.RequestUri!.AbsolutePath;
        Calls.Enqueue(new Call(path, form, request.Headers.Authorization?.Parameter,
            request.Headers.TryGetValues("Idempotency-Key", out var k) ? k.Single() : null));

        object answer;
        if (path == "/v1/checkout/sessions")
        {
            var id = $"cs_test_{Guid.NewGuid():N}";
            answer = new
            {
                id,
                url = $"https://checkout.stripe.com/c/pay/{id}",
                expires_at = long.Parse(form["expires_at"]),
                payment_intent = (string?)null,
            };
        }
        else if (path == "/v1/billing_portal/sessions")
            answer = new { id = $"bps_{Guid.NewGuid():N}", url = $"https://billing.stripe.com/p/session/{Guid.NewGuid():N}" };
        else if (path.EndsWith("/expire"))
            answer = new { id = path.Split('/')[^2], status = "expired" };
        else if (path == "/v1/refunds")
        {
            if (FailRefunds)
                return Json(HttpStatusCode.BadRequest, new { error = new { message = "Charge has already been refunded." } });
            answer = new { id = $"re_{Guid.NewGuid():N}", status = "succeeded" };
        }
        else
            return Json(HttpStatusCode.NotFound, new { error = new { message = "Unknown path" } });

        return Json(HttpStatusCode.OK, answer);
    }

    private static HttpResponseMessage Json(HttpStatusCode status, object body)
        => new(status) { Content = new StringContent(JsonSerializer.Serialize(body), Encoding.UTF8, "application/json") };
}

internal static class StripeWebhooks
{
    internal static string Event(string type, object dataObject)
        => JsonSerializer.Serialize(new
        {
            id = $"evt_{Guid.NewGuid():N}",
            @object = "event",
            type,
            data = new { @object = dataObject },
        });

    internal static string SessionCompleted(string sessionId, string paymentIntent, string paymentStatus = "paid")
        => Event("checkout.session.completed", new { id = sessionId, @object = "checkout.session", payment_status = paymentStatus, payment_intent = paymentIntent });

    internal static string SessionExpired(string sessionId)
        => Event("checkout.session.expired", new { id = sessionId, @object = "checkout.session", payment_status = "unpaid", payment_intent = (string?)null });

    internal static string ChargeRefunded(string paymentIntent)
        => Event("charge.refunded", new { id = $"ch_{Guid.NewGuid():N}", @object = "charge", refunded = true, payment_intent = paymentIntent });

    /// <summary>A webhook to the platform billing address, signed with the platform owner's secret.</summary>
    internal static HttpRequestMessage BillingRequest(string body, string? secret = null)
    {
        var ts = DateTimeOffset.UtcNow.ToUnixTimeSeconds();
        var msg = new HttpRequestMessage(HttpMethod.Post, "/api/v1/billing/stripe/webhook")
        {
            Content = new StringContent(body, Encoding.UTF8, "application/json"),
        };
        msg.Headers.TryAddWithoutValidation("Stripe-Signature",
            $"t={ts},v1={StripePaymentProvider.Sign(body, ts, secret ?? YurtWebAppFactory.BillingWebhookSecret)}");
        return msg;
    }

    /// <summary>A webhook request to the cafe's address, signed the way Stripe signs it.</summary>
    internal static HttpRequestMessage Request(string cafeSlug, string body, string secret, DateTimeOffset? at = null)
    {
        var ts = (at ?? DateTimeOffset.UtcNow).ToUnixTimeSeconds();
        var msg = new HttpRequestMessage(HttpMethod.Post, $"/api/v1/payments/stripe/webhook/{cafeSlug}")
        {
            Content = new StringContent(body, Encoding.UTF8, "application/json"),
        };
        msg.Headers.TryAddWithoutValidation("Stripe-Signature", $"t={ts},v1={StripePaymentProvider.Sign(body, ts, secret)}");
        return msg;
    }
}
