using System.Globalization;
using System.Net.Http.Headers;
using System.Security.Cryptography;
using System.Text;
using System.Text.Json;
using Microsoft.Extensions.Logging;
using Yurt.Application.Common.Interfaces;
using Yurt.Application.Common.Models;
using Yurt.Application.Features.Payments;
using Yurt.Application.Features.Payments.DTOs;
using Yurt.Domain.Entities;
using Yurt.Domain.Enums;

namespace Yurt.Infrastructure.Payments;

public class StripeOptions
{
    public string ApiBaseUrl { get; set; } = "https://api.stripe.com";

    /// <summary>Stripe API version the requests are written for. Webhook bodies follow the version set on the
    /// webhook in the Stripe dashboard; only fields that are the same in every recent version are read.</summary>
    public string ApiVersion { get; set; } = "2024-06-20";

    /// <summary>Off by default: only test keys (sk_test_...) work, so no real money moves until the platform owner
    /// has a company Stripe accepts and turns this on (Stripe__AllowLiveKeys=true).</summary>
    public bool AllowLiveKeys { get; set; }

    /// <summary>How old a webhook may be (Stripe's own libraries use 5 minutes).</summary>
    public int WebhookToleranceSeconds { get; set; } = 300;

    /// <summary>How long the payment page stays open. Stripe allows 30 minutes to 24 hours.</summary>
    public int CheckoutMinutes { get; set; } = 30;
}

/// <summary>
/// Stripe Checkout: the customer pays on Stripe's own payment page, which offers card, Apple Pay and Google Pay
/// (whichever are switched on in the cafe's Stripe dashboard and work on the customer's device). Uses the cafe's own
/// Stripe account (Stripe:SecretKey) and checks webhooks with the cafe's signing secret (Stripe:WebhookSecret).
/// </summary>
public class StripePaymentProvider : IPaymentProvider
{
    private readonly HttpClient _http;
    private readonly StripeOptions _options;
    private readonly ITenantSecrets _secrets;
    private readonly ILogger<StripePaymentProvider> _logger;

    public PaymentProvider Provider => PaymentProvider.Stripe;

    public StripePaymentProvider(HttpClient http, StripeOptions options, ITenantSecrets secrets, ILogger<StripePaymentProvider> logger)
    {
        _http = http;
        _options = options;
        _secrets = secrets;
        _logger = logger;
    }

    public async Task<bool> IsConfiguredAsync(CancellationToken cancellationToken = default)
        => await SecretKeyAsync(cancellationToken) is not null
            && !string.IsNullOrWhiteSpace(await _secrets.GetAsync(PaymentProviders.StripeWebhookSecret, cancellationToken));

    public async Task<PaymentInvoiceResponse> CreateInvoiceAsync(CreatePaymentInvoiceRequest request, CancellationToken cancellationToken = default)
    {
        var key = await SecretKeyAsync(cancellationToken) ?? throw new PaymentProviderException("Stripe is not set up for this cafe.");
        var expiresAt = DateTimeOffset.UtcNow.AddMinutes(Math.Clamp(_options.CheckoutMinutes, 30, 24 * 60));
        var form = new List<KeyValuePair<string, string>>
        {
            new("mode", "payment"),
            new("line_items[0][quantity]", "1"),
            new("line_items[0][price_data][currency]", request.Currency.ToString().ToLowerInvariant()),
            new("line_items[0][price_data][unit_amount]", ToMinorUnits(request.Amount).ToString(CultureInfo.InvariantCulture)),
            new("line_items[0][price_data][product_data][name]", $"Order {request.OrderLabel}"),
            new("success_url", request.SuccessUrl),
            new("cancel_url", request.CancelUrl),
            new("client_reference_id", request.OrderId.ToString()),
            new("expires_at", expiresAt.ToUnixTimeSeconds().ToString(CultureInfo.InvariantCulture)),
            new("metadata[order_id]", request.OrderId.ToString()),
            new("metadata[payment_id]", request.PaymentId.ToString()),
            new("metadata[cafe]", request.CafeSlug),
            new("payment_intent_data[metadata][order_id]", request.OrderId.ToString()),
            new("payment_intent_data[metadata][cafe]", request.CafeSlug),
        };

        // The payment id makes a retried request return the same page instead of opening a second one.
        using var doc = await PostAsync(key, "/v1/checkout/sessions", form, $"checkout-{request.PaymentId}", cancellationToken);
        var session = doc.RootElement;
        var id = session.GetProperty("id").GetString()!;
        var url = session.GetProperty("url").GetString()!;
        var expires = session.TryGetProperty("expires_at", out var e) && e.ValueKind == JsonValueKind.Number
            ? DateTimeOffset.FromUnixTimeSeconds(e.GetInt64()).UtcDateTime
            : expiresAt.UtcDateTime;

        return new PaymentInvoiceResponse(
            request.PaymentId,
            id,
            url,
            QrCode: string.Empty,
            request.Amount,
            request.Currency,
            Provider,
            PaymentRecordStatus.Pending,
            DateTime.UtcNow,
            expires,
            RawResponse: null);
    }

