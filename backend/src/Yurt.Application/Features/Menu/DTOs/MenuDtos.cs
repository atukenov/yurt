namespace Yurt.Application.Features.Menu.DTOs;

// ── Customer-facing DTOs (names already localized by service) ────────────────

public record MenuCategoryDto(Guid Id, string Name, int SortOrder);

public record MenuItemVariantDto(Guid Id, string Label, decimal Price, int SortOrder, bool IsDefault);

public record MenuItemDto(
    Guid Id,
    Guid CategoryId,
    string CategoryName,
    string Name,
    string Description,
    decimal Price,
    bool IsAvailable,
    bool IsNew,
    string? ImageUrl,
    DateTime CreatedAt = default,
    List<Guid>? LocationIds = null,
    List<MenuToppingDto>? AvailableToppings = null,
    List<MenuItemVariantDto>? Variants = null,
    // Names in every language so the client can search across languages.
    string? NameEn = null,
    string? NameRu = null,
    string? NameKk = null);

public record MenuToppingDto(Guid Id, string Name, decimal Price, bool IsAvailable, List<Guid> CategoryIds, string? Group = null);

// ── Admin-facing read DTOs (all language fields exposed) ─────────────────────

public record AdminMenuCategoryDto(Guid Id, string Name, string? NameRu, string? NameKk, int SortOrder);

public record AdminMenuItemVariantDto(Guid Id, string Label, string? LabelRu, string? LabelKk, decimal Price, int SortOrder, bool IsDefault, Guid? IikoProductSizeId = null);
public record CreateMenuItemVariantDto(string Label, string? LabelRu, string? LabelKk, decimal Price, int SortOrder, bool IsDefault, Guid? IikoProductSizeId = null);

public record AdminMenuItemDto(
    Guid Id,
    Guid CategoryId,
    string CategoryName,
    string Name,
    string? NameRu,
    string? NameKk,
    string Description,
    string? DescriptionRu,
    string? DescriptionKk,
    decimal Price,
    bool IsAvailable,
    bool IsNew,
    string? ImageUrl,
    int SortOrder = 0,
    List<Guid>? LocationIds = null,
    List<MenuToppingDto>? AvailableToppings = null,
    List<AdminMenuItemVariantDto>? Variants = null,
    Guid? IikoProductId = null,
    Guid? IikoProductSizeId = null);

public record AdminMenuToppingDto(
    Guid Id,
    string Name,
    string? NameRu,
    string? NameKk,
    decimal Price,
    bool IsAvailable,
    List<Guid> CategoryIds,
    string? Group = null);

// ── Admin write DTOs ──────────────────────────────────────────────────────────

public record CreateCategoryDto(string Name, string? NameRu, string? NameKk, int SortOrder);
public record UpdateCategoryDto(string Name, string? NameRu, string? NameKk, int SortOrder);
public record ReorderCategoriesDto(List<Guid> OrderedIds);
public record ReorderMenuItemsDto(Guid CategoryId, List<Guid> OrderedIds);

public record CreateMenuItemDto(
    Guid CategoryId,
    string Name,
    string? NameRu,
    string? NameKk,
    string? Description,
    string? DescriptionRu,
    string? DescriptionKk,
    decimal Price,
    bool IsAvailable,
    string? ImageUrl,
    List<Guid>? LocationIds = null,
    List<CreateMenuItemVariantDto>? Variants = null,
    Guid? IikoProductId = null,
    Guid? IikoProductSizeId = null,
    bool IsNew = false);

public record UpdateMenuItemDto(
    Guid CategoryId,
    string Name,
    string? NameRu,
    string? NameKk,
    string? Description,
    string? DescriptionRu,
    string? DescriptionKk,
    decimal Price,
    bool IsAvailable,
    string? ImageUrl,
    List<Guid>? LocationIds = null,
    List<CreateMenuItemVariantDto>? Variants = null,
    Guid? IikoProductId = null,
    Guid? IikoProductSizeId = null,
    bool IsNew = false);

public record CreateToppingDto
{
    public string Name { get; init; } = "";
    public string? NameRu { get; init; }
    public string? NameKk { get; init; }
    public decimal Price { get; init; }
    public bool IsAvailable { get; init; }
    public List<Guid> CategoryIds { get; init; } = [];
    public string? Group { get; init; }
}

public record UpdateToppingDto
{
    public string Name { get; init; } = "";
    public string? NameRu { get; init; }
    public string? NameKk { get; init; }
    public decimal Price { get; init; }
    public bool IsAvailable { get; init; }
    public List<Guid> CategoryIds { get; init; } = [];
    public string? Group { get; init; }
}
