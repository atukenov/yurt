using Yurt.Domain.Enums;

namespace Yurt.Application.Features.Payments.DTOs;

/// <summary>The customer asks to pay an order online. <paramref name="ReturnUrl"/> is the web address of the app the
/// customer came from (for example https://order.mycafe.kz); the payment page sends them back there.</summary>
public record CreatePaymentDto(Guid OrderId, string? ReturnUrl = null);

public record CreatePaymentInvoiceRequest(
    Guid PaymentId,
    Guid OrderId,
    string OrderLabel,
    decimal Amount,
    Currency Currency,
    string SuccessUrl,
    string CancelUrl,
    string CafeSlug);

public record PaymentInvoiceResponse(
    Guid PaymentId,
    string InvoiceId,
    string PaymentUrl,
    string QrCode,
    decimal Amount,
    Currency Currency,
    PaymentProvider Provider,
    PaymentRecordStatus Status,
    DateTime CreatedAt,
    DateTime? ExpiresAt,
    string? RawResponse);

public record PaymentStatusResponse(
    string InvoiceId,
    PaymentRecordStatus Status,
    Guid OrderId,
    string OrderStatus,
    decimal Amount,
    Currency Currency,
    PaymentProvider Provider,
    string PaymentUrl,
    string QrCode,
    DateTime CreatedAt,
    DateTime? UpdatedAt,
    string? RawResponse);

/// <summary>What the customer app needs to know: whether this cafe takes online payments, and through whom.</summary>
public record PaymentConfigResponse(string? Provider);

public enum PaymentWebhookKind { Paid, Failed, Expired, Refunded }

/// <summary>A checked webhook. The payment is found by <paramref name="InvoiceId"/> (the payment page id) or, for
/// events about the money itself such as a refund, by <paramref name="ProviderPaymentId"/>.</summary>
public record PaymentWebhookEvent(
    string EventId,
    PaymentWebhookKind Kind,
    string? InvoiceId,
    string? ProviderPaymentId);

/// <summary>Platform screen: the cafe's online payment provider and whether its keys are in Secrets.</summary>
public record PaymentSettingsDto(
    string? Provider,
    List<string> Providers,
    bool SecretKeySet,
    bool WebhookSecretSet,
    bool TestMode,
    string WebhookUrl);

public record SavePaymentSettingsDto(string? Provider);
