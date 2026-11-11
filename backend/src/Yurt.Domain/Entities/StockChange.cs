using Yurt.Domain.Common;

namespace Yurt.Domain.Entities;

/// <summary>
/// One hand change to a stock count at a branch (Phase 6, <c>own_pos</c>): a count set by hand, a restock, a write-off,
/// a stocktake, a move between branches, or an order that needed more than was left. Kept with the item's name so the
/// history still reads after the item is deleted. Orders lowering counts normally are not logged here.
/// </summary>
public class StockChange : BaseEntity, ITenantOwned
{
    public Guid TenantId { get; set; }

    public Guid LocationId { get; set; }
    public Location Location { get; set; } = null!;

    /// <summary>"dish" or "ingredient".</summary>
    public string Kind { get; set; } = "dish";

    /// <summary>The menu item or ingredient id. No foreign key, so deleting the item keeps its history.</summary>
    public Guid ItemId { get; set; }
    public string ItemName { get; set; } = string.Empty;
    public string? Unit { get; set; }

    /// <summary>Null means the item was not counted (before) or stopped being counted (after).</summary>
    public decimal? OldQuantity { get; set; }
    public decimal? NewQuantity { get; set; }

    public string ChangedBy { get; set; } = string.Empty;

    /// <summary>What kind of change this was; see <see cref="StockChangeTypes"/>.</summary>
    public string Type { get; set; } = StockChangeTypes.Set;

    /// <summary>Why stock was written off (see <see cref="WriteOffReasons"/>). Only for write-offs.</summary>
    public string? Reason { get; set; }

    public string? Note { get; set; }

    /// <summary>What one unit cost at the time, so the money value of a write-off or stocktake difference stays fixed.</summary>
    public decimal? UnitCost { get; set; }

    /// <summary>For moves between branches: the other branch (kept by name too, in case it is deleted).</summary>
    public Guid? OtherLocationId { get; set; }
    public string? OtherLocationName { get; set; }

    /// <summary>The purchase invoice this delivery (or its cancelling) belongs to.</summary>
    public Guid? PurchaseId { get; set; }
}

public static class StockChangeTypes
{
    public const string Set = "set";
    public const string Restock = "restock";
    public const string WriteOff = "writeoff";
    public const string Stocktake = "stocktake";
    public const string TransferOut = "transfer_out";
    public const string TransferIn = "transfer_in";
    /// <summary>An accepted order needed more than the branch had left; the count stopped at zero.</summary>
    public const string Shortfall = "shortfall";
    /// <summary>A posted purchase invoice was cancelled: its amounts were taken back off the shelf.</summary>
    public const string RestockCancel = "restock_cancel";
}

public static class WriteOffReasons
{
    public const string Spilled = "spilled";
    public const string Expired = "expired";
    public const string StaffDrink = "staff";
    public const string Broken = "broken";
    public const string Other = "other";

    public static readonly IReadOnlyList<string> All = [Spilled, Expired, StaffDrink, Broken, Other];
}
