namespace Yurt.Domain.Entities;

public class MenuItemLocation
{
    public Guid MenuItemId { get; set; }
    public Guid LocationId { get; set; }
    public MenuItem MenuItem { get; set; } = null!;
    public Location Location { get; set; } = null!;
}
