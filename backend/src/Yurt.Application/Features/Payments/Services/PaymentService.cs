using System.Collections.Generic;
using System.Text.Json;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;
using Yurt.Application.Common.Interfaces;
using Yurt.Application.Common.Models;
using Yurt.Application.Features.Payments.DTOs;
using Yurt.Domain.Entities;
using Yurt.Domain.Enums;

namespace Yurt.Application.Features.Payments.Services;

public class PaymentService
{
  private readonly IApplicationDbContext _db;
  private readonly IOrdersHubService _ordersHub;
  private readonly IPaymentProviderFactory _providerFactory;
  private readonly IPaymentWebhookValidator _webhookValidator;
  private readonly FreedomPayOptions _freedomPayOptions;
  private readonly ILogger<PaymentService> _logger;

  public PaymentService(
      IApplicationDbContext db,
      IOrdersHubService ordersHub,
      IPaymentProviderFactory providerFactory,
      IPaymentWebhookValidator webhookValidator,
      FreedomPayOptions freedomPayOptions,
      ILogger<PaymentService> logger)
  {
    _db = db;
    _ordersHub = ordersHub;
    _providerFactory = providerFactory;
    _webhookValidator = webhookValidator;
    _freedomPayOptions = freedomPayOptions;
    _logger = logger;
  }

  public async Task<Result<PaymentInvoiceResponse>> CreatePaymentAsync(
      Guid customerId,
      CreatePaymentDto dto,
      CancellationToken cancellationToken = default)
  {
    var order = await _db.Orders
        .Include(o => o.Items)
        .FirstOrDefaultAsync(o => o.Id == dto.OrderId, cancellationToken);

    if (order == null)
      return Result<PaymentInvoiceResponse>.NotFound("Order not found.");

    if (order.CustomerUserId != customerId)
      return Result<PaymentInvoiceResponse>.Forbidden();

    if (order.Status != OrderStatus.Created)
      return Result<PaymentInvoiceResponse>.Failure("Payment can only be created for a new order.", 422);

    if (order.Total <= 0)
      return Result<PaymentInvoiceResponse>.Failure("Order amount must be greater than zero.", 422);

    var pendingPayment = await _db.Payments
        .FirstOrDefaultAsync(p => p.OrderId == order.Id && p.Status == PaymentRecordStatus.Pending, cancellationToken);

    if (pendingPayment != null)
      return Result<PaymentInvoiceResponse>.Failure("A payment is already pending for this order.", 409);

    if (dto.Provider == PaymentProvider.FreedomPay && !_freedomPayOptions.Enabled)
      return Result<PaymentInvoiceResponse>.Failure("Apple Pay is not enabled.", 422);

    var provider = _providerFactory.GetProvider(dto.Provider);
    var invoiceRequest = new CreatePaymentInvoiceRequest(
        order.Id,
        customerId,
        order.Total,
        Currency.KZT,
        dto.Provider,
        dto.SandboxBehavior);

    var invoice = await provider.CreateInvoiceAsync(invoiceRequest, cancellationToken);

    var payment = new Payment
    {
      OrderId = order.Id,
      Provider = dto.Provider,
      InvoiceId = invoice.InvoiceId,
      Amount = invoice.Amount,
      Currency = invoice.Currency,
      Status = PaymentRecordStatus.Pending,
      PaymentUrl = invoice.PaymentUrl,
      QrCode = invoice.QrCode,
      RawResponse = invoice.RawResponse,
      ExpiresAt = invoice.ExpiresAt
    };

    _db.Payments.Add(payment);
    await _db.SaveChangesAsync(cancellationToken);

    await _ordersHub.NotifyPaymentPendingAsync(order, cancellationToken);

    return Result<PaymentInvoiceResponse>.Success(invoice, 201);
  }

  public async Task<Result<PaymentStatusResponse>> GetPaymentStatusAsync(
      string invoiceId,
      CancellationToken cancellationToken = default)
  {
    var payment = await _db.Payments
        .Include(p => p.Order)
        .FirstOrDefaultAsync(p => p.InvoiceId == invoiceId, cancellationToken);

    if (payment == null)
      return Result<PaymentStatusResponse>.NotFound("Payment not found.");

    return Result<PaymentStatusResponse>.Success(new PaymentStatusResponse(
        payment.InvoiceId,
        payment.Status,
        payment.OrderId,
        payment.Order.Status.ToString(),
        payment.Amount,
        payment.Currency,
        payment.Provider,
        payment.PaymentUrl,
        payment.QrCode,
        payment.CreatedAt,
        payment.UpdatedAt,
        payment.RawResponse));
  }

  public ApplePayConfigResponse GetApplePayConfig()
      => new(_freedomPayOptions.Enabled, _freedomPayOptions.AppleMerchantId);

