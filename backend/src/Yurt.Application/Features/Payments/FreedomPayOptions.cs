namespace Yurt.Application.Features.Payments;

/// <summary>
/// Configuration for Apple Pay via the Freedom Pay iOS Merchant SDK ("FreedomPay" section).
/// The Freedom Pay SDK itself runs entirely client-side (native Swift plugin, configured with
/// Freedom Pay's own merchant ID/secret key/region — see issue #49) — the backend never talks
/// to Freedom Pay directly in this version, so those credentials live in the iOS app's native
/// config, not here. This options class only holds what the backend needs: a kill switch, and
/// the Apple Merchant ID the app fetches at runtime so it isn't hardcoded into a build.
/// </summary>
public class FreedomPayOptions
{
    /// <summary>Master feature flag. When false, the apple-pay-confirm endpoint rejects
    /// confirmations and the customer app should hide the Apple Pay button — lets ops kill
    /// the feature instantly without an app release (see issue #3's "hardening" notes).</summary>
    public bool Enabled { get; set; } = false;

    /// <summary>Apple Merchant ID registered in the Apple Developer portal
    /// (e.g. "merchant.com.yurt.app"), used by the app to build the native `PKPaymentRequest`.
    /// Not a secret — safe to serve from a config endpoint.</summary>
    public string AppleMerchantId { get; set; } = string.Empty;
}
