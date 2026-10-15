using Yurt.Domain.Common;

namespace Yurt.Domain.Entities;

public class MenuItem : BaseEntity
{
    public Guid CategoryId { get; set; }
    public string Name { get; set; } = string.Empty;
    public string? NameRu { get; set; }
    public string? NameKk { get; set; }
    public string Description { get; set; } = string.Empty;
    public string? DescriptionRu { get; set; }
    public string? DescriptionKk { get; set; }
    public decimal Price { get; set; }
    public bool IsAvailable { get; set; } = true;
    public bool IsNew { get; set; }
    public string? ImageUrl { get; set; }
    public int SortOrder { get; set; }

    // iiko nomenclature mapping (order-push feature). Null = unmapped; orders containing
    // this item fail closed (skip the iiko push) rather than guessing. ProductSizeId is
    // used only when the item has no Variants of its own.
    public Guid? IikoProductId { get; set; }
    public Guid? IikoProductSizeId { get; set; }

    public MenuCategory Category { get; set; } = null!;
    public ICollection<OrderItem> OrderItems { get; set; } = new List<OrderItem>();
    public ICollection<Favorite> Favorites { get; set; } = new List<Favorite>();
    public ICollection<MenuItemLocation> MenuItemLocations { get; set; } = new List<MenuItemLocation>();
    public ICollection<MenuItemVariant> Variants { get; set; } = new List<MenuItemVariant>();
}
