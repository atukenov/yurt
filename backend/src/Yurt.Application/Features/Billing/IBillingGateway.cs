using Yurt.Application.Common.Models;

namespace Yurt.Application.Features.Billing;

/// <summary>
/// The platform owner's own Stripe account, used to charge cafes a monthly fee (not the cafes' Stripe accounts, which
/// take their customers' order payments). Keys come from server settings Billing:Stripe:SecretKey and
/// Billing:Stripe:WebhookSecret.
/// </summary>
public interface IBillingGateway
{
    /// <summary>True when both platform Stripe keys are set (and the key is a test key, unless live keys are allowed).</summary>
    bool IsConfigured { get; }

    /// <summary>True unless a live key (sk_live_...) is set: no real money moves in test mode.</summary>
    bool TestMode { get; }

    /// <summary>Opens Stripe's page where the cafe enters a card and starts the monthly subscription. Returns its address.</summary>
    Task<string> CreateCheckoutAsync(BillingCheckoutRequest request, CancellationToken ct = default);

    /// <summary>Opens Stripe's billing page where the cafe changes its card, sees invoices or cancels. Returns its address.</summary>
    Task<string> CreatePortalAsync(string customerId, string returnUrl, CancellationToken ct = default);

    /// <summary>Checks the webhook signature with the platform secret and reads what happened.
    /// A success with a null event means "valid, but nothing we act on".</summary>
    Result<BillingEvent?> ParseWebhook(string rawBody, IReadOnlyDictionary<string, string> headers);
}

public record BillingCheckoutRequest(
    Guid TenantId,
    string CafeSlug,
    string CafeName,
    decimal MonthlyPrice,
    string Currency,
    string? CustomerId,
    string SuccessUrl,
    string CancelUrl);

public enum BillingEventKind
{
    /// <summary>The cafe finished Stripe's checkout page; tells us its customer and subscription ids.</summary>
    CheckoutCompleted,
    /// <summary>A monthly invoice was paid: extend the cafe's modules to the end of the paid month.</summary>
    InvoicePaid,
    /// <summary>Charging the card failed; Stripe retries on its own schedule.</summary>
    InvoicePaymentFailed,
    /// <summary>The subscription was created, changed or ended (status, cancel at period end).</summary>
    SubscriptionChanged,
}

/// <summary>What one Stripe webhook means for a cafe's billing. <see cref="TenantId"/> comes from the metadata Yurt put
/// on the subscription; the ids let us find the cafe when the metadata is missing.</summary>
public record BillingEvent(
    string EventId,
    BillingEventKind Kind,
    Guid? TenantId,
    string? SubscriptionId,
    string? CustomerId,
    string? Status = null,
    bool CancelAtPeriodEnd = false,
    DateTime? PeriodEnd = null,
    string? InvoiceId = null,
    decimal? Amount = null);

/// <summary>Thrown when Stripe refuses a billing call; the message is safe to show to the cafe owner.</summary>
public class BillingGatewayException(string message) : Exception(message);
