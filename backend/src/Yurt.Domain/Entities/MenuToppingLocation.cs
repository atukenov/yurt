using Yurt.Domain.Common;

namespace Yurt.Domain.Entities;

/// <summary>The price of an option at one branch, when it differs from <see cref="MenuTopping.Price"/>.</summary>
public class MenuToppingLocation : ITenantOwned
{
    public Guid TenantId { get; set; }

    public Guid ToppingId { get; set; }
    public Guid LocationId { get; set; }
    public decimal Price { get; set; }

    public MenuTopping Topping { get; set; } = null!;
    public Location Location { get; set; } = null!;
}
