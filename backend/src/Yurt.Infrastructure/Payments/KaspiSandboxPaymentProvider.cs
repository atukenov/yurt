using System.Collections.Concurrent;
using System.Net.Http.Headers;
using System.Security.Cryptography;
using System.Text;
using System.Text.Json;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using QRCoder;
using Yurt.Application.Common.Interfaces;
using Yurt.Application.Features.Payments.DTOs;
using Yurt.Domain.Enums;

namespace Yurt.Infrastructure.Payments;

public class KaspiSandboxPaymentProvider : IPaymentProvider
{
  private readonly IHttpClientFactory _httpClientFactory;
  private readonly PaymentOptions _options;
  private readonly ILogger<KaspiSandboxPaymentProvider> _logger;
  private readonly ConcurrentDictionary<string, SandboxInvoiceState> _invoices = new();

  public PaymentProvider Provider => PaymentProvider.KaspiSandbox;

  public KaspiSandboxPaymentProvider(
      IHttpClientFactory httpClientFactory,
      IOptions<PaymentOptions> options,
      ILogger<KaspiSandboxPaymentProvider> logger)
  {
    _httpClientFactory = httpClientFactory;
    _options = options.Value;
    _logger = logger;
  }

  public Task<PaymentStatusResponse> GetPaymentStatusAsync(string invoiceId, CancellationToken cancellationToken = default)
  {
    if (!_invoices.TryGetValue(invoiceId, out var state))
      throw new InvalidOperationException("Invoice not found in sandbox provider.");

    var response = new PaymentStatusResponse(
        invoiceId,
        state.Status,
        state.OrderId,
        state.OrderStatus,
        state.Amount,
        state.Currency,
        Provider,
        state.PaymentUrl,
        state.QrCode,
        state.CreatedAt,
        state.UpdatedAt,
        state.RawResponse);

    return Task.FromResult(response);
  }

  public Task<PaymentInvoiceResponse> CreateInvoiceAsync(CreatePaymentInvoiceRequest request, CancellationToken cancellationToken = default)
  {
    var invoiceId = $"KS-{Guid.NewGuid():N}";
    var expiresAt = DateTime.UtcNow.AddMinutes(20);
    var paymentUrl = $"{_options.SandboxBaseUrl.TrimEnd('/')}/pay/{invoiceId}";
    var payload = GenerateQrCodePayload(invoiceId, paymentUrl, request.Amount, request.Currency);
    var qrCode = GenerateQrCodeImage(payload);

    var state = new SandboxInvoiceState(
        request.OrderId,
        invoiceId,
        request.Amount,
        request.Currency,
        request.Provider,
        paymentUrl,
        qrCode,
        DateTime.UtcNow,
        expiresAt,
        request.SandboxBehavior);

    _invoices.TryAdd(invoiceId, state);
    Task.Run(() => SimulateAsync(state), CancellationToken.None);

    return Task.FromResult(new PaymentInvoiceResponse(
        Guid.Empty,
        invoiceId,
        paymentUrl,
        qrCode,
        request.Amount,
        request.Currency,
        request.Provider,
        PaymentRecordStatus.Pending,
        state.CreatedAt,
        expiresAt,
        null));
  }

  private async Task SimulateAsync(SandboxInvoiceState state)
  {
    try
    {
      var delay = Random.Shared.Next(_options.ProcessingDelaySecondsMin, _options.ProcessingDelaySecondsMax + 1);
      await Task.Delay(TimeSpan.FromSeconds(delay));

      var eventType = DetermineEventType(state.SandboxBehavior);
      await SendWebhookAsync(state, eventType);

      if (state.SandboxBehavior == SandboxPaymentBehavior.DuplicateWebhook)
      {
        await Task.Delay(TimeSpan.FromSeconds(2));
        await SendWebhookAsync(state, eventType);
      }
    }
    catch (Exception ex)
    {
      _logger.LogWarning(ex, "Sandbox payment simulation failed for invoice {InvoiceId}", state.InvoiceId);
    }
  }

  private static string DetermineEventType(SandboxPaymentBehavior behavior)
      => behavior switch
      {
        SandboxPaymentBehavior.Failure => "failed",
        SandboxPaymentBehavior.Expired => "expired",
        SandboxPaymentBehavior.DuplicateWebhook => "paid",
        SandboxPaymentBehavior.NetworkFailure => "paid",
        _ => "paid"
      };

