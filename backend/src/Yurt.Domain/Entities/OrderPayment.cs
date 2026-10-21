using Yurt.Domain.Common;
using Yurt.Domain.Enums;

namespace Yurt.Domain.Entities;

/// <summary>
/// One part of a split payment (Phase 6, <c>own_pos</c>): the cashier records that a guest paid part of an order with
/// one method and the rest with another. An order with no rows here was paid with its single <c>PaymentMethod</c>.
/// </summary>
public class OrderPayment : BaseEntity, ITenantOwned
{
    public Guid TenantId { get; set; }

    public Guid OrderId { get; set; }
    public Order Order { get; set; } = null!;

    public PaymentMethod Method { get; set; }
    public decimal Amount { get; set; }
}
