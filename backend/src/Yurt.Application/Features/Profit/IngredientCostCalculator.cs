using Microsoft.EntityFrameworkCore;
using Yurt.Application.Common.Interfaces;
using Yurt.Application.Features.Stock;
using Yurt.Domain.Entities;

namespace Yurt.Application.Features.Profit;

/// <summary>What the ingredients of one portion cost: null when the dish has no recipe.</summary>
public record PortionCost(decimal? UnitCost, bool MissingPrice);

/// <summary>
/// Works out what the ingredients of one portion of an ordered item cost: the dish's recipe for its size plus each topping
/// on it, at the ingredients' current cost per unit. An ingredient without a cost counts as 0 (and is flagged).
/// </summary>
public class IngredientCostCalculator(IApplicationDbContext db)
{
    public async Task<List<PortionCost>> ForLinesAsync(IReadOnlyList<StockLine> lines, CancellationToken ct = default)
    {
        if (lines.Count == 0) return [];
        var itemIds = lines.Select(l => l.MenuItemId).Distinct().ToList();
        var recipes = await db.RecipeLines.AsNoTracking()
            .Where(r => itemIds.Contains(r.MenuItemId))
            .Select(r => new { r.MenuItemId, r.VariantId, r.Quantity, r.Ingredient.CostPerUnit })
            .ToListAsync(ct);
        var toppingIds = lines.SelectMany(l => l.ToppingIds).Distinct().ToList();
        var uses = toppingIds.Count == 0
            ? []
            : await db.ToppingUses.AsNoTracking()
                .Where(u => toppingIds.Contains(u.ToppingId))
                .Select(u => new { u.ToppingId, u.Quantity, u.Ingredient.CostPerUnit })
                .ToListAsync(ct);

        var result = new List<PortionCost>(lines.Count);
        foreach (var line in lines)
        {
            var own = recipes.Where(r => r.MenuItemId == line.MenuItemId && (r.VariantId == null || r.VariantId == line.VariantId)).ToList();
            if (own.Count == 0)
            {
                result.Add(new PortionCost(null, false));
                continue;
            }
            var tops = line.ToppingIds.SelectMany(t => uses.Where(u => u.ToppingId == t)).ToList();
            var cost = own.Sum(r => r.Quantity * (r.CostPerUnit ?? 0m)) + tops.Sum(u => u.Quantity * (u.CostPerUnit ?? 0m));
            var missing = own.Any(r => r.CostPerUnit is null) || tops.Any(u => u.CostPerUnit is null);
            result.Add(new PortionCost(Math.Round(cost, 4), missing));
        }
        return result;
    }

    /// <summary>Saves what each line of an order costs right now, so later price changes do not rewrite past profit.</summary>
    public async Task SnapshotAsync(Order order, CancellationToken ct = default)
    {
        var items = order.Items.ToList();
        var costs = await ForLinesAsync(items.Select(ToLine).ToList(), ct);
        for (var i = 0; i < items.Count; i++) items[i].UnitCost = costs[i].UnitCost;
    }

    public static StockLine ToLine(OrderItem i)
        => new(i.MenuItemId, i.VariantId, i.Quantity, i.Toppings.Select(t => t.ToppingId).ToList());
}
