using Yurt.Domain.Common;

namespace Yurt.Domain.Entities;

public class MenuItemVariant : BaseEntity
{
    public Guid MenuItemId { get; set; }
    public string Label { get; set; } = string.Empty;
    public string? LabelRu { get; set; }
    public string? LabelKk { get; set; }
    public decimal Price { get; set; }
    public int SortOrder { get; set; }
    public bool IsDefault { get; set; }

    // iiko size mapping (order-push feature) — used when the parent MenuItem has size variants.
    public Guid? IikoProductSizeId { get; set; }

    public MenuItem MenuItem { get; set; } = null!;
}
