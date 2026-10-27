using Yurt.Domain.Enums;

namespace Yurt.Application.Features.Payments;

/// <summary>Who can take a cafe's online payments (<c>Tenant.PaymentProvider</c>). A local Kazakh provider can be added
/// here later next to Stripe; each cafe picks one on the Platform screen.</summary>
public static class PaymentProviders
{
    public const string Stripe = "stripe";

    public static readonly string[] All = [Stripe];

    /// <summary>Secret names of the cafe's Stripe account (stored encrypted, see <c>TenantSecretKeys</c>).</summary>
    public const string StripeSecretKey = "Stripe:SecretKey";
    public const string StripeWebhookSecret = "Stripe:WebhookSecret";

    public static PaymentProvider? ToEnum(string? name) => name switch
    {
        Stripe => PaymentProvider.Stripe,
        _ => null,
    };
}
