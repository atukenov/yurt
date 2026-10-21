namespace Yurt.Application.Features.Stock;

/// <summary>One menu item of a branch with its count. <c>Quantity</c> is null when the item is not counted.</summary>
public record StockItemDto(
    Guid MenuItemId, string Name, string CategoryName, int? Quantity, int? LowStockAt, bool IsLow, bool IsSoldOut);

/// <summary>A null <c>Quantity</c> stops counting the item (it becomes unlimited again).</summary>
public record SetStockDto(int? Quantity, int? LowStockAt);

/// <summary>An ingredient with its count in one branch. <c>Quantity</c> is null when the branch does not count it.</summary>
public record IngredientDto(
    Guid Id, string Name, string Unit, decimal? Quantity, decimal? LowStockAt, bool IsLow, bool IsOut, int UsedInItems);

public record SaveIngredientDto(string Name, string? Unit);

public record SetIngredientStockDto(decimal? Quantity, decimal? LowStockAt);

/// <summary>One recipe line as shown to staff. A null <c>VariantId</c> means every size.</summary>
public record RecipeLineDto(Guid IngredientId, string IngredientName, string Unit, Guid? VariantId, decimal Quantity);

public record SaveRecipeLineDto(Guid IngredientId, Guid? VariantId, decimal Quantity);

public record SaveRecipeDto(List<SaveRecipeLineDto> Lines);

/// <summary>One thing that is running low or has run out at a branch. `Kind` is "dish" or "ingredient".</summary>
public record StockAlertDto(string Kind, Guid Id, string Name, Guid LocationId, string LocationName, decimal Quantity, string? Unit, bool IsOut);

public record StockAlertsDto(int Low, int Out, List<StockAlertDto> Items);

public record ToppingUseDto(Guid IngredientId, string IngredientName, string Unit, decimal Quantity);

public record SaveToppingUseDto(Guid IngredientId, decimal Quantity);

public record SaveToppingUsesDto(List<SaveToppingUseDto> Lines);
