namespace Yurt.Application.Features.Profit;

/// <summary>One dish (and size) over a month: what it sold, what its ingredients cost and what is left.</summary>
public record ItemProfitDto(
    Guid MenuItemId, string Name, Guid? VariantId, string? Size,
    int Sold,
    // Money taken for it, after the order's discount is shared out over the order's lines.
    decimal Revenue,
    // Null when the dish has no recipe, so its cost is unknown.
    decimal? Cost, decimal? Profit, decimal? FoodCostPercent, decimal? ProfitPerItem,
    // Portions sold before the cost was saved on each sale: priced at today's ingredient costs.
    int EstimatedCount,
    // A recipe ingredient (or topping) has no cost per unit yet, so the cost counts it as 0.
    bool MissingPrice);

public record ItemProfitReportDto(string Month, Guid? LocationId, List<ItemProfitDto> Items);

public record WriteOffReasonCostDto(string Reason, decimal Cost);

public record CafeExpenseDto(Guid Id, string Month, Guid? LocationId, string? LocationName, string Category, string Name, decimal Amount);

public record SaveCafeExpenseDto
{
    public string Month { get; init; } = "";
    public Guid? LocationId { get; init; }
    public string Category { get; init; } = "";
    public string Name { get; init; } = "";
    public decimal Amount { get; init; }
}

public record CopyExpensesDto
{
    /// <summary>The month to fill, "yyyy-MM"; the rows are copied from the month before it.</summary>
    public string Month { get; init; } = "";
}

/// <summary>The key numbers of a month, used for "last month" next to the chosen one.</summary>
public record ProfitTotalsDto(decimal NetSales, decimal IngredientCost, decimal GrossProfit, decimal OtherCosts, decimal Profit);

/// <summary>
/// The month's profit: what came in, minus ingredients of what was sold, minus stock lost, wages and the costs typed in.
/// </summary>
public record ProfitMonthDto(
    string Month, Guid? LocationId,
    int Orders,
    // Completed orders before discounts.
    decimal Sales,
    decimal Discounts,
    // Completed orders whose money was given back.
    decimal Refunds,
    decimal NetSales,
    decimal IngredientCost,
    // Order lines priced at today's costs because they were sold before the cost was saved on each sale.
    int EstimatedLines,
    // Lines of dishes without a recipe: their ingredients are not in <see cref="IngredientCost"/>.
    int LinesWithoutRecipe,
    decimal GrossProfit,
    decimal WriteOffs,
    List<WriteOffReasonCostDto> WriteOffsByReason,
    // Value of what stocktakes found missing, minus what they found extra. Negative = more found than expected.
    decimal StocktakeLoss,
    // Null when payroll is off or the report is for one branch (wages are not split by branch).
    decimal? Payroll,
    bool PayrollClosed,
    List<CafeExpenseDto> Expenses,
    decimal ExpensesTotal,
    decimal Profit,
    ProfitTotalsDto Previous);
