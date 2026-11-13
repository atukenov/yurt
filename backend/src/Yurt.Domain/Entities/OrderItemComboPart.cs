using Yurt.Domain.Common;

namespace Yurt.Domain.Entities;

/// <summary>
/// What one sold combo contained, saved when it was sold so later edits to the combo do not change the past: stock is
/// taken off by it, the kitchen ticket lists it, and the combo report compares the combo price with the separate prices.
/// </summary>
public class OrderItemComboPart : BaseEntity, ITenantOwned
{
    public Guid TenantId { get; set; }

    public Guid OrderItemId { get; set; }
    public Guid ItemId { get; set; }
    public string ItemName { get; set; } = string.Empty;   // snapshot
    public Guid? VariantId { get; set; }
    public string? VariantLabel { get; set; }
    public int Quantity { get; set; }                        // per one combo
    // The price of one piece bought separately at that branch when the combo was sold.
    public decimal RegularPrice { get; set; }

    public OrderItem OrderItem { get; set; } = null!;
}
