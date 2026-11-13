namespace Yurt.Application.Features.Menu.DTOs;

// ── Customer-facing DTOs (names already localized by service) ────────────────

public record MenuCategoryDto(Guid Id, string Name, int SortOrder);

public record MenuItemVariantDto(Guid Id, string Label, decimal Price, int SortOrder, bool IsDefault, decimal? RegularPrice = null);

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
    string? NameKk = null,
    // What the guest has to choose for this dish (only groups the dish offers).
    List<OptionRuleDto>? OptionRules = null,
    // The usual price while a time price (happy hour...) is running; null otherwise.
    decimal? RegularPrice = null,
    // A combo: a set price for the dishes listed in ComboItems.
    bool IsCombo = false,
    List<ComboItemDto>? ComboItems = null,
    // The number on the pack, scanned at the till; and a dish sold by weight (Price is then per kg, till only).
    string? Barcode = null,
    bool SoldByWeight = false,
    // Allergens and nutrition per serving; null when nothing is filled in.
    NutritionDto? Nutrition = null);

/// <summary>Allergen codes (see Nutrition.AllergenCodes) and nutrition per serving. Every number is optional.</summary>
public record NutritionDto(int? Calories, decimal? Protein, decimal? Fat, decimal? Carbs, int? WeightGrams, List<string>? Allergens);

/// <summary>A dish inside a combo, for the guest: name, size (if one is set) and how many.</summary>
public record ComboItemDto(Guid ItemId, string Name, string? VariantLabel, int Quantity);

/// <summary>How many options of one group the guest must / may choose. Max null = no limit.</summary>
public record OptionRuleDto(string Group, string Title, bool Required, int Min, int? Max);

/// <summary>An option's price at one branch.</summary>
public record LocationPriceDto(Guid LocationId, decimal Price);

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
    Guid? IikoProductSizeId = null,
    // Options picked by hand for this dish (empty = the category's options) and prices that differ by branch.
    List<Guid>? ToppingIds = null,
    List<LocationPriceDto>? LocationPrices = null,
    string? Barcode = null,
    bool SoldByWeight = false,
    NutritionDto? Nutrition = null);

public record AdminMenuToppingDto(
    Guid Id,
    string Name,
    string? NameRu,
    string? NameKk,
    decimal Price,
    bool IsAvailable,
    List<Guid> CategoryIds,
    string? Group = null,
    List<LocationPriceDto>? LocationPrices = null);

public record AdminOptionGroupDto(
    string Key, string? Title, string? TitleRu, string? TitleKk, bool Required, int MinSelect, int? MaxSelect);

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
    bool IsNew = false,
    List<Guid>? ToppingIds = null,
    List<LocationPriceDto>? LocationPrices = null,
    string? Barcode = null,
    bool SoldByWeight = false,
    NutritionDto? Nutrition = null);

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
    bool IsNew = false,
    List<Guid>? ToppingIds = null,
    List<LocationPriceDto>? LocationPrices = null,
    string? Barcode = null,
    bool SoldByWeight = false,
    NutritionDto? Nutrition = null);

public record CreateToppingDto
{
    public string Name { get; init; } = "";
    public string? NameRu { get; init; }
    public string? NameKk { get; init; }
    public decimal Price { get; init; }
    public bool IsAvailable { get; init; }
    public List<Guid> CategoryIds { get; init; } = [];
    public string? Group { get; init; }
    /// <summary>Null = leave the branch prices as they are.</summary>
    public List<LocationPriceDto>? LocationPrices { get; init; }
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
    /// <summary>Null = leave the branch prices as they are.</summary>
    public List<LocationPriceDto>? LocationPrices { get; init; }
}

// ── Time menus (breakfast, happy hour...) ────────────────────────────────────

/// <summary>A price that applies while the schedule runs: for one dish or a whole category. One of Price / PercentOff.</summary>
public record TimePriceDto(Guid? MenuItemId, Guid? CategoryId, decimal? Price, decimal? PercentOff);

/// <summary>
/// A time window with what it controls. <c>Days</c> is a bit mask, Monday = 1 ... Sunday = 64. <c>Start</c> / <c>End</c> are
/// "HH:mm" on the branch's clock ("24:00" = end of day; an end at or before the start runs past midnight).
/// <c>CategoryIds</c> / <c>ItemIds</c> are on the menu only inside the window; <c>Prices</c> apply only inside it.
/// </summary>
public record AdminMenuScheduleDto(
    Guid Id, string Name, int Days, string Start, string End, Guid? LocationId, bool IsActive,
    List<Guid> CategoryIds, List<Guid> ItemIds, List<TimePriceDto> Prices, bool RunningNow = false);

public record SaveMenuScheduleDto
{
    public string Name { get; init; } = string.Empty;
    public int Days { get; init; } = 127;
    public string Start { get; init; } = "00:00";
    public string End { get; init; } = "24:00";
    public Guid? LocationId { get; init; }
    public bool IsActive { get; init; } = true;
    public List<Guid>? CategoryIds { get; init; }
    public List<Guid>? ItemIds { get; init; }
    public List<TimePriceDto>? Prices { get; init; }
}

/// <summary>When a time menu next opens or closes at a branch (UTC); null when there are none. Apps reload the menu then.</summary>
public record MenuNextChangeDto(DateTime? At);
