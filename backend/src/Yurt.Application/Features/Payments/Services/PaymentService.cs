using System.Collections.Generic;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;
using Yurt.Application.Common.Interfaces;
using Yurt.Application.Common.Models;
using Yurt.Application.Features.Payments.DTOs;
using Yurt.Application.Features.Printing;
using Yurt.Domain;
using Yurt.Domain.Entities;
using Yurt.Domain.Enums;

namespace Yurt.Application.Features.Payments.Services;

/// <summary>Where the payment page may send the customer back to.</summary>
public class PaymentReturnOptions
{
  /// <summary>Web addresses of the customer app (the CORS list). A return address from the app must start with one.</summary>
  public List<string> AllowedOrigins { get; set; } = [];

  /// <summary>Used when the app sends no return address (the phone apps) and the cafe has no domain of its own.</summary>
  public string? CustomerAppUrl { get; set; }
}

/// <summary>
/// Online payments of customer orders. Each cafe picks its provider on the Platform screen (Stripe today) and pays into
/// its own account. A paid order stays "new" until staff accept it, like any other order; declining it gives the money
/// back automatically.
/// </summary>
public class PaymentService
{
  private readonly IApplicationDbContext _db;
  private readonly IOrdersHubService _ordersHub;
  private readonly IPaymentProviderFactory _providerFactory;
  private readonly ITenantContext _tenant;
  private readonly IModuleGate _modules;
  private readonly IAuditLogService _audit;
  private readonly PaymentReturnOptions _returnOptions;
  private readonly ILogger<PaymentService> _logger;

  public PaymentService(
      IApplicationDbContext db,
      IOrdersHubService ordersHub,
      IPaymentProviderFactory providerFactory,
      ITenantContext tenant,
      IModuleGate modules,
      IAuditLogService audit,
      PaymentReturnOptions returnOptions,
      ILogger<PaymentService> logger)
  {
    _db = db;
    _ordersHub = ordersHub;
    _providerFactory = providerFactory;
    _tenant = tenant;
    _modules = modules;
    _audit = audit;
    _returnOptions = returnOptions;
    _logger = logger;
  }

  /// <summary>The provider customers of this cafe can pay with online, or null when they can't (module off, no
  /// provider chosen, or its keys are missing), so the app only shows the button when paying will work.</summary>
  public async Task<PaymentConfigResponse> GetConfigAsync(CancellationToken cancellationToken = default)
  {
    if (!await _modules.IsEnabledAsync(ModuleKeys.OnlinePayments, cancellationToken))
      return new PaymentConfigResponse(null);

    var provider = await CafeProviderAsync(cancellationToken);
    return provider is not null && await provider.IsConfiguredAsync(cancellationToken)
        ? new PaymentConfigResponse(provider.Provider.ToString().ToLowerInvariant())
        : new PaymentConfigResponse(null);
  }

