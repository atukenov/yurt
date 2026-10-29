using System.Globalization;
using System.Net.Http.Headers;
using System.Text.Json;
using Microsoft.Extensions.Configuration;
using Yurt.Application.Common.Models;
using Yurt.Application.Features.Billing;
using Yurt.Application.Features.Payments;

namespace Yurt.Infrastructure.Payments;

/// <summary>
/// Monthly cafe billing on the platform owner's own Stripe account: a Checkout page in subscription mode, Stripe's
/// billing portal, and the platform webhook. Shares the API address, version and the "test keys only" switch with
/// <see cref="StripePaymentProvider"/>; the keys are separate (Billing:Stripe:SecretKey, Billing:Stripe:WebhookSecret).
/// </summary>
public class StripeBillingGateway(HttpClient http, StripeOptions options, IConfiguration config) : IBillingGateway
{
    public const string SecretKeySetting = "Billing:Stripe:SecretKey";
    public const string WebhookSecretSetting = "Billing:Stripe:WebhookSecret";

    private string? RawKey => config[SecretKeySetting] is { Length: > 0 } k ? k.Trim() : null;
    private string? WebhookSecret => config[WebhookSecretSetting] is { Length: > 0 } s ? s.Trim() : null;

    /// <summary>The key, or null when missing or a live key while live keys are off.</summary>
    private string? Key => RawKey is { } k && (options.AllowLiveKeys || PaymentSettingsService.IsTestKey(k)) ? k : null;

    public bool IsConfigured => Key is not null && WebhookSecret is not null;

    public bool TestMode => RawKey is null || PaymentSettingsService.IsTestKey(RawKey);

    public async Task<string> CreateCheckoutAsync(BillingCheckoutRequest request, CancellationToken ct = default)
    {
        var key = Key ?? throw new BillingGatewayException("Billing is not set up on this server.");
        var expiresAt = DateTimeOffset.UtcNow.AddMinutes(60);
        var tenantId = request.TenantId.ToString();
        var form = new List<KeyValuePair<string, string>>
        {
            new("mode", "subscription"),
            new("line_items[0][quantity]", "1"),
            new("line_items[0][price_data][currency]", request.Currency),
            new("line_items[0][price_data][unit_amount]", StripePaymentProvider.ToMinorUnits(request.MonthlyPrice).ToString(CultureInfo.InvariantCulture)),
            new("line_items[0][price_data][recurring][interval]", "month"),
            new("line_items[0][price_data][product_data][name]", $"Yurt platform: {request.CafeName}"),
            new("success_url", request.SuccessUrl),
            new("cancel_url", request.CancelUrl),
            new("client_reference_id", tenantId),
            new("expires_at", expiresAt.ToUnixTimeSeconds().ToString(CultureInfo.InvariantCulture)),
            new("metadata[tenant_id]", tenantId),
            new("metadata[cafe]", request.CafeSlug),
            // Copied by Stripe onto every invoice of the subscription, so each paid invoice names its cafe.
            new("subscription_data[metadata][tenant_id]", tenantId),
            new("subscription_data[metadata][cafe]", request.CafeSlug),
        };
        if (request.CustomerId is not null) form.Add(new("customer", request.CustomerId));

        using var doc = await PostAsync(key, "/v1/checkout/sessions", form, ct);
        return doc.RootElement.GetProperty("url").GetString()
            ?? throw new BillingGatewayException("Stripe sent no payment page address.");
    }

    public async Task<string> CreatePortalAsync(string customerId, string returnUrl, CancellationToken ct = default)
    {
        var key = Key ?? throw new BillingGatewayException("Billing is not set up on this server.");
        using var doc = await PostAsync(key, "/v1/billing_portal/sessions",
            [new("customer", customerId), new("return_url", returnUrl)], ct);
        return doc.RootElement.GetProperty("url").GetString()
            ?? throw new BillingGatewayException("Stripe sent no billing page address.");
    }

    public Result<BillingEvent?> ParseWebhook(string rawBody, IReadOnlyDictionary<string, string> headers)
    {
        if (!headers.TryGetValue("Stripe-Signature", out var signature) || string.IsNullOrWhiteSpace(signature))
            return Result<BillingEvent?>.Failure("Missing Stripe-Signature header.", 400);
        if (WebhookSecret is not { } secret)
            return Result<BillingEvent?>.Failure("Billing webhook secret is not configured.", 503);

        var check = StripePaymentProvider.VerifySignature(rawBody, signature, secret, DateTimeOffset.UtcNow, options.WebhookToleranceSeconds);
        if (check is not null) return Result<BillingEvent?>.Failure(check, 400);

        try
        {
            using var doc = JsonDocument.Parse(rawBody);
            return Result<BillingEvent?>.Success(ReadEvent(doc.RootElement));
        }
        catch (Exception ex) when (ex is JsonException or KeyNotFoundException or InvalidOperationException)
        {
            return Result<BillingEvent?>.Failure("Invalid webhook payload.", 400);
        }
    }