  private async Task SendWebhookAsync(SandboxInvoiceState state, string eventType)
  {
    if (DateTime.UtcNow >= state.ExpiresAt && eventType == "paid")
      eventType = "expired";

    var payload = new
    {
      invoiceId = state.InvoiceId,
      eventType,
      amount = state.Amount,
      currency = state.Currency.ToString(),
      provider = state.Provider.ToString(),
      timestamp = DateTime.UtcNow.ToString("o")
    };

    state.Status = eventType switch
    {
      "paid" => PaymentRecordStatus.Paid,
      "failed" => PaymentRecordStatus.Failed,
      "expired" => PaymentRecordStatus.Expired,
      _ => state.Status
    };

    var rawBody = JsonSerializer.Serialize(payload);
    state.RawResponse = rawBody;
    state.UpdatedAt = DateTime.UtcNow;

    if (state.SandboxBehavior == SandboxPaymentBehavior.NetworkFailure && Random.Shared.NextDouble() < _options.NetworkFailureChance)
    {
      _logger.LogWarning("Simulating network failure for webhook invoice {InvoiceId}", state.InvoiceId);
      await Task.Delay(TimeSpan.FromSeconds(2));
      await PostWebhookAsync(rawBody, payload);
      return;
    }

    await PostWebhookAsync(rawBody, payload);
  }

  private async Task PostWebhookAsync(string body, object payload)
  {
    using var request = new HttpRequestMessage(HttpMethod.Post, _options.WebhookCallbackUrl)
    {
      Content = new StringContent(body, Encoding.UTF8, "application/json")
    };

    var timestamp = DateTime.UtcNow.ToString("o");
    var signature = ComputeSignature(timestamp, body, _options.WebhookSecret);
    request.Headers.Add("X-Kaspi-Timestamp", timestamp);
    request.Headers.Add("X-Kaspi-Signature", signature);

    var client = _httpClientFactory.CreateClient();
    var response = await client.SendAsync(request);
    if (!response.IsSuccessStatusCode)
    {
      _logger.LogWarning("Sandbox webhook delivery returned {StatusCode} for invoice {InvoiceId}", response.StatusCode, payload.GetType().GetProperty("invoiceId")?.GetValue(payload));
    }
  }

  private static string GenerateQrCodePayload(string invoiceId, string paymentUrl, decimal amount, Currency currency)
  {
    var payload = new
    {
      provider = "KaspiSandbox",
      invoiceId,
      paymentUrl,
      amount,
      currency = currency.ToString()
    };
    return JsonSerializer.Serialize(payload);
  }

  private static string GenerateQrCodeImage(string payload)
  {
    using var qrGenerator = new QRCodeGenerator();
    using var qrCodeData = qrGenerator.CreateQrCode(payload, QRCodeGenerator.ECCLevel.Q);
    using var qrCode = new PngByteQRCode(qrCodeData);
    var bytes = qrCode.GetGraphic(20);
    return $"data:image/png;base64,{Convert.ToBase64String(bytes)}";
  }

  private static string ComputeSignature(string timestamp, string body, string secret)
  {
    var bytes = Encoding.UTF8.GetBytes($"{timestamp}.{body}");
    using var hmac = new HMACSHA256(Encoding.UTF8.GetBytes(secret));
    return Convert.ToBase64String(hmac.ComputeHash(bytes));
  }

  private sealed class SandboxInvoiceState
  {
    public Guid OrderId { get; }
    public string InvoiceId { get; }
    public decimal Amount { get; }
    public Currency Currency { get; }
    public PaymentProvider Provider { get; }
    public string PaymentUrl { get; }
    public string QrCode { get; }
    public DateTime CreatedAt { get; }
    public DateTime ExpiresAt { get; }
    public SandboxPaymentBehavior SandboxBehavior { get; }
    public PaymentRecordStatus Status { get; set; } = PaymentRecordStatus.Pending;
    public string? RawResponse { get; set; }
    public DateTime? UpdatedAt { get; set; }
    public string OrderStatus { get; set; } = Yurt.Domain.Enums.OrderStatus.Created.ToString();

    public SandboxInvoiceState(
        Guid orderId,
        string invoiceId,
        decimal amount,
        Currency currency,
        PaymentProvider provider,
        string paymentUrl,
        string qrCode,
        DateTime createdAt,
        DateTime expiresAt,
        SandboxPaymentBehavior sandboxBehavior)
    {
      OrderId = orderId;
      InvoiceId = invoiceId;
      Amount = amount;
      Currency = currency;
      Provider = provider;
      PaymentUrl = paymentUrl;
      QrCode = qrCode;
      CreatedAt = createdAt;
      ExpiresAt = expiresAt;
      SandboxBehavior = sandboxBehavior;
    }
  }
}
