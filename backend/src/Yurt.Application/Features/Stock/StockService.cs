using Microsoft.EntityFrameworkCore;
using Yurt.Application.Common.Interfaces;
using Yurt.Application.Common.Models;
using Yurt.Domain;
using Yurt.Domain.Entities;

namespace Yurt.Application.Features.Stock;

/// <summary>
/// Per-branch stock counts and the automatic stop-list (Phase 6, <c>own_pos</c>). An item with a count is hidden from the
/// customer menu of that branch at zero and cannot be ordered beyond what is left; an accepted order lowers the count.
/// While the <c>own_pos</c> module is off nothing is enforced, so an item can never stay stuck at zero without a way to fix it.
/// </summary>
public class StockService(
    IApplicationDbContext db, IAuditLogService audit, IMenuCacheService menuCache, IModuleGate modules)
{
    public Task<bool> IsActiveAsync(CancellationToken ct = default) => modules.IsEnabledAsync(ModuleKeys.OwnPos, ct);

    /// <summary>The branch's menu items with their counts, for the admin Stock page.</summary>
    public async Task<Result<List<StockItemDto>>> ListAsync(Guid locationId, CancellationToken ct = default)
    {
        if (!await db.Locations.AnyAsync(l => l.Id == locationId, ct))
            return Result<List<StockItemDto>>.Failure("Location not found.", 404);

        var items = await db.MenuItems
            .Where(i => !i.MenuItemLocations.Any() || i.MenuItemLocations.Any(l => l.LocationId == locationId))
            .OrderBy(i => i.Category.SortOrder).ThenBy(i => i.SortOrder).ThenBy(i => i.Name)
            .Select(i => new { i.Id, i.Name, Category = i.Category.Name })
            .ToListAsync(ct);
        var counts = await db.MenuItemStocks.Where(s => s.LocationId == locationId).ToDictionaryAsync(s => s.MenuItemId, ct);

        var result = items.Select(i =>
        {
            counts.TryGetValue(i.Id, out var s);
            return new StockItemDto(i.Id, i.Name, i.Category, s?.Quantity, s?.LowStockAt,
                IsLow: s is { LowStockAt: not null } && s.Quantity <= s.LowStockAt && s.Quantity > 0,
                IsSoldOut: s is { Quantity: <= 0 });
        }).ToList();
        return Result<List<StockItemDto>>.Success(result);
    }

    public async Task<Result<StockItemDto>> SetAsync(Guid menuItemId, Guid locationId, SetStockDto dto, CancellationToken ct = default)
    {
        if (dto.Quantity is < 0 || dto.LowStockAt is < 0)
            return Result<StockItemDto>.Failure("Numbers cannot be negative.", 400);

        var item = await db.MenuItems.Include(i => i.Category).FirstOrDefaultAsync(i => i.Id == menuItemId, ct);
        if (item is null) return Result<StockItemDto>.Failure("Menu item not found.", 404);
        if (!await db.Locations.AnyAsync(l => l.Id == locationId, ct))
            return Result<StockItemDto>.Failure("Location not found.", 404);

        var row = await db.MenuItemStocks.FirstOrDefaultAsync(s => s.MenuItemId == menuItemId && s.LocationId == locationId, ct);
        if (dto.Quantity is null)
        {
            if (row is not null) db.MenuItemStocks.Remove(row);
            row = null;
        }
        else if (row is null)
        {
            row = new MenuItemStock { MenuItemId = menuItemId, LocationId = locationId, Quantity = dto.Quantity.Value, LowStockAt = dto.LowStockAt };
            db.MenuItemStocks.Add(row);
        }
        else
        {
            row.Quantity = dto.Quantity.Value;
            row.LowStockAt = dto.LowStockAt;
            row.UpdatedAt = DateTime.UtcNow;
        }
        await db.SaveChangesAsync(ct);
        await menuCache.InvalidateAllMenuAsync();

        await audit.LogAsync("StockSet", "MenuItem", menuItemId.ToString(),
            $"Branch {locationId}: {(dto.Quantity?.ToString() ?? "not counted")}", ct);
        return Result<StockItemDto>.Success(new StockItemDto(
            item.Id, item.Name, item.Category.Name, row?.Quantity, row?.LowStockAt,
            IsLow: row is { LowStockAt: not null } && row.Quantity <= row.LowStockAt && row.Quantity > 0,
            IsSoldOut: row is { Quantity: <= 0 }));
    }

    /// <summary>Menu items at zero in this branch. Empty while the module is off.</summary>
    public async Task<List<Guid>> SoldOutItemIdsAsync(Guid locationId, CancellationToken ct = default)
    {
        if (!await IsActiveAsync(ct)) return [];
        var byProduct = await db.MenuItemStocks
            .Where(s => s.LocationId == locationId && s.Quantity <= 0)
            .Select(s => s.MenuItemId)
            .ToListAsync(ct);

        // An item whose every-size recipe needs more of a counted ingredient than the branch has left cannot be made.
        var byIngredient = await (
            from r in db.RecipeLines
            join s in db.IngredientStocks on r.IngredientId equals s.IngredientId
            where r.VariantId == null && s.LocationId == locationId && s.Quantity < r.Quantity
            select r.MenuItemId).Distinct().ToListAsync(ct);

        return byProduct.Union(byIngredient).ToList();
    }

    /// <summary>
    /// Checks whether the ingredients left in the branch cover an order's recipes. Returns a customer-facing message for
    /// the first item that cannot be made, or null when everything is covered (or the ingredients are not counted).
    /// </summary>
    public async Task<string?> FindIngredientShortageAsync(
        Guid locationId, IReadOnlyList<(Guid MenuItemId, Guid? VariantId, int Quantity)> lines,
        IReadOnlyDictionary<Guid, string> names, CancellationToken ct = default)
    {
        if (lines.Count == 0 || !await IsActiveAsync(ct)) return null;

        var recipes = await RecipesForAsync(lines, ct);
        if (recipes.Count == 0) return null;
        var needs = NeedsOf(lines, recipes);
        var ids = needs.Keys.ToList();
        var left = await db.IngredientStocks
            .Where(s => s.LocationId == locationId && ids.Contains(s.IngredientId))
            .ToDictionaryAsync(s => s.IngredientId, s => s.Quantity, ct);

        foreach (var (ingredientId, need) in needs)
        {
            if (!left.TryGetValue(ingredientId, out var have) || need <= have) continue;
            var culprit = lines.First(l => recipes.Any(r => r.IngredientId == ingredientId && Uses(r, l)));
            return $"'{names.GetValueOrDefault(culprit.MenuItemId, "An item")}' is not available in that amount right now.";
        }
        return null;
    }

    private async Task<List<RecipeLine>> RecipesForAsync(
        IReadOnlyList<(Guid MenuItemId, Guid? VariantId, int Quantity)> lines, CancellationToken ct)
    {
        var itemIds = lines.Select(l => l.MenuItemId).Distinct().ToList();
        return await db.RecipeLines.Where(r => itemIds.Contains(r.MenuItemId)).ToListAsync(ct);
    }

    private static bool Uses(RecipeLine r, (Guid MenuItemId, Guid? VariantId, int Quantity) line)
        => r.MenuItemId == line.MenuItemId && (r.VariantId == null || r.VariantId == line.VariantId);

    private static Dictionary<Guid, decimal> NeedsOf(
        IReadOnlyList<(Guid MenuItemId, Guid? VariantId, int Quantity)> lines, List<RecipeLine> recipes)
    {
        var needs = new Dictionary<Guid, decimal>();
        foreach (var line in lines)
            foreach (var r in recipes.Where(r => Uses(r, line)))
                needs[r.IngredientId] = needs.GetValueOrDefault(r.IngredientId) + r.Quantity * line.Quantity;
        return needs;
    }

    /// <summary>
    /// Checks an order's lines against what is left. Returns a customer-facing message for the first item that
    /// cannot be served, or null when everything is in stock (or not counted).
    /// </summary>
    public async Task<string?> FindShortageAsync(
        Guid locationId, IReadOnlyDictionary<Guid, int> wanted, IReadOnlyDictionary<Guid, string> names, CancellationToken ct = default)
    {
        if (wanted.Count == 0 || !await IsActiveAsync(ct)) return null;

        var ids = wanted.Keys.ToList();
        var left = await db.MenuItemStocks
            .Where(s => s.LocationId == locationId && ids.Contains(s.MenuItemId))
            .ToDictionaryAsync(s => s.MenuItemId, s => s.Quantity, ct);

        foreach (var (id, qty) in wanted)
        {
            if (!left.TryGetValue(id, out var have) || qty <= have) continue;
            var name = names.GetValueOrDefault(id, "An item");
            return have <= 0 ? $"'{name}' is sold out." : $"Only {have} left of '{name}'.";
        }
        return null;
    }

    /// <summary>Lowers the counts for an accepted order. Never goes below zero; safe when two orders are accepted together.</summary>
    public async Task ConsumeForOrderAsync(Order order, CancellationToken ct = default)
    {
        if (!await IsActiveAsync(ct)) return;

        var now = DateTime.UtcNow;
        var touched = false;
        foreach (var line in order.Items.GroupBy(i => i.MenuItemId))
        {
            var qty = line.Sum(i => i.Quantity);
            var changed = await db.MenuItemStocks
                .Where(s => s.MenuItemId == line.Key && s.LocationId == order.LocationId)
                .ExecuteUpdateAsync(u => u
                    .SetProperty(s => s.Quantity, s => s.Quantity > qty ? s.Quantity - qty : 0)
                    .SetProperty(s => s.UpdatedAt, now), ct);
            touched |= changed > 0;
        }

        var orderLines = order.Items.Select(i => (i.MenuItemId, i.VariantId, i.Quantity)).ToList();
        var recipes = await RecipesForAsync(orderLines, ct);
        foreach (var (ingredientId, need) in NeedsOf(orderLines, recipes))
        {
            var changed = await db.IngredientStocks
                .Where(s => s.IngredientId == ingredientId && s.LocationId == order.LocationId)
                .ExecuteUpdateAsync(u => u
                    .SetProperty(s => s.Quantity, s => s.Quantity > need ? s.Quantity - need : 0m)
                    .SetProperty(s => s.UpdatedAt, now), ct);
            touched |= changed > 0;
        }

        // The customer menu is cached; an item that just sold out must disappear from it.
        if (touched) await menuCache.InvalidateAllMenuAsync();
    }
}
