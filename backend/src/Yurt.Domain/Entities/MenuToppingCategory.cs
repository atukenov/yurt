namespace Yurt.Domain.Entities;

public class MenuToppingCategory
{
    public Guid ToppingId { get; set; }
    public Guid CategoryId { get; set; }

    public MenuTopping Topping { get; set; } = null!;
    public MenuCategory Category { get; set; } = null!;
}
