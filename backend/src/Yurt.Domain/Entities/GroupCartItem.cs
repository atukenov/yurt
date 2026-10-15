using Yurt.Domain.Common;

namespace Yurt.Domain.Entities;

public class GroupCartItem : BaseEntity
{
    public Guid GroupCartId { get; set; }
    public Guid AddedByUserId { get; set; }
    public string AddedByName { get; set; } = string.Empty;
    public Guid MenuItemId { get; set; }
    public string MenuItemName { get; set; } = string.Empty;
    public decimal UnitPrice { get; set; }
    public int Quantity { get; set; }
    public string ToppingsJson { get; set; } = "[]";
    public string? Notes { get; set; }

    public GroupCart GroupCart { get; set; } = null!;
}