    public async Task CancelInvoiceAsync(Payment payment, CancellationToken cancellationToken = default)
    {
        var key = await SecretKeyAsync(cancellationToken) ?? throw new PaymentProviderException("Stripe is not set up for this cafe.");
        using var _ = await PostAsync(key, $"/v1/checkout/sessions/{Uri.EscapeDataString(payment.InvoiceId)}/expire", [], null, cancellationToken);
    }

    public async Task<Result> RefundAsync(Payment payment, CancellationToken cancellationToken = default)
    {
        if (string.IsNullOrEmpty(payment.ProviderPaymentId))
            return Result.Failure("Stripe did not tell us the payment id of this order, so it cannot be refunded from Yurt. Refund it in the Stripe dashboard.", 422);
        var key = await SecretKeyAsync(cancellationToken);
        if (key is null) return Result.Failure("Stripe is not set up for this cafe.", 422);

        try
        {
            using var doc = await PostAsync(key, "/v1/refunds",
            [
                new("payment_intent", payment.ProviderPaymentId),
                new("metadata[order_id]", payment.OrderId.ToString()),
            ], $"refund-{payment.Id}", cancellationToken);
            var status = doc.RootElement.TryGetProperty("status", out var s) ? s.GetString() : null;
            return status is "failed" or "canceled"
                ? Result.Failure($"Stripe could not give the money back (refund {status}).", 502)
                : Result.Success();
        }
        catch (PaymentProviderException ex)
        {
            return Result.Failure(ex.Message, 502);
        }
    }

    public async Task<Result<PaymentWebhookEvent?>> ParseWebhookAsync(
        string rawBody, IReadOnlyDictionary<string, string> headers, CancellationToken cancellationToken = default)
    {
        if (!headers.TryGetValue("Stripe-Signature", out var signature) || string.IsNullOrWhiteSpace(signature))
            return Result<PaymentWebhookEvent?>.Failure("Missing Stripe-Signature header.", 400);

        // The secret belongs to the cafe in the webhook address. A cafe without one accepts no webhooks at all.
        var secret = await _secrets.GetAsync(PaymentProviders.StripeWebhookSecret, cancellationToken);
        if (string.IsNullOrWhiteSpace(secret))
            return Result<PaymentWebhookEvent?>.Failure("Stripe webhook secret is not configured for this cafe.", 503);

        var check = VerifySignature(rawBody, signature, secret, DateTimeOffset.UtcNow, _options.WebhookToleranceSeconds);
        if (check is not null)
            return Result<PaymentWebhookEvent?>.Failure(check, 400);

        try
        {
            using var doc = JsonDocument.Parse(rawBody);
            return Result<PaymentWebhookEvent?>.Success(ReadEvent(doc.RootElement));
        }
        catch (Exception ex) when (ex is JsonException or KeyNotFoundException or InvalidOperationException)
        {
            return Result<PaymentWebhookEvent?>.Failure("Invalid webhook payload.", 400);
        }
    }

    /// <summary>Stripe's signature scheme: header "t=timestamp,v1=hex(HMAC-SHA256(secret, timestamp + '.' + body))".
    /// Returns null when valid, otherwise why not.</summary>
    public static string? VerifySignature(string body, string header, string secret, DateTimeOffset now, int toleranceSeconds)
    {
        long? timestamp = null;
        var signatures = new List<string>();
        foreach (var part in header.Split(','))
        {
            var kv = part.Split('=', 2);
            if (kv.Length != 2) continue;
            var name = kv[0].Trim();
            if (name == "t" && long.TryParse(kv[1].Trim(), NumberStyles.None, CultureInfo.InvariantCulture, out var t)) timestamp = t;
            else if (name == "v1") signatures.Add(kv[1].Trim());
        }
        if (timestamp is null || signatures.Count == 0)
            return "Malformed Stripe-Signature header.";

        if (Math.Abs(now.ToUnixTimeSeconds() - timestamp.Value) > toleranceSeconds)
            return "Webhook timestamp is outside the allowed window.";

        var expected = Sign(body, timestamp.Value, secret);
        var expectedBytes = Encoding.ASCII.GetBytes(expected);
        return signatures.Any(s => CryptographicOperations.FixedTimeEquals(Encoding.ASCII.GetBytes(s.ToLowerInvariant()), expectedBytes))
            ? null
            : "Webhook signature validation failed.";
    }

