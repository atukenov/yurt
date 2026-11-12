using Yurt.Domain.Common;

namespace Yurt.Domain.Entities;

/// <summary>
/// Options picked by hand for one dish. A dish with at least one row offers only these options;
/// a dish with none offers the options of its category.
/// </summary>
public class MenuItemTopping : ITenantOwned
{
    public Guid TenantId { get; set; }

    public Guid MenuItemId { get; set; }
    public Guid ToppingId { get; set; }

    public MenuItem MenuItem { get; set; } = null!;
    public MenuTopping Topping { get; set; } = null!;
}
