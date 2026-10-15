using Yurt.Domain.Common;

namespace Yurt.Domain.Entities;

public class OrderItem : BaseEntity
{
    public Guid OrderId { get; set; }
    public Guid MenuItemId { get; set; }
    public int Quantity { get; set; }
    public decimal UnitPrice { get; set; }
    public decimal LineTotal { get; set; }
    public string MenuItemName { get; set; } = string.Empty; // snapshot
    public Guid? VariantId { get; set; }
    public string? VariantLabel { get; set; }
    public string? Notes { get; set; }

    public Order Order { get; set; } = null!;
    public MenuItem MenuItem { get; set; } = null!;
    public ICollection<OrderItemTopping> Toppings { get; set; } = new List<OrderItemTopping>();
}