    /// <summary>Reads the few fields Yurt needs. Written to work with the older Stripe API versions (invoice.subscription,
    /// subscription.current_period_end) and the 2025 ones (invoice.parent.subscription_details, items' periods).</summary>
    public static BillingEvent? ReadEvent(JsonElement evt)
    {
        var eventId = evt.GetProperty("id").GetString() ?? string.Empty;
        var type = evt.GetProperty("type").GetString();
        var obj = evt.GetProperty("data").GetProperty("object");

        switch (type)
        {
            case "checkout.session.completed" when Str(obj, "mode") == "subscription":
                return new BillingEvent(eventId, BillingEventKind.CheckoutCompleted,
                    TenantOf(Obj(obj, "metadata")) ?? ParseGuid(Str(obj, "client_reference_id")),
                    Id(obj, "subscription"), Id(obj, "customer"));

            case "customer.subscription.created" or "customer.subscription.updated" or "customer.subscription.deleted":
                return new BillingEvent(eventId, BillingEventKind.SubscriptionChanged,
                    TenantOf(Obj(obj, "metadata")), Str(obj, "id"), Id(obj, "customer"),
                    Status: type == "customer.subscription.deleted" ? "canceled" : Str(obj, "status"),
                    CancelAtPeriodEnd: obj.TryGetProperty("cancel_at_period_end", out var c) && c.ValueKind == JsonValueKind.True);

            case "invoice.paid" or "invoice.payment_failed":
            {
                var details = Obj(obj, "subscription_details") ?? Obj(Obj(obj, "parent"), "subscription_details");
                var subscription = Id(obj, "subscription") ?? Id(details, "subscription");
                var lines = Obj(obj, "lines") is { } l && l.TryGetProperty("data", out var d) && d.ValueKind == JsonValueKind.Array
                    ? d.EnumerateArray().ToList() : [];
                var tenant = TenantOf(Obj(obj, "metadata")) ?? TenantOf(Obj(details, "metadata"))
                    ?? lines.Select(line => TenantOf(Obj(line, "metadata"))).FirstOrDefault(t => t is not null);
                if (subscription is null && tenant is null) return null; // a one-off invoice, not a cafe subscription

                // The paid month is the period of the subscription line, not the invoice's own period fields.
                var periodEnd = lines.Select(line => Unix(Obj(line, "period"), "end")).Where(e => e is not null).Max()
                    ?? Unix(obj, "period_end");
                var amount = obj.TryGetProperty("amount_paid", out var a) && a.ValueKind == JsonValueKind.Number ? a.GetInt64() / 100m : (decimal?)null;

                return new BillingEvent(eventId,
                    type == "invoice.paid" ? BillingEventKind.InvoicePaid : BillingEventKind.InvoicePaymentFailed,
                    tenant, subscription, Id(obj, "customer"),
                    PeriodEnd: periodEnd, InvoiceId: Str(obj, "id"), Amount: amount);
            }

            default:
                return null;
        }
    }

    private static JsonElement? Obj(JsonElement? parent, string name)
        => parent is { ValueKind: JsonValueKind.Object } p && p.TryGetProperty(name, out var v) && v.ValueKind == JsonValueKind.Object ? v : null;

    private static string? Str(JsonElement? parent, string name)
        => parent is { ValueKind: JsonValueKind.Object } p && p.TryGetProperty(name, out var v) && v.ValueKind == JsonValueKind.String ? v.GetString() : null;

    /// <summary>An id field, sent as a string or (when expanded) as an object with an id.</summary>
    private static string? Id(JsonElement? parent, string name)
        => Str(parent, name) ?? Str(Obj(parent, name), "id");

    private static Guid? TenantOf(JsonElement? metadata) => ParseGuid(Str(metadata, "tenant_id"));

    private static Guid? ParseGuid(string? value) => Guid.TryParse(value, out var g) ? g : null;

    private static DateTime? Unix(JsonElement? parent, string name)
        => parent is { ValueKind: JsonValueKind.Object } p && p.TryGetProperty(name, out var v) && v.ValueKind == JsonValueKind.Number
            ? DateTimeOffset.FromUnixTimeSeconds(v.GetInt64()).UtcDateTime : null;

    private async Task<JsonDocument> PostAsync(string key, string path, IEnumerable<KeyValuePair<string, string>> form, CancellationToken ct)
    {
        using var request = new HttpRequestMessage(HttpMethod.Post, options.ApiBaseUrl.TrimEnd('/') + path)
        {
            Content = new FormUrlEncodedContent(form),
        };
        request.Headers.Authorization = new AuthenticationHeaderValue("Bearer", key);
        request.Headers.Add("Stripe-Version", options.ApiVersion);

        HttpResponseMessage response;
        string body;
        try
        {
            response = await http.SendAsync(request, ct);
            body = await response.Content.ReadAsStringAsync(ct);
        }
        catch (Exception ex) when (ex is HttpRequestException or TaskCanceledException && !ct.IsCancellationRequested)
        {
            throw new BillingGatewayException($"Stripe could not be reached: {ex.Message}");
        }

        using (response)
        {
            if (!response.IsSuccessStatusCode)
            {
                string? message = null;
                try
                {
                    using var err = JsonDocument.Parse(body);
                    message = Str(Obj(err.RootElement, "error"), "message");
                }
                catch (JsonException) { }
                throw new BillingGatewayException($"Stripe answered {(int)response.StatusCode}: {message ?? response.ReasonPhrase ?? "error"}");
            }
            try { return JsonDocument.Parse(body); }
            catch (JsonException) { throw new BillingGatewayException("Stripe sent an answer Yurt could not read."); }
        }
    }
}
