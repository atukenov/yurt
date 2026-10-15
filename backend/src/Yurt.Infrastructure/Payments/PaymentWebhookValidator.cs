using System.Collections.Generic;
using System.Linq;
using System.Security.Cryptography;
using System.Text;
using System.Text.Json;
using Microsoft.Extensions.Options;
using Yurt.Application.Common.Interfaces;
using Yurt.Application.Common.Models;
using Yurt.Application.Features.Payments.DTOs;
using Yurt.Domain.Enums;

namespace Yurt.Infrastructure.Payments;

public class PaymentWebhookValidator : IPaymentWebhookValidator
{
  private readonly PaymentOptions _options;

  public PaymentWebhookValidator(IOptions<PaymentOptions> options)
  {
    _options = options.Value;
  }

  public Task<Result<ValidatedPaymentWebhook>> ValidateAsync(
      string rawBody,
      IReadOnlyDictionary<string, string> headers,
      CancellationToken cancellationToken = default)
  {
    if (!headers.TryGetValue("X-Kaspi-Timestamp", out var timestamp) || string.IsNullOrEmpty(timestamp))
      return Task.FromResult(Result<ValidatedPaymentWebhook>.Failure("Missing webhook timestamp header.", 400));

    if (!headers.TryGetValue("X-Kaspi-Signature", out var signature) || string.IsNullOrEmpty(signature))
      return Task.FromResult(Result<ValidatedPaymentWebhook>.Failure("Missing webhook signature header.", 400));

    var timestampValue = timestamp;
    var signatureValue = signature;

    if (!DateTimeOffset.TryParse(timestampValue, out var sentAt))
      return Task.FromResult(Result<ValidatedPaymentWebhook>.Failure("Invalid webhook timestamp.", 400));

    if (Math.Abs((DateTimeOffset.UtcNow - sentAt).TotalMinutes) > 5)
      return Task.FromResult(Result<ValidatedPaymentWebhook>.Failure("Webhook timestamp is outside allowed window.", 400));

    var computedSignature = ComputeSignature(timestampValue, rawBody, _options.WebhookSecret);
    try
    {
      var receivedBytes = Convert.FromBase64String(signatureValue);
      var expectedBytes = Convert.FromBase64String(computedSignature);
      if (!CryptographicOperations.FixedTimeEquals(receivedBytes, expectedBytes))
        return Task.FromResult(Result<ValidatedPaymentWebhook>.Failure("Webhook signature validation failed.", 401));
    }
    catch (FormatException)
    {
      return Task.FromResult(Result<ValidatedPaymentWebhook>.Failure("Invalid webhook signature format.", 400));
    }

    PaymentWebhookPayload? payload;
    try
    {
      payload = JsonSerializer.Deserialize<PaymentWebhookPayload>(rawBody, new JsonSerializerOptions
      {
        PropertyNameCaseInsensitive = true
      });
    }
    catch (JsonException)
    {
      return Task.FromResult(Result<ValidatedPaymentWebhook>.Failure("Invalid webhook payload.", 400));
    }

    if (payload == null || string.IsNullOrWhiteSpace(payload.InvoiceId) || string.IsNullOrWhiteSpace(payload.EventType))
      return Task.FromResult(Result<ValidatedPaymentWebhook>.Failure("Webhook payload is incomplete.", 400));

    if (!Enum.TryParse<PaymentProvider>(payload.Provider, true, out var provider))
      return Task.FromResult(Result<ValidatedPaymentWebhook>.Failure("Unsupported payment provider.", 400));

    if (!Enum.TryParse<Currency>(payload.Currency, true, out var currency))
      return Task.FromResult(Result<ValidatedPaymentWebhook>.Failure("Unsupported currency.", 400));

    var validated = new ValidatedPaymentWebhook(
        payload.InvoiceId,
        payload.EventType,
        payload.Amount,
        currency,
        provider,
        sentAt.UtcDateTime,
        rawBody);

    return Task.FromResult(Result<ValidatedPaymentWebhook>.Success(validated));
  }

  private static string ComputeSignature(string timestamp, string body, string secret)
  {
    var bytes = Encoding.UTF8.GetBytes($"{timestamp}.{body}");
    using var hmac = new HMACSHA256(Encoding.UTF8.GetBytes(secret));
    return Convert.ToBase64String(hmac.ComputeHash(bytes));
  }

  private sealed class PaymentWebhookPayload
  {
    public string InvoiceId { get; set; } = string.Empty;
    public string EventType { get; set; } = string.Empty;
    public decimal Amount { get; set; }
    public string Currency { get; set; } = string.Empty;
    public string Provider { get; set; } = string.Empty;
    public string Timestamp { get; set; } = string.Empty;
  }
}
