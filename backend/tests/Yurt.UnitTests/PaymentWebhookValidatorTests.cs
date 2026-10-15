using System.Security.Cryptography;
using System.Text;
using System.Text.Json;
using Microsoft.Extensions.Options;
using Yurt.Infrastructure.Payments;

namespace Yurt.UnitTests;

/// <summary>
/// Tests for <see cref="PaymentWebhookValidator"/> — the HMAC signature/timestamp
/// check that guards every inbound payment webhook. No coverage existed for this
/// before, despite it being the main defense once a real payment provider (not
/// just the self-signing sandbox) starts sending webhooks.
/// </summary>
public class PaymentWebhookValidatorTests
{
    private const string Secret = "test-webhook-secret-32-chars-ok!!";

    private static PaymentWebhookValidator Build(string secret = Secret)
    {
        var options = Options.Create(new PaymentOptions { WebhookSecret = secret });
        return new PaymentWebhookValidator(options);
    }

    private static string Sign(string timestamp, string body, string secret = Secret)
    {
        var bytes = Encoding.UTF8.GetBytes($"{timestamp}.{body}");
        using var hmac = new HMACSHA256(Encoding.UTF8.GetBytes(secret));
        return Convert.ToBase64String(hmac.ComputeHash(bytes));
    }

    private static string ValidBody(string invoiceId = "KS-abc123", string eventType = "paid") =>
        JsonSerializer.Serialize(new
        {
            invoiceId,
            eventType,
            amount = 1500m,
            currency = "KZT",
            provider = "KaspiSandbox",
        });

    private static Dictionary<string, string> ValidHeaders(string body, string? timestamp = null)
    {
        timestamp ??= DateTimeOffset.UtcNow.ToString("o");
        return new Dictionary<string, string>
        {
            ["X-Kaspi-Timestamp"] = timestamp,
            ["X-Kaspi-Signature"] = Sign(timestamp, body),
        };
    }

    [Fact]
    public async Task ValidateAsync_ValidSignedWebhook_Succeeds()
    {
        var body = ValidBody();
        var headers = ValidHeaders(body);
        var validator = Build();

        var result = await validator.ValidateAsync(body, headers);

        Assert.True(result.Succeeded);
        Assert.Equal("KS-abc123", result.Data!.InvoiceId);
        Assert.Equal("paid", result.Data.EventType);
        Assert.Equal(1500m, result.Data.Amount);
    }

    [Fact]
    public async Task ValidateAsync_MissingTimestampHeader_Fails400()
    {
        var body = ValidBody();
        var headers = new Dictionary<string, string> { ["X-Kaspi-Signature"] = Sign(DateTimeOffset.UtcNow.ToString("o"), body) };
        var validator = Build();

        var result = await validator.ValidateAsync(body, headers);

        Assert.False(result.Succeeded);
        Assert.Equal(400, result.StatusCode);
    }

    [Fact]
    public async Task ValidateAsync_MissingSignatureHeader_Fails400()
    {
        var body = ValidBody();
        var headers = new Dictionary<string, string> { ["X-Kaspi-Timestamp"] = DateTimeOffset.UtcNow.ToString("o") };
        var validator = Build();

        var result = await validator.ValidateAsync(body, headers);

        Assert.False(result.Succeeded);
        Assert.Equal(400, result.StatusCode);
    }

    [Fact]
    public async Task ValidateAsync_TimestampTooOld_Fails400()
    {
        var body = ValidBody();
        var oldTimestamp = DateTimeOffset.UtcNow.AddMinutes(-10).ToString("o");
        var headers = ValidHeaders(body, oldTimestamp);
        var validator = Build();

        var result = await validator.ValidateAsync(body, headers);

        Assert.False(result.Succeeded);
        Assert.Equal(400, result.StatusCode);
    }

