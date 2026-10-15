using Yurt.Application.Common.Interfaces;
using Yurt.Application.Features.Payments;
using Yurt.Application.Features.Payments.DTOs;
using Yurt.Domain.Enums;

namespace Yurt.Infrastructure.Payments;

/// <summary>
/// Apple Pay via Freedom Pay (issue #49). Unlike <see cref="KaspiSandboxPaymentProvider"/>'s
/// redirect/QR flow, Apple Pay is authorized natively on-device — PassKit presents the sheet
/// and the Freedom Pay iOS SDK (`createApplePayment`/`confirmApplePayment`) runs entirely in
/// the native Capacitor plugin. This provider's only job is to create the local
/// <c>Payment</c> row checkout already expects; the actual charge happens client-side, and
/// <see cref="Yurt.Application.Features.Payments.Services.PaymentService.ConfirmApplePayAsync"/>
/// marks it Paid afterward once the native flow succeeds.
/// </summary>
public class FreedomPayPaymentProvider : IPaymentProvider
{
    private readonly FreedomPayOptions _options;

    public PaymentProvider Provider => PaymentProvider.FreedomPay;

    public FreedomPayPaymentProvider(FreedomPayOptions options)
    {
        _options = options;
    }

    public Task<PaymentInvoiceResponse> CreateInvoiceAsync(
        CreatePaymentInvoiceRequest request, CancellationToken cancellationToken = default)
    {
        if (!_options.Enabled)
            throw new InvalidOperationException("Apple Pay (Freedom Pay) is not enabled.");

        var invoiceId = $"FP-{Guid.NewGuid():N}";
        var now = DateTime.UtcNow;

        return Task.FromResult(new PaymentInvoiceResponse(
            Guid.Empty,
            invoiceId,
            PaymentUrl: string.Empty,
            QrCode: string.Empty,
            request.Amount,
            request.Currency,
            Provider,
            PaymentRecordStatus.Pending,
            now,
            ExpiresAt: now.AddMinutes(10),
            RawResponse: null));
    }

    public Task<PaymentStatusResponse> GetPaymentStatusAsync(
        string invoiceId, CancellationToken cancellationToken = default)
    {
        // Nothing calls IPaymentProvider.GetPaymentStatusAsync today — PaymentService's own
        // GetPaymentStatusAsync reads the Payment row straight from the database for every
        // provider. Throwing rather than faking a response keeps that assumption honest if
        // it ever changes.
        throw new NotSupportedException(
            "FreedomPayPaymentProvider has no separate status to poll; read Payment status from the database instead.");
    }
}
