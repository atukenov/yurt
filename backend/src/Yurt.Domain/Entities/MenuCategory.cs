using Yurt.Domain.Common;

namespace Yurt.Domain.Entities;

public class MenuCategory : BaseEntity
{
    public string Name { get; set; } = string.Empty;
    public string? NameRu { get; set; }
    public string? NameKk { get; set; }
    public int SortOrder { get; set; }

    public ICollection<MenuItem> Items { get; set; } = new List<MenuItem>();
    public ICollection<MenuToppingCategory> ToppingLinks { get; set; } = new List<MenuToppingCategory>();
}
