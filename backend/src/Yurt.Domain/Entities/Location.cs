using Yurt.Domain.Common;

namespace Yurt.Domain.Entities;

public class Location : BaseEntity, ITenantOwned
{
    public Guid TenantId { get; set; }

    public string Name { get; set; } = string.Empty;
    public string Address { get; set; } = string.Empty;
    public string WorkingHours { get; set; } = string.Empty;
    public string ContactPhone { get; set; } = string.Empty;
    public bool IsActive { get; set; } = true;

    // Link to this location in 2GIS (https://2gis.kz/… or https://go.2gis.com/…). Optional.
    public string? TwoGisUrl { get; set; }

    // iiko terminal group this location's orders are pushed to. Null = don't push
    // orders for this location (fail closed, same principle as an unmapped menu item).
    public Guid? IikoTerminalGroupId { get; set; }

    // Payment methods this branch offers (Phase 6, own_pos), as comma-separated PaymentMethod names.
    // Null/empty = every method (the default, and what a branch gets while own_pos is off).
    public string? PaymentMethods { get; set; }

    public ICollection<Order> Orders { get; set; } = new List<Order>();
}
