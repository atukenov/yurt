using Yurt.Domain.Common;

namespace Yurt.Domain.Entities;

/// <summary>Something the kitchen uses up (milk, coffee beans, a bun), counted in a unit such as g, ml or pcs (Phase 6, <c>own_pos</c>).</summary>
public class Ingredient : BaseEntity, ITenantOwned
{
    public Guid TenantId { get; set; }

    public string Name { get; set; } = string.Empty;

    /// <summary>Free text shown next to numbers, for example "g", "ml" or "pcs".</summary>
    public string Unit { get; set; } = "pcs";
}

/// <summary>How much of an ingredient a branch has. No row means the branch does not count it (unlimited).</summary>
public class IngredientStock : BaseEntity, ITenantOwned
{
    public Guid TenantId { get; set; }

    public Guid IngredientId { get; set; }
    public Guid LocationId { get; set; }

    public decimal Quantity { get; set; }

    /// <summary>The admin list flags the ingredient as running low at or below this number.</summary>
    public decimal? LowStockAt { get; set; }

    public Ingredient Ingredient { get; set; } = null!;
    public Location Location { get; set; } = null!;
}

/// <summary>How much of an ingredient one portion of a menu item uses. A null variant means every size.</summary>
public class RecipeLine : BaseEntity, ITenantOwned
{
    public Guid TenantId { get; set; }

    public Guid MenuItemId { get; set; }
    public Guid? VariantId { get; set; }
    public Guid IngredientId { get; set; }

    public decimal Quantity { get; set; }

    public MenuItem MenuItem { get; set; } = null!;
    public MenuItemVariant? Variant { get; set; }
    public Ingredient Ingredient { get; set; } = null!;
}

/// <summary>
/// How much of an ingredient one topping uses each time a guest adds it to an item (for example oat milk 200 ml).
/// Applies per item ordered. A topping with no rows is not counted.
/// </summary>
public class ToppingUse : BaseEntity, ITenantOwned
{
    public Guid TenantId { get; set; }

    public Guid ToppingId { get; set; }
    public Guid IngredientId { get; set; }

    public decimal Quantity { get; set; }

    public MenuTopping Topping { get; set; } = null!;
    public Ingredient Ingredient { get; set; } = null!;
}
