using Yurt.Domain.Common;

namespace Yurt.Domain.Entities;

/// <summary>
/// How many of a menu item are left at one branch (Phase 6, <c>own_pos</c>). No row means the item is not counted
/// (unlimited). At zero the item drops off the customer menu for that branch by itself.
/// </summary>
public class MenuItemStock : BaseEntity, ITenantOwned
{
    public Guid TenantId { get; set; }

    public Guid MenuItemId { get; set; }
    public Guid LocationId { get; set; }

    /// <summary>Portions left. Accepted orders lower it; it never goes below zero.</summary>
    public int Quantity { get; set; }

    /// <summary>The admin list flags the item as running low at or below this number. Null means no warning.</summary>
    public int? LowStockAt { get; set; }

    public MenuItem MenuItem { get; set; } = null!;
    public Location Location { get; set; } = null!;
}
