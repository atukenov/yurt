namespace Yurt.Application.Features.Stock;

/// <summary>One menu item of a branch with its count. <c>Quantity</c> is null when the item is not counted.</summary>
public record StockItemDto(
    Guid MenuItemId, string Name, string CategoryName, int? Quantity, int? LowStockAt, bool IsLow, bool IsSoldOut);

/// <summary>A null <c>Quantity</c> stops counting the item (it becomes unlimited again).</summary>
public record SetStockDto(int? Quantity, int? LowStockAt);

/// <summary>An ingredient with its count in one branch. <c>Quantity</c> is null when the branch does not count it.</summary>
public record IngredientDto(
    Guid Id, string Name, string Unit, decimal? Quantity, decimal? LowStockAt, bool IsLow, bool IsOut, int UsedInItems,
    decimal? CostPerUnit = null, string? Barcode = null);

/// <summary>A null cost means "not known".</summary>
public record SetIngredientCostDto(decimal? CostPerUnit);

public record SaveIngredientDto(string Name, string? Unit, string? Barcode = null);

public record SetIngredientStockDto(decimal? Quantity, decimal? LowStockAt);

/// <summary>One recipe line as shown to staff. A null <c>VariantId</c> means every size.</summary>
public record RecipeLineDto(Guid IngredientId, string IngredientName, string Unit, Guid? VariantId, decimal Quantity);

public record SaveRecipeLineDto(Guid IngredientId, Guid? VariantId, decimal Quantity);

public record SaveRecipeDto(List<SaveRecipeLineDto> Lines);

/// <summary>One thing that is running low or has run out at a branch. `Kind` is "dish" or "ingredient".</summary>
public record StockAlertDto(string Kind, Guid Id, string Name, Guid LocationId, string LocationName, decimal Quantity, string? Unit, bool IsOut);

public record StockAlertsDto(int Low, int Out, List<StockAlertDto> Items);

/// <summary>
/// One hand change to a count. A null quantity means "not counted"; <c>Change</c> is the difference when both are numbers.
/// </summary>
public record StockChangeDto(
    Guid Id, string Kind, Guid ItemId, string ItemName, string? Unit,
    decimal? OldQuantity, decimal? NewQuantity, decimal? Change, string ChangedBy, DateTime ChangedAt,
    string Type = "set", string? Reason = null, string? Note = null, decimal? UnitCost = null,
    Guid? OtherLocationId = null, string? OtherLocationName = null, decimal? CostValue = null, Guid? PurchaseId = null);

/// <summary>A phone that gets low-stock pushes. <c>HasApp</c> is true when a cafe-app account with this number has a
/// device registered for pushes, so the alert can actually arrive.</summary>
public record StockAlertPhoneDto(string Phone, bool HasApp);

public record SaveStockAlertPhonesDto(List<string>? Phones);

public record ToppingUseDto(Guid IngredientId, string IngredientName, string Unit, decimal Quantity);

public record SaveToppingUseDto(Guid IngredientId, decimal Quantity);

public record SaveToppingUsesDto(List<SaveToppingUseDto> Lines);

// ── Stock pack: restocks with cost, write-offs, moves between branches, stocktakes, food cost ──

/// <summary>Stock that arrived. <c>TotalCost</c> is what the whole delivery cost; it updates the ingredient's average cost.</summary>
public record RestockDto(Guid LocationId, Guid IngredientId, decimal Quantity, decimal? TotalCost, string? Note);

/// <summary><c>Kind</c> is "dish" or "ingredient"; <c>Reason</c> is one of spilled, expired, staff, broken, other.</summary>
public record WriteOffDto(Guid LocationId, string Kind, Guid ItemId, decimal Quantity, string Reason, string? Note);

public record TransferDto(Guid FromLocationId, Guid ToLocationId, string Kind, Guid ItemId, decimal Quantity, string? Note);

/// <summary>A count done by hand: what is really on the shelf now.</summary>
public record StocktakeLineDto(string Kind, Guid ItemId, decimal Counted);

public record StocktakeDto(Guid LocationId, List<StocktakeLineDto>? Lines, string? Note);

/// <summary>
/// One line of a stocktake: what the system expected, what was counted, and the difference (minus = missing).
/// <c>CostDifference</c> is the money value of the difference when the cost is known.
/// </summary>
public record StocktakeResultLineDto(
    string Kind, Guid ItemId, string Name, string? Unit, decimal Expected, decimal Counted, decimal Difference, decimal? CostDifference);

/// <summary>Totals are money values of the lines with a known cost: missing stock and extra stock.</summary>
public record StocktakeResultDto(List<StocktakeResultLineDto> Lines, decimal MissingCost, decimal ExtraCost);

/// <summary>Result of a restock, write-off or move: the new count at the branch it was made from.</summary>
public record StockMoveResultDto(string Kind, Guid ItemId, string Name, string? Unit, decimal Left, decimal? CostPerUnit);

public record WriteOffReasonTotalDto(string Reason, int Count, decimal Cost);

/// <summary>Write-offs in a period by reason. <c>Cost</c> counts only write-offs whose cost was known.</summary>
public record WriteOffSummaryDto(DateTime From, DateTime To, List<WriteOffReasonTotalDto> ByReason, decimal TotalCost);

/// <summary>
/// What one portion of a dish (one size) costs to make from its recipe, against its menu price.
/// <c>Cost</c> is null when the dish has no recipe; <c>MissingCost</c> names ingredients without a cost (counted as 0).
/// </summary>
public record FoodCostDto(
    Guid MenuItemId, string Name, string CategoryName, Guid? VariantId, string? VariantLabel,
    decimal Price, decimal? Cost, decimal? FoodCostPercent, decimal? Margin, List<string> MissingCost);

/// <summary>Something an order needs more of than the branch has left. Accepting it anyway stops the count at zero.</summary>
public record StockShortDto(string Kind, Guid Id, string Name, string? Unit, decimal Need, decimal Left);
