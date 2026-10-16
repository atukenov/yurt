using Yurt.Domain.Common;

namespace Yurt.Domain.Entities;

public class OrderItemTopping : BaseEntity, ITenantOwned
{
    public Guid TenantId { get; set; }

    public Guid OrderItemId { get; set; }
    public Guid ToppingId { get; set; }
    public string ToppingName { get; set; } = string.Empty; // snapshot
    public decimal Price { get; set; }                       // snapshot

    public OrderItem OrderItem { get; set; } = null!;
}
