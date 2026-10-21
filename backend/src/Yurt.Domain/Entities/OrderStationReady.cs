using Yurt.Domain.Common;

namespace Yurt.Domain.Entities;

/// <summary>
/// One station (Kitchen, Bar, ...) has finished its part of an order (Phase 6, <c>own_pos</c>). When every station
/// that has items in the order is ready, the whole order moves to Ready. No rows = nobody marked a part ready, which is
/// how every order worked before stations had their own status.
/// </summary>
public class OrderStationReady : BaseEntity, ITenantOwned
{
    public Guid TenantId { get; set; }

    public Guid OrderId { get; set; }
    public Order Order { get; set; } = null!;

    public Guid StationId { get; set; }
}
