using Yurt.Domain.Common;

namespace Yurt.Domain.Entities;

public class MenuToppingCategory : ITenantOwned
{
    public Guid TenantId { get; set; }

    public Guid ToppingId { get; set; }
    public Guid CategoryId { get; set; }

    public MenuTopping Topping { get; set; } = null!;
    public MenuCategory Category { get; set; } = null!;
}
