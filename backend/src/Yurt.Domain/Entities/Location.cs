using Yurt.Domain.Common;

namespace Yurt.Domain.Entities;

public class Location : BaseEntity
{
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

    public ICollection<Order> Orders { get; set; } = new List<Order>();
}
