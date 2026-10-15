using System.Security.Cryptography;
using System.Text;
using System.Text.Json;

namespace Yurt.IntegrationTests.Helpers;

internal static class WebhookHelpers
{
    private static readonly JsonSerializerOptions _opts =
        new() { PropertyNamingPolicy = JsonNamingPolicy.CamelCase };

    // Builds a signed webhook payload matching PaymentWebhookValidator's expected format.
    internal static (string Body, string Timestamp, string Signature) BuildPayload(
        string invoiceId,
        string eventType,
        decimal amount,
        string secret,
        string currency = "KZT",
        string provider = "KaspiSandbox",
        DateTimeOffset? at = null)
    {
        var ts = (at ?? DateTimeOffset.UtcNow).ToString("O");
        var payload = new
        {
            invoiceId,
            eventType,
            amount,
            currency,
            provider,
            timestamp = ts
        };
        var body = JsonSerializer.Serialize(payload, _opts);
        var sig  = Sign(ts, body, secret);
        return (body, ts, sig);
    }

    internal static HttpRequestMessage BuildMessage(
        string invoiceId, string eventType, decimal amount, string secret,
        string currency = "KZT", string provider = "KaspiSandbox",
        DateTimeOffset? at = null)
    {
        var (body, ts, sig) = BuildPayload(invoiceId, eventType, amount, secret, currency, provider, at);
        var msg = new HttpRequestMessage(HttpMethod.Post, "/api/v1/payments/webhook")
        {
            Content = new StringContent(body, Encoding.UTF8, "application/json")
        };
        msg.Headers.TryAddWithoutValidation("X-Kaspi-Timestamp", ts);
        msg.Headers.TryAddWithoutValidation("X-Kaspi-Signature", sig);
        return msg;
    }

    private static string Sign(string timestamp, string body, string secret)
    {
        var data = Encoding.UTF8.GetBytes($"{timestamp}.{body}");
        using var hmac = new HMACSHA256(Encoding.UTF8.GetBytes(secret));
        return Convert.ToBase64String(hmac.ComputeHash(data));
    }
}
