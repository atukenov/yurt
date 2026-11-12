using Yurt.Domain.Common;

namespace Yurt.Domain.Entities;

public class MenuItemLocation : ITenantOwned
{
    public Guid TenantId { get; set; }

    public Guid MenuItemId { get; set; }
    public Guid LocationId { get; set; }
    /// <summary>Price at this branch when it differs from the dish price. Null = the dish price.</summary>
    public decimal? Price { get; set; }
    public MenuItem MenuItem { get; set; } = null!;
    public Location Location { get; set; } = null!;
}
