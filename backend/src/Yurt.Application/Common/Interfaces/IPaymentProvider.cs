using Yurt.Application.Common.Models;
using Yurt.Application.Features.Payments.DTOs;
using Yurt.Domain.Entities;
using Yurt.Domain.Enums;

namespace Yurt.Application.Common.Interfaces;

/// <summary>
/// One online payment provider (Stripe today). Every call uses the current cafe's own account, read from its
/// encrypted secrets, so money always goes to the cafe that owns the order.
/// </summary>
public interface IPaymentProvider
{
    PaymentProvider Provider { get; }

    /// <summary>True when the current cafe has the keys this provider needs.</summary>
    Task<bool> IsConfiguredAsync(CancellationToken cancellationToken = default);

    /// <summary>Opens a payment page for the order. The customer is sent to <see cref="PaymentInvoiceResponse.PaymentUrl"/>.</summary>
    Task<PaymentInvoiceResponse> CreateInvoiceAsync(CreatePaymentInvoiceRequest request, CancellationToken cancellationToken = default);

    /// <summary>Closes a payment page that was not paid yet, so the customer can no longer pay for it.</summary>
    Task CancelInvoiceAsync(Payment payment, CancellationToken cancellationToken = default);

    /// <summary>Gives the whole amount of a paid payment back to the customer's card.</summary>
    Task<Result> RefundAsync(Payment payment, CancellationToken cancellationToken = default);

    /// <summary>Checks the signature of a webhook with the current cafe's secret and reads what happened.
    /// A success with a null event means "valid, but nothing we act on".</summary>
    Task<Result<PaymentWebhookEvent?>> ParseWebhookAsync(
        string rawBody, IReadOnlyDictionary<string, string> headers, CancellationToken cancellationToken = default);
}

/// <summary>Thrown when the provider refuses a call; the message is safe to show to staff.</summary>
public class PaymentProviderException(string message) : Exception(message);