  /// <summary>Marks an Apple Pay payment Paid after the native Freedom Pay SDK flow succeeds
  /// client-side. Unlike <see cref="ProcessWebhookAsync"/>, there is no independent
  /// server-to-server verification against Freedom Pay here — Freedom Pay's server-side
  /// verification API isn't confirmed yet (see issue #49's open questions). This trusts the
  /// authenticated customer's own report that their device completed the charge, which is
  /// weaker than a provider webhook; harden this once that API is confirmed.</summary>
  public async Task<Result<PaymentStatusResponse>> ConfirmApplePayAsync(
      Guid customerId, ConfirmApplePayDto dto, CancellationToken cancellationToken = default)
  {
    if (!_freedomPayOptions.Enabled)
      return Result<PaymentStatusResponse>.Failure("Apple Pay is not enabled.", 422);

    var payment = await _db.Payments
        .Include(p => p.Order)
        .FirstOrDefaultAsync(
            p => p.InvoiceId == dto.InvoiceId && p.Provider == PaymentProvider.FreedomPay,
            cancellationToken);

    if (payment == null) return Result<PaymentStatusResponse>.NotFound("Payment invoice not found.");
    if (payment.Order.CustomerUserId != customerId) return Result<PaymentStatusResponse>.Forbidden();

    if (payment.Status == PaymentRecordStatus.Paid)
      return Result<PaymentStatusResponse>.Success(BuildStatusResponse(payment)); // idempotent retry

    if (payment.Status != PaymentRecordStatus.Pending)
      return Result<PaymentStatusResponse>.Failure("Payment is no longer pending.", 409);

    payment.Status = PaymentRecordStatus.Paid;
    payment.RawResponse = JsonSerializer.Serialize(new { freedomPaymentId = dto.FreedomPaymentId });
    payment.UpdatedAt = DateTime.UtcNow;
    payment.Order.PaymentStatus = PaymentStatus.Paid;
    if (payment.Order.Status == OrderStatus.Created) payment.Order.Status = OrderStatus.Preparing;

    await _db.SaveChangesAsync(cancellationToken);

    await _ordersHub.NotifyPaymentUpdatedAsync(payment.Order, cancellationToken);
    await _ordersHub.NotifyPaymentSucceededAsync(payment.Order, cancellationToken);

    return Result<PaymentStatusResponse>.Success(BuildStatusResponse(payment));
  }

  public async Task<Result<PaymentStatusResponse>> ProcessWebhookAsync(
      string rawBody,
      IReadOnlyDictionary<string, string> headers,
      CancellationToken cancellationToken = default)
  {
    var validation = await _webhookValidator.ValidateAsync(rawBody, headers, cancellationToken);
    if (!validation.Succeeded)
    {
      _logger.LogWarning("Payment webhook validation failed: {Error}", validation.Error);
      return Result<PaymentStatusResponse>.Failure(validation.Error ?? "Webhook validation failed.", 400);
    }

    var webhook = validation.Data!;
    var logEntry = new PaymentWebhookLog
    {
      Provider = webhook.Provider,
      Payload = rawBody,
      Headers = string.Join(";", headers.Select(h => $"{h.Key}:{string.Join(',', h.Value)}")),
      Processed = false
    };

    _db.PaymentWebhookLogs.Add(logEntry);

    var payment = await _db.Payments
        .Include(p => p.Order)
        .FirstOrDefaultAsync(p => p.InvoiceId == webhook.InvoiceId, cancellationToken);

    if (payment == null)
    {
      await _db.SaveChangesAsync(cancellationToken);
      return Result<PaymentStatusResponse>.NotFound("Payment invoice not found.");
    }

    var targetStatus = MapWebhookEventToStatus(webhook.EventType);
    if (targetStatus == null)
    {
      await _db.SaveChangesAsync(cancellationToken);
      return Result<PaymentStatusResponse>.Failure("Unsupported webhook event type.", 422);
    }

    if (payment.Status == targetStatus)
    {
      logEntry.Processed = true;
      await _db.SaveChangesAsync(cancellationToken);
      return Result<PaymentStatusResponse>.Success(BuildStatusResponse(payment));
    }

    if (payment.Status != PaymentRecordStatus.Pending)
    {
      logEntry.Processed = true;
      await _db.SaveChangesAsync(cancellationToken);
      return Result<PaymentStatusResponse>.Success(BuildStatusResponse(payment));
    }

    payment.Status = targetStatus.Value;
    payment.RawResponse = rawBody;
    payment.UpdatedAt = DateTime.UtcNow;

    if (targetStatus == PaymentRecordStatus.Paid)
    {
      payment.Order.PaymentStatus = PaymentStatus.Paid;
      if (payment.Order.Status == OrderStatus.Created)
        payment.Order.Status = OrderStatus.Preparing;
    }

    logEntry.Processed = true;
    await _db.SaveChangesAsync(cancellationToken);

    await _ordersHub.NotifyPaymentUpdatedAsync(payment.Order, cancellationToken);

    if (targetStatus == PaymentRecordStatus.Paid)
      await _ordersHub.NotifyPaymentSucceededAsync(payment.Order, cancellationToken);
    else if (targetStatus == PaymentRecordStatus.Failed || targetStatus == PaymentRecordStatus.Expired)
      await _ordersHub.NotifyPaymentFailedAsync(payment.Order, cancellationToken);

    return Result<PaymentStatusResponse>.Success(BuildStatusResponse(payment));
  }

  private static PaymentRecordStatus? MapWebhookEventToStatus(string eventType)
      => eventType.ToLowerInvariant() switch
      {
        "paid" => PaymentRecordStatus.Paid,
        "failed" => PaymentRecordStatus.Failed,
        "expired" => PaymentRecordStatus.Expired,
        _ => null
      };

  private static PaymentStatusResponse BuildStatusResponse(Payment payment)
      => new(
          payment.InvoiceId,
          payment.Status,
          payment.OrderId,
          payment.Order.Status.ToString(),
          payment.Amount,
          payment.Currency,
          payment.Provider,
          payment.PaymentUrl,
          payment.QrCode,
          payment.CreatedAt,
          payment.UpdatedAt,
          payment.RawResponse);
}
