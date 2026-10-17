using Yurt.Domain.Common;

namespace Yurt.Domain.Entities;

/// <summary>
/// A customer's bonus wallet in our own database (Phase 6, <c>own_pos</c>). One account per phone number per cafe.
/// The balance is never stored: it is the sum of the account's <see cref="LoyaltyEntry"/> rows.
/// </summary>
public class LoyaltyAccount : BaseEntity, ITenantOwned
{
    public Guid TenantId { get; set; }
    public string Phone { get; set; } = string.Empty;
    public string? Name { get; set; }
    public string? Surname { get; set; }

    /// <summary>Set when the account joined the loyalty program; null until then.</summary>
    public Guid? WalletId { get; set; }

    public List<LoyaltyEntry> Entries { get; set; } = [];
}

public enum LoyaltyEntryKind
{
    Topup = 0,
    Chargeoff = 1,
    Hold = 2,
    HoldRelease = 3,
}

/// <summary>One movement on a bonus wallet. <see cref="Amount"/> is signed: positive adds points, negative takes them.</summary>
public class LoyaltyEntry : BaseEntity, ITenantOwned
{
    public Guid TenantId { get; set; }
    public Guid AccountId { get; set; }
    public LoyaltyAccount Account { get; set; } = null!;
    public LoyaltyEntryKind Kind { get; set; }
    public decimal Amount { get; set; }
    public string? Comment { get; set; }

    /// <summary>Only for <see cref="LoyaltyEntryKind.Hold"/>: set once the hold has been released, so it is released at most once.</summary>
    public bool Released { get; set; }
}