  public async Task<Result<PaymentInvoiceResponse>> CreatePaymentAsync(
      Guid customerId,
      CreatePaymentDto dto,
      CancellationToken cancellationToken = default)
  {
    var order = await _db.Orders
        .FirstOrDefaultAsync(o => o.Id == dto.OrderId, cancellationToken);

    if (order == null)
      return Result<PaymentInvoiceResponse>.NotFound("Order not found.");

    if (order.CustomerUserId != customerId)
      return Result<PaymentInvoiceResponse>.Forbidden();

    if (order.Status != OrderStatus.Created)
      return Result<PaymentInvoiceResponse>.Failure("Payment can only be created for a new order.", 422);

    if (order.PaymentStatus != PaymentStatus.Unpaid)
      return Result<PaymentInvoiceResponse>.Failure("This order is already paid.", 409);

    if (order.Total <= 0)
      return Result<PaymentInvoiceResponse>.Failure("Order amount must be greater than zero.", 422);

    var provider = await CafeProviderAsync(cancellationToken);
    if (provider == null || !await provider.IsConfiguredAsync(cancellationToken))
      return Result<PaymentInvoiceResponse>.Failure("This cafe does not take online payments.", 422);

    // Coming back to pay again (for example after closing the payment page) reopens the same page while it is valid,
    // so one order never has two open payment pages.
    var pending = await _db.Payments
        .Where(p => p.OrderId == order.Id && p.Status == PaymentRecordStatus.Pending)
        .ToListAsync(cancellationToken);
    var reusable = pending.FirstOrDefault(p => p.Provider == provider.Provider && p.ExpiresAt > DateTime.UtcNow.AddMinutes(1));
    if (reusable != null)
      return Result<PaymentInvoiceResponse>.Success(ToInvoice(reusable));
    foreach (var stale in pending)
    {
      stale.Status = PaymentRecordStatus.Expired;
      stale.UpdatedAt = DateTime.UtcNow;
    }

    var returnBase = await ReturnBaseAsync(dto.ReturnUrl, cancellationToken);
    if (returnBase == null)
      return Result<PaymentInvoiceResponse>.Failure("No web address to come back to after paying is set for this cafe.", 422);

    var paymentId = Guid.NewGuid();
    var orderPage = $"{returnBase}/orders/{order.Id}";
    var invoiceRequest = new CreatePaymentInvoiceRequest(
        paymentId,
        order.Id,
        PrintLayouts.ShortId(order.Id),
        order.Total,
        Currency.KZT,
        $"{orderPage}?payment=success",
        $"{orderPage}?payment=cancelled",
        _tenant.Slug);

    PaymentInvoiceResponse invoice;
    try
    {
      invoice = await provider.CreateInvoiceAsync(invoiceRequest, cancellationToken);
    }
    catch (PaymentProviderException ex)
    {
      _logger.LogWarning("Could not open a payment page for order {OrderId}: {Error}", order.Id, ex.Message);
      return Result<PaymentInvoiceResponse>.Failure("The payment service did not answer. Please try again.", 502);
    }

    var payment = new Payment
    {
      Id = paymentId,
      OrderId = order.Id,
      Provider = provider.Provider,
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

    return Result<PaymentInvoiceResponse>.Success(ToInvoice(payment), 201);
  }

  public async Task<Result<PaymentStatusResponse>> GetPaymentStatusAsync(
      Guid customerId,
      string invoiceId,
      CancellationToken cancellationToken = default)
  {
    var payment = await _db.Payments
        .Include(p => p.Order)
        .FirstOrDefaultAsync(p => p.InvoiceId == invoiceId, cancellationToken);

    if (payment == null)
      return Result<PaymentStatusResponse>.NotFound("Payment not found.");

    if (payment.Order.CustomerUserId != customerId)
      return Result<PaymentStatusResponse>.NotFound("Payment not found.");

    return Result<PaymentStatusResponse>.Success(BuildStatusResponse(payment));
  }

  /// <summary>Handles a webhook of <paramref name="providerKind"/> for the current cafe. Events about payments Yurt does
  /// not know (other sales on the same account) are accepted and ignored, so the provider stops resending them.</summary>
  public async Task<Result> ProcessWebhookAsync(
      PaymentProvider providerKind,
      string rawBody,
      IReadOnlyDictionary<string, string> headers,
      CancellationToken cancellationToken = default)
  {
    var provider = _providerFactory.GetProvider(providerKind);
    var parsed = await provider.ParseWebhookAsync(rawBody, headers, cancellationToken);
    if (!parsed.Succeeded)
    {
      _logger.LogWarning("Payment webhook validation failed: {Error}", parsed.Error);
      return Result.Failure(parsed.Error ?? "Webhook validation failed.", parsed.StatusCode);
    }

    var webhook = parsed.Data;
    if (webhook == null)
      return Result.Success();

    var logEntry = new PaymentWebhookLog
    {
      Provider = providerKind,
      Payload = rawBody,
      Headers = string.Join(";", headers.Select(h => $"{h.Key}:{h.Value}")),
      Processed = false
    };
    _db.PaymentWebhookLogs.Add(logEntry);

    var payment = await FindPaymentAsync(providerKind, webhook, cancellationToken);
    if (payment == null)
    {
      await _db.SaveChangesAsync(cancellationToken);
      return Result.Success();
    }

    var order = payment.Order;
    var changed = false;
    var refundNow = false;
    switch (webhook.Kind)
    {
      case PaymentWebhookKind.Paid when payment.Status == PaymentRecordStatus.Pending:
        payment.Status = PaymentRecordStatus.Paid;
        payment.ProviderPaymentId = webhook.ProviderPaymentId ?? payment.ProviderPaymentId;
        order.PaymentStatus = PaymentStatus.Paid;
        // The customer finished paying after the order was declined or cancelled: give the money back right away.
        refundNow = order.Status == OrderStatus.Declined;
        changed = true;
        break;
      case PaymentWebhookKind.Failed or PaymentWebhookKind.Expired when payment.Status == PaymentRecordStatus.Pending:
        payment.Status = webhook.Kind == PaymentWebhookKind.Failed ? PaymentRecordStatus.Failed : PaymentRecordStatus.Expired;
        changed = true;
        break;
      case PaymentWebhookKind.Refunded when payment.Status == PaymentRecordStatus.Paid:
        // A refund made in the provider's own dashboard.
        payment.Status = PaymentRecordStatus.Refunded;
        order.PaymentStatus = PaymentStatus.Refunded;
        changed = true;
        break;
    }

    if (changed)
    {
      payment.RawResponse = rawBody;
      payment.UpdatedAt = DateTime.UtcNow;
      order.UpdatedAt = DateTime.UtcNow;
    }
    logEntry.Processed = true;
    await _db.SaveChangesAsync(cancellationToken);

    if (!changed)
      return Result.Success();

    await _ordersHub.NotifyPaymentUpdatedAsync(order, cancellationToken);
    if (payment.Status == PaymentRecordStatus.Paid)
      await _ordersHub.NotifyPaymentSucceededAsync(order, cancellationToken);
    else if (payment.Status is PaymentRecordStatus.Failed or PaymentRecordStatus.Expired)
      await _ordersHub.NotifyPaymentFailedAsync(order, cancellationToken);

    if (refundNow)
      await TryRefundAsync(payment, "paid after the order was declined", cancellationToken);

    return Result.Success();
  }

  /// <summary>Staff give an online payment back in full (Orders screen).</summary>
  public async Task<Result> RefundOrderAsync(Guid orderId, CancellationToken cancellationToken = default)
  {
    var payment = await _db.Payments
        .Include(p => p.Order)
        .Where(p => p.OrderId == orderId && p.Status == PaymentRecordStatus.Paid)
        .OrderByDescending(p => p.CreatedAt)
        .FirstOrDefaultAsync(cancellationToken);
    if (payment == null)
    {
      var exists = await _db.Orders.AnyAsync(o => o.Id == orderId, cancellationToken);
      return exists
          ? Result.Failure("This order has no online payment to give back.", 422)
          : Result.Failure("Order not found.", 404);
    }

    var refund = await RefundAsync(payment, cancellationToken);
    if (!refund.Succeeded) return refund;

    await _audit.LogAsync("OrderRefunded", "Order", orderId.ToString(),
        $"{payment.Provider} {payment.Amount} {payment.Currency}", cancellationToken);
    return Result.Success();
  }

  /// <summary>Called when an order is declined (by staff or the customer): an open payment page is closed and a paid
  /// payment is given back. Fails open: a provider error is logged and staff can press Refund again later.</summary>
  public async Task OnOrderDeclinedAsync(Order order, CancellationToken cancellationToken = default)
  {
    var payments = await _db.Payments
        .Include(p => p.Order)
        .Where(p => p.OrderId == order.Id
            && (p.Status == PaymentRecordStatus.Pending || p.Status == PaymentRecordStatus.Paid))
        .ToListAsync(cancellationToken);

    foreach (var payment in payments)
    {
      if (payment.Status == PaymentRecordStatus.Paid)
      {
        await TryRefundAsync(payment, "order declined", cancellationToken);
        continue;
      }

      try
      {
        await _providerFactory.GetProvider(payment.Provider).CancelInvoiceAsync(payment, cancellationToken);
        payment.Status = PaymentRecordStatus.Expired;
        payment.UpdatedAt = DateTime.UtcNow;
        await _db.SaveChangesAsync(cancellationToken);
      }
      catch (Exception ex) when (ex is PaymentProviderException or InvalidOperationException)
      {
        // Left Pending: if the customer still pays, the webhook sees the declined order and refunds it.
        _logger.LogWarning("Could not close the payment page of declined order {OrderId}: {Error}", order.Id, ex.Message);
      }
    }
  }

  private async Task TryRefundAsync(Payment payment, string reason, CancellationToken cancellationToken)
  {
    var result = await RefundAsync(payment, cancellationToken);
    if (result.Succeeded)
      await _audit.LogAsync("OrderRefunded", "Order", payment.OrderId.ToString(), $"automatic: {reason}", cancellationToken);
    else
      _logger.LogError("Automatic refund of order {OrderId} failed ({Reason}): {Error}", payment.OrderId, reason, result.Error);
  }

  private async Task<Result> RefundAsync(Payment payment, CancellationToken cancellationToken)
  {
    Result refund;
    try
    {
      refund = await _providerFactory.GetProvider(payment.Provider).RefundAsync(payment, cancellationToken);
    }
    catch (Exception ex) when (ex is PaymentProviderException or InvalidOperationException)
    {
      refund = Result.Failure(ex.Message, 502);
    }
    if (!refund.Succeeded) return refund;

    payment.Status = PaymentRecordStatus.Refunded;
    payment.UpdatedAt = DateTime.UtcNow;
    payment.Order.PaymentStatus = PaymentStatus.Refunded;
    payment.Order.UpdatedAt = DateTime.UtcNow;
    await _db.SaveChangesAsync(cancellationToken);
    await _ordersHub.NotifyPaymentUpdatedAsync(payment.Order, cancellationToken);
    return Result.Success();
  }

  private async Task<Payment?> FindPaymentAsync(PaymentProvider providerKind, PaymentWebhookEvent webhook, CancellationToken ct)
  {
    var query = _db.Payments.Include(p => p.Order).Where(p => p.Provider == providerKind);
    if (!string.IsNullOrEmpty(webhook.InvoiceId)
        && await query.FirstOrDefaultAsync(p => p.InvoiceId == webhook.InvoiceId, ct) is { } byInvoice)
      return byInvoice;
    if (!string.IsNullOrEmpty(webhook.ProviderPaymentId))
      return await query.FirstOrDefaultAsync(p => p.ProviderPaymentId == webhook.ProviderPaymentId, ct);
    return null;
  }

  private async Task<IPaymentProvider?> CafeProviderAsync(CancellationToken ct)
  {
    var name = await _db.Tenants.AsNoTracking()
        .Where(t => t.Id == _tenant.TenantId)
        .Select(t => t.PaymentProvider)
        .FirstOrDefaultAsync(ct);
    return PaymentProviders.ToEnum(name) is { } kind ? _providerFactory.GetProvider(kind) : null;
  }

  /// <summary>The customer app's address (scheme, host and port, no trailing slash). The app's own address is used when
  /// it is one of the known app addresses, so the payment page can never send a customer to some other site.</summary>
  private async Task<string?> ReturnBaseAsync(string? requested, CancellationToken ct)
  {
    var domain = await _db.Tenants.AsNoTracking()
        .Where(t => t.Id == _tenant.TenantId)
        .Select(t => t.Domain)
        .FirstOrDefaultAsync(ct);

    if (Uri.TryCreate(requested, UriKind.Absolute, out var uri)
        && (uri.Scheme == Uri.UriSchemeHttps || uri.Scheme == Uri.UriSchemeHttp))
    {
      var origin = uri.GetLeftPart(UriPartial.Authority);
      var known = _returnOptions.AllowedOrigins.Any(o => string.Equals(o.TrimEnd('/'), origin, StringComparison.OrdinalIgnoreCase))
          || (domain != null && string.Equals(uri.Host, domain, StringComparison.OrdinalIgnoreCase));
      if (known) return origin;
    }

    if (!string.IsNullOrWhiteSpace(domain)) return $"https://{domain}";
    return string.IsNullOrWhiteSpace(_returnOptions.CustomerAppUrl) ? null : _returnOptions.CustomerAppUrl.TrimEnd('/');
  }

  private static PaymentInvoiceResponse ToInvoice(Payment payment)
      => new(
          payment.Id,
          payment.InvoiceId,
          payment.PaymentUrl,
          payment.QrCode,
          payment.Amount,
          payment.Currency,
          payment.Provider,
          payment.Status,
          payment.CreatedAt,
          payment.ExpiresAt,
          null);

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
          null);
}
