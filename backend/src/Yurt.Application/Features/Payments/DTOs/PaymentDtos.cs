using Yurt.Domain.Enums;

namespace Yurt.Application.Features.Payments.DTOs;

public enum SandboxPaymentBehavior
{
  Default = 0,
  Success = 1,
  Failure = 2,
  Expired = 3,
  DuplicateWebhook = 4,
  NetworkFailure = 5
}

public record CreatePaymentDto(Guid OrderId, PaymentProvider Provider, SandboxPaymentBehavior SandboxBehavior = SandboxPaymentBehavior.Default);

public record CreatePaymentInvoiceRequest(
    Guid OrderId,
    Guid CustomerId,
    decimal Amount,
    Currency Currency,
    PaymentProvider Provider,
    SandboxPaymentBehavior SandboxBehavior);

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

public record ConfirmApplePayDto(string InvoiceId, string FreedomPaymentId);

public record ApplePayConfigResponse(bool Enabled, string AppleMerchantId);

public record ValidatedPaymentWebhook(
    string InvoiceId,
    string EventType,
    decimal Amount,
    Currency Currency,
    PaymentProvider Provider,
    DateTime Timestamp,
    string RawBody);
