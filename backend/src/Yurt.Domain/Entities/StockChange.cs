using Yurt.Domain.Common;

namespace Yurt.Domain.Entities;

/// <summary>
/// One hand change to a stock count at a branch (Phase 6, <c>own_pos</c>): a restock, a recount or a stop-counting.
/// Kept with the item's name so the history still reads after the item is deleted. Orders lowering counts are not logged here.
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
}
