using Yurt.Domain.Common;

namespace Yurt.Domain.Entities;

/// <summary>
/// A place where part of an order is made, such as Kitchen or Bar (Phase 6, <c>own_pos</c>). Menu categories are routed
/// to a station; the kitchen screen can then show each station only its own items.
/// </summary>
public class Station : BaseEntity, ITenantOwned
{
    public Guid TenantId { get; set; }

    public string Name { get; set; } = string.Empty;
    public int SortOrder { get; set; }
}
