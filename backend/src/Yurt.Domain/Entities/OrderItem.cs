using Yurt.Domain.Common;

namespace Yurt.Domain.Entities;

public class OrderItem : BaseEntity, ITenantOwned
{
    public Guid TenantId { get; set; }

    public Guid OrderId { get; set; }
    public Guid MenuItemId { get; set; }
    public int Quantity { get; set; }
    public decimal UnitPrice { get; set; }
    public decimal LineTotal { get; set; }
    // What the ingredients of one portion cost (toppings included) when the order was accepted, from the recipes and the
    // ingredients' cost per unit at that moment. Null = not worked out (accepted before this was saved, or no recipe).
    public decimal? UnitCost { get; set; }
    public string MenuItemName { get; set; } = string.Empty; // snapshot
    public Guid? VariantId { get; set; }
    public string? VariantLabel { get; set; }
    public string? Notes { get; set; }

    public Order Order { get; set; } = null!;
    public MenuItem MenuItem { get; set; } = null!;
    public ICollection<OrderItemTopping> Toppings { get; set; } = new List<OrderItemTopping>();
    // What a combo line contained when it was sold. Empty for a plain dish.
    public ICollection<OrderItemComboPart> ComboParts { get; set; } = new List<OrderItemComboPart>();
}