    [Fact]
    public async Task ValidateAsync_TimestampTooFarInFuture_Fails400()
    {
        var body = ValidBody();
        var futureTimestamp = DateTimeOffset.UtcNow.AddMinutes(10).ToString("o");
        var headers = ValidHeaders(body, futureTimestamp);
        var validator = Build();

        var result = await validator.ValidateAsync(body, headers);

        Assert.False(result.Succeeded);
        Assert.Equal(400, result.StatusCode);
    }

    [Fact]
    public async Task ValidateAsync_WrongSecret_Fails401()
    {
        var body = ValidBody();
        var timestamp = DateTimeOffset.UtcNow.ToString("o");
        var headers = new Dictionary<string, string>
        {
            ["X-Kaspi-Timestamp"] = timestamp,
            ["X-Kaspi-Signature"] = Sign(timestamp, body, secret: "a-completely-different-secret!!"),
        };
        var validator = Build();

        var result = await validator.ValidateAsync(body, headers);

        Assert.False(result.Succeeded);
        Assert.Equal(401, result.StatusCode);
    }

    [Fact]
    public async Task ValidateAsync_TamperedBody_Fails401()
    {
        var originalBody = ValidBody();
        var timestamp = DateTimeOffset.UtcNow.ToString("o");
        var headers = ValidHeaders(originalBody, timestamp);
        var tamperedBody = ValidBody(eventType: "failed"); // signature no longer matches
        var validator = Build();

        var result = await validator.ValidateAsync(tamperedBody, headers);

        Assert.False(result.Succeeded);
        Assert.Equal(401, result.StatusCode);
    }

    [Fact]
    public async Task ValidateAsync_NonBase64Signature_Fails400()
    {
        var body = ValidBody();
        var timestamp = DateTimeOffset.UtcNow.ToString("o");
        var headers = new Dictionary<string, string>
        {
            ["X-Kaspi-Timestamp"] = timestamp,
            ["X-Kaspi-Signature"] = "not-valid-base64!!!",
        };
        var validator = Build();

        var result = await validator.ValidateAsync(body, headers);

        Assert.False(result.Succeeded);
        Assert.Equal(400, result.StatusCode);
    }

    [Fact]
    public async Task ValidateAsync_MalformedJsonBody_Fails400()
    {
        var body = "{ not valid json";
        var headers = ValidHeaders(body);
        var validator = Build();

        var result = await validator.ValidateAsync(body, headers);

        Assert.False(result.Succeeded);
        Assert.Equal(400, result.StatusCode);
    }

    [Fact]
    public async Task ValidateAsync_MissingInvoiceId_Fails400()
    {
        var body = JsonSerializer.Serialize(new { invoiceId = "", eventType = "paid", amount = 100m, currency = "KZT", provider = "KaspiSandbox" });
        var headers = ValidHeaders(body);
        var validator = Build();

        var result = await validator.ValidateAsync(body, headers);

        Assert.False(result.Succeeded);
        Assert.Equal(400, result.StatusCode);
    }

    [Fact]
    public async Task ValidateAsync_UnsupportedProvider_Fails400()
    {
        var body = JsonSerializer.Serialize(new { invoiceId = "KS-1", eventType = "paid", amount = 100m, currency = "KZT", provider = "NotARealProvider" });
        var headers = ValidHeaders(body);
        var validator = Build();

        var result = await validator.ValidateAsync(body, headers);

        Assert.False(result.Succeeded);
        Assert.Equal(400, result.StatusCode);
    }

    [Fact]
    public async Task ValidateAsync_UnsupportedCurrency_Fails400()
    {
        var body = JsonSerializer.Serialize(new { invoiceId = "KS-1", eventType = "paid", amount = 100m, currency = "GBP", provider = "KaspiSandbox" });
        var headers = ValidHeaders(body);
        var validator = Build();

        var result = await validator.ValidateAsync(body, headers);

        Assert.False(result.Succeeded);
        Assert.Equal(400, result.StatusCode);
    }
}
