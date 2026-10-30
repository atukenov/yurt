using Yurt.Domain.Common;

namespace Yurt.Domain.Entities;

/// <summary>
/// How the platform owner charges one cafe every month through the owner's own Stripe account: the price, which modules the
/// price covers, and what Stripe last told us about the cafe's subscription. One row per cafe at most; no row means the
/// cafe is not billed through Stripe (modules are then managed by hand on the Platform screen, as before).
/// </summary>
public class CafeBilling : BaseEntity, ITenantOwned
{
    public Guid TenantId { get; set; }

    /// <summary>Monthly price in <see cref="Currency"/>. Null = no plan set, the cafe cannot start paying yet.</summary>
    public decimal? MonthlyPrice { get; set; }

    /// <summary>Lowercase ISO code Stripe uses: "usd", "eur" or "kzt".</summary>
    public string Currency { get; set; } = "usd";

    /// <summary>Comma-separated module keys a payment extends. Null = every module except core; empty = none.</summary>
    public string? ModuleKeys { get; set; }

    /// <summary>Monthly price of each module as JSON (<c>{"core":20,"marketing":10}</c>); core is the base fee.
    /// Null = no prices set yet.</summary>
    public string? ModulePrices { get; set; }

    /// <summary>True: <see cref="MonthlyPrice"/> was typed by the operator. False: it is the sum of the prices of core
    /// and the switched-on modules, worked out again on every save of the module plan.</summary>
    public bool PriceByHand { get; set; }

    public string? StripeCustomerId { get; set; }
    public string? StripeSubscriptionId { get; set; }

    /// <summary>Stripe's subscription status (active, past_due, unpaid, canceled, incomplete, ...). Null = never started.</summary>
    public string? Status { get; set; }

    /// <summary>The cafe asked to stop at the end of the paid month.</summary>
    public bool CancelAtPeriodEnd { get; set; }

    /// <summary>End of the last month that was paid for (UTC). The covered modules are valid until this time.</summary>
    public DateTime? PaidUntil { get; set; }

    public DateTime? LastPaidAt { get; set; }
    public decimal? LastPaidAmount { get; set; }
    public string? LastInvoiceId { get; set; }
}
