namespace Yurt.Application.Features.Loyalty;

/// <summary>
/// Configuration for the iikoCloud loyalty integration ("Iiko" section).
/// Secrets (ApiKey, ClientSecret) must come from environment variables, never from committed config.
/// </summary>
public class IikoOptions
{
    /// <summary>Master feature flag. When false no iiko calls are made anywhere.</summary>
    public bool Enabled { get; set; } = false;

    public string BaseUrl { get; set; } = "https://api-ru.iiko.services";

    /// <summary>API key generated in iikoWeb (Integrations → API Keys). Used by /api/v2/access_token.</summary>
    public string ApiKey { get; set; } = string.Empty;

    /// <summary>Application id issued by the iiko Developer Portal. Used by /api/v2/access_token.</summary>
    public string AppId { get; set; } = string.Empty;

    /// <summary>Application secret issued by the iiko Developer Portal. Used by /api/v2/access_token.</summary>
    public string ClientSecret { get; set; } = string.Empty;

    public Guid OrganizationId { get; set; }

    /// <summary>Loyalty program customers are enrolled into.</summary>
    public Guid ProgramId { get; set; }

    /// <summary>Percent of the completed order total credited as points (e.g. 5 = 5%).</summary>
    public decimal EarnPercent { get; set; } = 10m;

    /// <summary>Session token lifetime safety margin — tokens live ~1h; refresh after this many minutes.</summary>
    public int TokenLifetimeMinutes { get; set; } = 50;

    /// <summary>
    /// Secondary feature flag for pushing accepted app orders into iiko as real delivery
    /// orders (sales/inventory reporting + kitchen routing side-channel; the wallet
    /// earn/spend flow above is unaffected either way). Independent of <see cref="Enabled"/>
    /// so order-push can be piloted separately from loyalty.
    /// </summary>
    public bool PushOrdersEnabled { get; set; } = false;

    /// <summary>
    /// iiko payment type registered as "paid in app" (isProcessedExternally: true), so iiko
    /// doesn't attempt to collect payment again. Look up available IDs via the admin
    /// iiko/payment-types reference endpoint. Global — shared by all locations.
    /// </summary>
    public Guid PaymentTypeId { get; set; }

    /// <summary>Shared secret registered with iiko's webhooks/update_settings; checked on incoming webhook calls.</summary>
    public string WebhookAuthToken { get; set; } = string.Empty;

    /// <summary>How far back the customer-facing bonus history view looks (on-demand fetch, no background sync).</summary>
    public int TransactionHistoryDays { get; set; } = 90;
}
