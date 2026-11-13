namespace Yurt.Application.Features.Profit;

/// <summary>Menu-engineering group of a dish: how popular it is and how much each portion earns.</summary>
public static class MenuClasses
{
    public const string Star = "star";           // popular, earns well
    public const string Plowhorse = "plowhorse"; // popular, earns little per portion
    public const string Puzzle = "puzzle";       // earns well, few are sold
    public const string Dog = "dog";             // few are sold, earns little
}

/// <summary>What to do about the dish: keep it, push it, fix it (price or recipe) or drop it.</summary>
public static class MenuActions
{
    public const string Keep = "keep";
    public const string Push = "push";
    public const string Fix = "fix";
    public const string Drop = "drop";
}

/// <summary>One dish (all its sizes together) with its menu group, ABC class and what to do.</summary>
public record MenuAnalysisItemDto(
    Guid MenuItemId, string Name, int Sold, decimal Revenue, decimal Cost, decimal Profit, decimal ProfitPerItem,
    // Share of all portions sold / of all profit, in percent.
    decimal SoldShare, decimal ProfitShare,
    string Class, string Action,
    // "A" = the dishes that bring the first 80% of profit, "B" = the next 15%, "C" = the last 5% (and losses).
    string Abc,
    // For plowhorses: how much more each portion would have to earn to reach the menu's average. Null otherwise.
    decimal? RaisePriceBy,
    bool Estimated, bool MissingPrice);

public record MenuAnalysisDto(
    string Month, Guid? LocationId,
    // The two lines that split the menu: portions a dish needs to count as popular, and profit per portion the menu averages.
    decimal PopularFrom, decimal AverageProfitPerItem,
    List<MenuAnalysisItemDto> Items,
    // Dishes sold without a recipe: their cost is unknown, so they are left out of the groups.
    List<MenuAnalysisNoRecipeDto> NoRecipe);

public record MenuAnalysisNoRecipeDto(Guid MenuItemId, string Name, int Sold, decimal Revenue);

/// <summary>
/// Menu engineering from the month's profit by dish. A dish is <b>popular</b> when it sells at least 70% of an even share
/// of all portions (the usual Kasavana-Smith rule), and <b>earns well</b> when its profit per portion is at or above what a
/// portion earns on average over the whole menu.
/// </summary>
public static class MenuAnalysis
{
    private const decimal PopularRule = 0.7m;
    private const decimal ALimit = 80m;
    private const decimal BLimit = 95m;

    public static MenuAnalysisDto Build(ItemProfitReportDto report)
    {
        // Sizes of one dish count together: a guest chooses a dish, not a size.
        var dishes = report.Items.GroupBy(i => i.MenuItemId).Select(g => new
        {
            Id = g.Key, Name = g.First().Name, Sold = g.Sum(i => i.Sold), Revenue = g.Sum(i => i.Revenue),
            Known = g.All(i => i.Cost is not null),
            Cost = g.Sum(i => i.Cost ?? 0m),
            Estimated = g.Any(i => i.EstimatedCount > 0), Missing = g.Any(i => i.MissingPrice),
        }).ToList();

        var noRecipe = dishes.Where(d => !d.Known).OrderByDescending(d => d.Revenue)
            .Select(d => new MenuAnalysisNoRecipeDto(d.Id, d.Name, d.Sold, Math.Round(d.Revenue, 2))).ToList();
        var known = dishes.Where(d => d.Known && d.Sold > 0).ToList();
        if (known.Count == 0) return new MenuAnalysisDto(report.Month, report.LocationId, 0, 0, [], noRecipe);

        var totalSold = known.Sum(d => d.Sold);
        var totalProfit = known.Sum(d => d.Revenue - d.Cost);
        var popularFrom = Math.Round(totalSold / (decimal)known.Count * PopularRule, 1);
        var averagePerItem = totalProfit / totalSold;
        var positiveProfit = known.Sum(d => Math.Max(0m, d.Revenue - d.Cost));

        var rows = new List<MenuAnalysisItemDto>();
        var running = 0m;
        foreach (var d in known.OrderByDescending(d => d.Revenue - d.Cost).ThenByDescending(d => d.Sold))
        {
            var profit = d.Revenue - d.Cost;
            var perItem = profit / d.Sold;
            var popular = d.Sold >= totalSold / (decimal)known.Count * PopularRule;
            var earnsWell = perItem >= averagePerItem;
            var group = popular ? (earnsWell ? MenuClasses.Star : MenuClasses.Plowhorse) : (earnsWell ? MenuClasses.Puzzle : MenuClasses.Dog);
            var action = group switch
            {
                MenuClasses.Star => MenuActions.Keep,
                MenuClasses.Puzzle => MenuActions.Push,
                MenuClasses.Plowhorse => MenuActions.Fix,
                _ => MenuActions.Drop,
            };

            // ABC: a dish is A while the dishes before it have not yet brought 80% of the profit.
            var before = positiveProfit > 0 ? running / positiveProfit * 100m : 100m;
            var abc = profit <= 0 || before >= BLimit ? "C" : before >= ALimit ? "B" : "A";
            if (profit > 0) running += profit;

            rows.Add(new MenuAnalysisItemDto(
                d.Id, d.Name, d.Sold, Math.Round(d.Revenue, 2), Math.Round(d.Cost, 2), Math.Round(profit, 2), Math.Round(perItem, 2),
                Math.Round(d.Sold * 100m / totalSold, 1),
                totalProfit > 0 ? Math.Round(profit * 100m / totalProfit, 1) : 0m,
                group, action, abc,
                group == MenuClasses.Plowhorse ? Math.Round(averagePerItem - perItem, 2) : null,
                d.Estimated, d.Missing));
        }
        return new MenuAnalysisDto(report.Month, report.LocationId, popularFrom, Math.Round(averagePerItem, 2), rows, noRecipe);
    }
}
