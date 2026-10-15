using Yurt.Domain.Common;

namespace Yurt.Domain.Entities;

public class MenuTopping : BaseEntity
{
    public string Name { get; set; } = string.Empty;
    public string? NameRu { get; set; }
    public string? NameKk { get; set; }
    public decimal Price { get; set; }
    public bool IsAvailable { get; set; } = true;
    /// <summary>Mutual-exclusion group key (e.g. "milk", "syrup"). Null = free checkbox.</summary>
    public string? Group { get; set; }

    public ICollection<MenuToppingCategory> ToppingCategories { get; set; } = new List<MenuToppingCategory>();
}