    public static string Sign(string body, long timestamp, string secret)
    {
        using var hmac = new HMACSHA256(Encoding.UTF8.GetBytes(secret));
        var hash = hmac.ComputeHash(Encoding.UTF8.GetBytes($"{timestamp.ToString(CultureInfo.InvariantCulture)}.{body}"));
        return Convert.ToHexString(hash).ToLowerInvariant();
    }

    /// <summary>KZT, USD and EUR all have two decimals at Stripe, so the amount is sent in tiyn / cents.</summary>
    public static long ToMinorUnits(decimal amount) => (long)Math.Round(amount * 100m, MidpointRounding.AwayFromZero);

    private static PaymentWebhookEvent? ReadEvent(JsonElement evt)
    {
        var eventId = evt.GetProperty("id").GetString() ?? string.Empty;
        var type = evt.GetProperty("type").GetString();
        var obj = evt.GetProperty("data").GetProperty("object");
        string? Str(string name) => obj.TryGetProperty(name, out var v) && v.ValueKind == JsonValueKind.String ? v.GetString() : null;
        // payment_intent is an id, or the whole object when the webhook expands it.
        string? PaymentIntent() => obj.TryGetProperty("payment_intent", out var v)
            ? v.ValueKind switch
            {
                JsonValueKind.String => v.GetString(),
                JsonValueKind.Object when v.TryGetProperty("id", out var id) => id.GetString(),
                _ => null,
            }
            : null;

        return type switch
        {
            // Card and wallet payments are paid at once; slower methods report later with async_payment_*.
            "checkout.session.completed" when Str("payment_status") is "paid" or "no_payment_required"
                => new PaymentWebhookEvent(eventId, PaymentWebhookKind.Paid, Str("id"), PaymentIntent()),
            "checkout.session.async_payment_succeeded"
                => new PaymentWebhookEvent(eventId, PaymentWebhookKind.Paid, Str("id"), PaymentIntent()),
            "checkout.session.async_payment_failed"
                => new PaymentWebhookEvent(eventId, PaymentWebhookKind.Failed, Str("id"), PaymentIntent()),
            "checkout.session.expired"
                => new PaymentWebhookEvent(eventId, PaymentWebhookKind.Expired, Str("id"), null),
            // Only a full refund marks the order refunded; a partial one made in the dashboard is left to staff.
            "charge.refunded" when obj.TryGetProperty("refunded", out var full) && full.ValueKind == JsonValueKind.True
                => new PaymentWebhookEvent(eventId, PaymentWebhookKind.Refunded, null, PaymentIntent()),
            _ => null,
        };
    }

    /// <summary>The cafe's secret key, or null when missing (or a live key while live keys are off).</summary>
    private async Task<string?> SecretKeyAsync(CancellationToken ct)
    {
        var key = await _secrets.GetAsync(PaymentProviders.StripeSecretKey, ct);
        if (string.IsNullOrWhiteSpace(key)) return null;
        if (!_options.AllowLiveKeys && !PaymentSettingsService.IsTestKey(key))
        {
            _logger.LogWarning("A live Stripe key is stored for this cafe, but live keys are switched off (Stripe:AllowLiveKeys)");
            return null;
        }
        return key.Trim();
    }

    private async Task<JsonDocument> PostAsync(
        string key, string path, IEnumerable<KeyValuePair<string, string>> form, string? idempotencyKey, CancellationToken ct)
    {
        using var request = new HttpRequestMessage(HttpMethod.Post, _options.ApiBaseUrl.TrimEnd('/') + path)
        {
            Content = new FormUrlEncodedContent(form),
        };
        request.Headers.Authorization = new AuthenticationHeaderValue("Bearer", key);
        request.Headers.Add("Stripe-Version", _options.ApiVersion);
        if (idempotencyKey is not null) request.Headers.Add("Idempotency-Key", idempotencyKey);

        HttpResponseMessage response;
        string body;
        try
        {
            response = await _http.SendAsync(request, ct);
            body = await response.Content.ReadAsStringAsync(ct);
        }
        catch (Exception ex) when (ex is HttpRequestException or TaskCanceledException && !ct.IsCancellationRequested)
        {
            throw new PaymentProviderException($"Stripe could not be reached: {ex.Message}");
        }

        using (response)
        {
            if (!response.IsSuccessStatusCode)
            {
                var message = ErrorMessage(body) ?? response.ReasonPhrase ?? "error";
                throw new PaymentProviderException($"Stripe answered {(int)response.StatusCode}: {message}");
            }
            try { return JsonDocument.Parse(body); }
            catch (JsonException) { throw new PaymentProviderException("Stripe sent an answer Yurt could not read."); }
        }
    }

    private static string? ErrorMessage(string body)
    {
        try
        {
            using var doc = JsonDocument.Parse(body);
            return doc.RootElement.TryGetProperty("error", out var e) && e.TryGetProperty("message", out var m) ? m.GetString() : null;
        }
        catch (JsonException) { return null; }
    }
}
