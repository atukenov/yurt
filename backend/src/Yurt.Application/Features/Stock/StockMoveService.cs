using Microsoft.EntityFrameworkCore;
using Yurt.Application.Common.Interfaces;
using Yurt.Application.Common.Models;
using Yurt.Domain.Entities;

namespace Yurt.Application.Features.Stock;

/// <summary>
/// The stock pack (own_pos): deliveries with their cost, write-offs with a reason, moves between branches, stocktakes
/// and the food cost of each dish. Every change lands in the stock history with who did it.
/// Staff (not only admins) can write off, move and count, so a barista can fix the shelf without the owner.
/// Counts never go below zero: taking more than is left is refused with how much is left.
/// </summary>
public class StockMoveService(
    IApplicationDbContext db, IAuditLogService audit, IMenuCacheService menuCache, ICurrentUserService user)
{
    private const decimal MaxQuantity = 10_000_000m, MaxCost = 1_000_000_000m;
    private const int MaxNote = 300, MaxStocktakeLines = 1000;

    private sealed record Item(string Kind, Guid Id, string Name, string? Unit, decimal? CostPerUnit);

    public async Task<Result<StockMoveResultDto>> RestockAsync(RestockDto dto, CancellationToken ct = default)
    {
        if (QuantityError(dto.Quantity, "ingredient") is { } qtyError) return Fail(qtyError, 400);
        if (dto.TotalCost is < 0 or > MaxCost) return Fail("Cost must be zero or more.", 400);
        if (NoteError(dto.Note) is { } noteError) return Fail(noteError, 400);
        var ingredient = await db.Ingredients.FirstOrDefaultAsync(i => i.Id == dto.IngredientId, ct);
        if (ingredient is null) return Fail("Ingredient not found.", 404);
        if (!await db.Locations.AnyAsync(l => l.Id == dto.LocationId, ct)) return Fail("Location not found.", 404);

        var item = new Item("ingredient", ingredient.Id, ingredient.Name, ingredient.Unit, ingredient.CostPerUnit);
        var before = await CountAsync(dto.LocationId, item, ct);
        await AddAsync(dto.LocationId, item, dto.Quantity, ct);

        decimal? deliveryUnitCost = null;
        if (dto.TotalCost is { } total)
        {
            deliveryUnitCost = Math.Round(total / dto.Quantity, 4);
            // Running average: what is already on the shelf at its old cost plus this delivery at its price.
            var onShelf = Math.Max(0m, before ?? 0m);
            ingredient.CostPerUnit = ingredient.CostPerUnit is { } old && onShelf > 0
                ? Math.Round((old * onShelf + total) / (onShelf + dto.Quantity), 4)
                : deliveryUnitCost;
            ingredient.UpdatedAt = DateTime.UtcNow;
        }

        Log(dto.LocationId, item, before, (before ?? 0m) + dto.Quantity, StockChangeTypes.Restock, dto.Note,
            unitCost: deliveryUnitCost);
        await db.SaveChangesAsync(ct);
        await menuCache.InvalidateAllMenuAsync();
        await audit.LogAsync("StockRestocked", "Ingredient", ingredient.Id.ToString(),
            $"Branch {dto.LocationId}: +{dto.Quantity} {ingredient.Unit}{(dto.TotalCost is null ? "" : $" for {dto.TotalCost}")}", ct);
        return await ResultAsync(dto.LocationId, item with { CostPerUnit = ingredient.CostPerUnit }, ct);
    }

    public async Task<Result<StockMoveResultDto>> WriteOffAsync(WriteOffDto dto, CancellationToken ct = default)
    {
        var reason = dto.Reason?.Trim().ToLowerInvariant() ?? "";
        if (!WriteOffReasons.All.Contains(reason)) return Fail("Choose why the stock is written off.", 400);
        if (QuantityError(dto.Quantity, dto.Kind) is { } qtyError) return Fail(qtyError, 400);
        if (NoteError(dto.Note) is { } noteError) return Fail(noteError, 400);
        var item = await FindAsync(dto.Kind, dto.ItemId, ct);
        if (item is null) return Fail("Item not found.", 404);
        if (!await db.Locations.AnyAsync(l => l.Id == dto.LocationId, ct)) return Fail("Location not found.", 404);

        var (taken, left) = await TakeAsync(dto.LocationId, item, dto.Quantity, ct);
        if (taken is { } error) return Fail(error, left is null ? 400 : 409);

        var row = Log(dto.LocationId, item, left + dto.Quantity, left, StockChangeTypes.WriteOff, dto.Note, item.CostPerUnit);
        row.Reason = reason;
        await db.SaveChangesAsync(ct);
        await menuCache.InvalidateAllMenuAsync();
        await audit.LogAsync("StockWrittenOff", item.Kind == "dish" ? "MenuItem" : "Ingredient", item.Id.ToString(),
            $"Branch {dto.LocationId}: -{dto.Quantity} {item.Unit} ({reason})", ct);
        return await ResultAsync(dto.LocationId, item, ct);
    }

    /// <summary>
    /// Moves stock from one branch to another. The sending branch must count the item and have enough; the receiving
    /// branch starts counting it if it did not yet.
    /// </summary>
    public async Task<Result<StockMoveResultDto>> TransferAsync(TransferDto dto, CancellationToken ct = default)
    {
        if (dto.FromLocationId == dto.ToLocationId) return Fail("Choose a different branch to move to.", 400);
        if (QuantityError(dto.Quantity, dto.Kind) is { } qtyError) return Fail(qtyError, 400);
        if (NoteError(dto.Note) is { } noteError) return Fail(noteError, 400);
        var item = await FindAsync(dto.Kind, dto.ItemId, ct);
        if (item is null) return Fail("Item not found.", 404);
        var branches = await db.Locations.Where(l => l.Id == dto.FromLocationId || l.Id == dto.ToLocationId)
            .ToDictionaryAsync(l => l.Id, l => l.Name, ct);
        if (branches.Count != 2) return Fail("Location not found.", 404);

        var (taken, left) = await TakeAsync(dto.FromLocationId, item, dto.Quantity, ct);
        if (taken is { } error) return Fail(error, left is null ? 400 : 409);
        var receivedBefore = await CountAsync(dto.ToLocationId, item, ct);
        await AddAsync(dto.ToLocationId, item, dto.Quantity, ct);

        var outRow = Log(dto.FromLocationId, item, left + dto.Quantity, left, StockChangeTypes.TransferOut, dto.Note, item.CostPerUnit);
        outRow.OtherLocationId = dto.ToLocationId;
        outRow.OtherLocationName = branches[dto.ToLocationId];
        var inRow = Log(dto.ToLocationId, item, receivedBefore, (receivedBefore ?? 0m) + dto.Quantity,
            StockChangeTypes.TransferIn, dto.Note, item.CostPerUnit);
        inRow.OtherLocationId = dto.FromLocationId;
        inRow.OtherLocationName = branches[dto.FromLocationId];
        await db.SaveChangesAsync(ct);
        await menuCache.InvalidateAllMenuAsync();
        await audit.LogAsync("StockMoved", item.Kind == "dish" ? "MenuItem" : "Ingredient", item.Id.ToString(),
            $"{dto.Quantity} {item.Unit}: {branches[dto.FromLocationId]} -> {branches[dto.ToLocationId]}", ct);
        return await ResultAsync(dto.FromLocationId, item, ct);
    }

    /// <summary>
    /// Saves a hand count of the shelf. Each counted line replaces the number the system expected; the result shows the
    /// difference (minus = missing) and its money value. Only items the branch counts can be in a stocktake.
    /// </summary>
    public async Task<Result<StocktakeResultDto>> StocktakeAsync(StocktakeDto dto, CancellationToken ct = default)
    {
        var lines = dto.Lines ?? [];
        if (lines.Count == 0) return Result<StocktakeResultDto>.Failure("Count at least one item.", 400);
        if (lines.Count > MaxStocktakeLines) return Result<StocktakeResultDto>.Failure("Too many lines.", 400);
        if (NoteError(dto.Note) is { } noteError) return Result<StocktakeResultDto>.Failure(noteError, 400);
        if (lines.GroupBy(l => (l.Kind, l.ItemId)).Any(g => g.Count() > 1))
            return Result<StocktakeResultDto>.Failure("An item is counted twice.", 400);
        foreach (var l in lines)
        {
            if (l.Kind is not ("dish" or "ingredient")) return Result<StocktakeResultDto>.Failure("Unknown item kind.", 400);
            if (l.Counted < 0 || l.Counted > MaxQuantity) return Result<StocktakeResultDto>.Failure("Counts cannot be negative.", 400);
            if (l.Kind == "dish" && l.Counted != decimal.Truncate(l.Counted))
                return Result<StocktakeResultDto>.Failure("Dishes are counted in whole pieces.", 400);
        }
        if (!await db.Locations.AnyAsync(l => l.Id == dto.LocationId, ct))
            return Result<StocktakeResultDto>.Failure("Location not found.", 404);

        var dishIds = lines.Where(l => l.Kind == "dish").Select(l => l.ItemId).ToList();
        var ingredientIds = lines.Where(l => l.Kind == "ingredient").Select(l => l.ItemId).ToList();
        var dishRows = await db.MenuItemStocks.Include(s => s.MenuItem)
            .Where(s => s.LocationId == dto.LocationId && dishIds.Contains(s.MenuItemId))
            .ToDictionaryAsync(s => s.MenuItemId, ct);
        var ingredientRows = await db.IngredientStocks.Include(s => s.Ingredient)
            .Where(s => s.LocationId == dto.LocationId && ingredientIds.Contains(s.IngredientId))
            .ToDictionaryAsync(s => s.IngredientId, ct);
        if (dishRows.Count != dishIds.Count || ingredientRows.Count != ingredientIds.Count)
            return Result<StocktakeResultDto>.Failure("An item is not counted at this branch. Start counting it on the Stock page first.", 400);

        var now = DateTime.UtcNow;
        var result = new List<StocktakeResultLineDto>();
        foreach (var l in lines)
        {
            Item item;
            decimal expected;
            if (l.Kind == "dish")
            {
                var row = dishRows[l.ItemId];
                item = new Item("dish", row.MenuItemId, row.MenuItem.Name, null, null);
                expected = row.Quantity;
                row.Quantity = (int)l.Counted;
                row.UpdatedAt = now;
            }
            else
            {
                var row = ingredientRows[l.ItemId];
                item = new Item("ingredient", row.IngredientId, row.Ingredient.Name, row.Ingredient.Unit, row.Ingredient.CostPerUnit);
                expected = row.Quantity;
                row.Quantity = l.Counted;
                row.UpdatedAt = now;
            }
            var difference = l.Counted - expected;
            decimal? cost = item.CostPerUnit is { } c ? Math.Round(difference * c, 2) : null;
            result.Add(new StocktakeResultLineDto(item.Kind, item.Id, item.Name, item.Unit, expected, l.Counted, difference, cost));
            if (difference != 0)
                Log(dto.LocationId, item, expected, l.Counted, StockChangeTypes.Stocktake, dto.Note, item.CostPerUnit);
        }
        await db.SaveChangesAsync(ct);
        await menuCache.InvalidateAllMenuAsync();

        var missing = -result.Where(r => r.CostDifference < 0).Sum(r => r.CostDifference!.Value);
        var extra = result.Where(r => r.CostDifference > 0).Sum(r => r.CostDifference!.Value);
        await audit.LogAsync("Stocktake", "Location", dto.LocationId.ToString(),
            $"{lines.Count} items, {result.Count(r => r.Difference != 0)} different, missing {missing}, extra {extra}", ct);
        return Result<StocktakeResultDto>.Success(new StocktakeResultDto(
            result.OrderBy(r => r.Difference).ThenBy(r => r.Name).ToList(), missing, extra));
    }

    /// <summary>Write-offs in a branch (or all branches) over a period, totalled by reason.</summary>
    public async Task<WriteOffSummaryDto> WriteOffSummaryAsync(
        Guid? locationId, DateTime? from, DateTime? to, CancellationToken ct = default)
    {
        var end = to ?? DateTime.UtcNow;
        var start = from ?? end.AddDays(-30);
        var rows = await db.StockChanges
            .Where(c => c.Type == StockChangeTypes.WriteOff && c.CreatedAt >= start && c.CreatedAt < end
                && (locationId == null || c.LocationId == locationId))
            .Select(c => new { c.Reason, c.OldQuantity, c.NewQuantity, c.UnitCost })
            .ToListAsync(ct);
        var byReason = rows
            .GroupBy(r => r.Reason ?? WriteOffReasons.Other)
            .Select(g => new WriteOffReasonTotalDto(g.Key, g.Count(), Math.Round(g.Sum(r =>
                r.UnitCost is { } c && r.OldQuantity is { } o && r.NewQuantity is { } n ? (o - n) * c : 0m), 2)))
            .OrderByDescending(r => r.Cost).ThenByDescending(r => r.Count)
            .ToList();
        return new WriteOffSummaryDto(start, end, byReason, byReason.Sum(r => r.Cost));
    }

    /// <summary>
    /// What one portion of each dish with a recipe costs to make, per size, against its price. Toppings are not
    /// included (they are priced and costed separately).
    /// </summary>
    public async Task<List<FoodCostDto>> FoodCostAsync(CancellationToken ct = default)
    {
        var recipes = await db.RecipeLines
            .Select(r => new { r.MenuItemId, r.VariantId, r.Quantity, r.Ingredient.Name, r.Ingredient.CostPerUnit })
            .ToListAsync(ct);
        var itemIds = recipes.Select(r => r.MenuItemId).Distinct().ToList();
        var items = await db.MenuItems.Where(i => itemIds.Contains(i.Id))
            .OrderBy(i => i.Category.SortOrder).ThenBy(i => i.SortOrder).ThenBy(i => i.Name)
            .Select(i => new
            {
                i.Id, i.Name, Category = i.Category.Name, i.Price,
                Variants = i.Variants.OrderBy(v => v.SortOrder).Select(v => new { v.Id, v.Label, v.Price }).ToList(),
            })
            .ToListAsync(ct);

        var result = new List<FoodCostDto>();
        foreach (var item in items)
        {
            var own = recipes.Where(r => r.MenuItemId == item.Id).ToList();
            var sizes = item.Variants.Count == 0
                ? [(Id: (Guid?)null, Label: (string?)null, item.Price)]
                : item.Variants.Select(v => (Id: (Guid?)v.Id, Label: (string?)v.Label, v.Price)).ToList();
            foreach (var size in sizes)
            {
                var lines = own.Where(r => r.VariantId == null || r.VariantId == size.Id).ToList();
                if (lines.Count == 0)
                {
                    result.Add(new FoodCostDto(item.Id, item.Name, item.Category, size.Id, size.Label, size.Price, null, null, null, []));
                    continue;
                }
                var cost = Math.Round(lines.Sum(l => l.Quantity * (l.CostPerUnit ?? 0m)), 2);
                var missing = lines.Where(l => l.CostPerUnit is null).Select(l => l.Name).Distinct().OrderBy(n => n).ToList();
                decimal? percent = size.Price > 0 ? Math.Round(cost / size.Price * 100m, 1) : null;
                result.Add(new FoodCostDto(item.Id, item.Name, item.Category, size.Id, size.Label, size.Price,
                    cost, percent, size.Price - cost, missing));
            }
        }
        return result;
    }

    // ── helpers ──

    private static Result<StockMoveResultDto> Fail(string error, int status) => Result<StockMoveResultDto>.Failure(error, status);

    private static string? QuantityError(decimal quantity, string kind)
    {
        if (quantity <= 0 || quantity > MaxQuantity) return "Amount must be more than zero.";
        if (kind == "dish" && quantity != decimal.Truncate(quantity)) return "Dishes are counted in whole pieces.";
        return null;
    }

    private static string? NoteError(string? note) => note?.Length > MaxNote ? $"Note is too long (up to {MaxNote} characters)." : null;

    private async Task<Item?> FindAsync(string kind, Guid id, CancellationToken ct) => kind switch
    {
        "ingredient" => await db.Ingredients.Where(i => i.Id == id)
            .Select(i => new Item("ingredient", i.Id, i.Name, i.Unit, i.CostPerUnit)).FirstOrDefaultAsync(ct),
        "dish" => await db.MenuItems.Where(i => i.Id == id)
            .Select(i => new Item("dish", i.Id, i.Name, null, null)).FirstOrDefaultAsync(ct),
        _ => null,
    };

    /// <summary>The branch's count of the item, or null when the branch does not count it.</summary>
    private async Task<decimal?> CountAsync(Guid locationId, Item item, CancellationToken ct) => item.Kind == "dish"
        ? await db.MenuItemStocks.Where(s => s.MenuItemId == item.Id && s.LocationId == locationId)
            .Select(s => (decimal?)s.Quantity).FirstOrDefaultAsync(ct)
        : await db.IngredientStocks.Where(s => s.IngredientId == item.Id && s.LocationId == locationId)
            .Select(s => (decimal?)s.Quantity).FirstOrDefaultAsync(ct);

    /// <summary>
    /// Takes stock off the shelf in one database step, so two people (or an order) at the same moment cannot take it
    /// below zero. Returns an error and what is left when there is not enough, or null and what is left after.
    /// </summary>
    private async Task<(string? Error, decimal? Left)> TakeAsync(Guid locationId, Item item, decimal quantity, CancellationToken ct)
    {
        var now = DateTime.UtcNow;
        int changed;
        if (item.Kind == "dish")
        {
            var q = (int)quantity;
            changed = await db.MenuItemStocks
                .Where(s => s.MenuItemId == item.Id && s.LocationId == locationId && s.Quantity >= q)
                .ExecuteUpdateAsync(u => u.SetProperty(s => s.Quantity, s => s.Quantity - q).SetProperty(s => s.UpdatedAt, now), ct);
        }
        else
        {
            changed = await db.IngredientStocks
                .Where(s => s.IngredientId == item.Id && s.LocationId == locationId && s.Quantity >= quantity)
                .ExecuteUpdateAsync(u => u.SetProperty(s => s.Quantity, s => s.Quantity - quantity).SetProperty(s => s.UpdatedAt, now), ct);
        }

        var left = await CountAsync(locationId, item, ct);
        if (left is null) return ($"'{item.Name}' is not counted at this branch.", null);
        if (changed == 0)
            return ($"Only {left.Value:0.###}{(item.Unit is null ? "" : " " + item.Unit)} of '{item.Name}' left at this branch.", left);
        return (null, left);
    }

    /// <summary>Puts stock on the shelf; starts counting the item at the branch when it was not counted yet.</summary>
    private async Task AddAsync(Guid locationId, Item item, decimal quantity, CancellationToken ct)
    {
        var now = DateTime.UtcNow;
        if (item.Kind == "dish")
        {
            var q = (int)quantity;
            var changed = await db.MenuItemStocks.Where(s => s.MenuItemId == item.Id && s.LocationId == locationId)
                .ExecuteUpdateAsync(u => u.SetProperty(s => s.Quantity, s => s.Quantity + q).SetProperty(s => s.UpdatedAt, now), ct);
            if (changed == 0) db.MenuItemStocks.Add(new MenuItemStock { MenuItemId = item.Id, LocationId = locationId, Quantity = q });
        }
        else
        {
            var changed = await db.IngredientStocks.Where(s => s.IngredientId == item.Id && s.LocationId == locationId)
                .ExecuteUpdateAsync(u => u.SetProperty(s => s.Quantity, s => s.Quantity + quantity).SetProperty(s => s.UpdatedAt, now), ct);
            if (changed == 0) db.IngredientStocks.Add(new IngredientStock { IngredientId = item.Id, LocationId = locationId, Quantity = quantity });
        }
    }

    private StockChange Log(Guid locationId, Item item, decimal? before, decimal? after, string type, string? note, decimal? unitCost)
    {
        var row = new StockChange
        {
            LocationId = locationId, Kind = item.Kind, ItemId = item.Id, ItemName = item.Name, Unit = item.Unit,
            OldQuantity = before, NewQuantity = after, ChangedBy = user.Username ?? "unknown", Type = type,
            Note = string.IsNullOrWhiteSpace(note) ? null : note.Trim(), UnitCost = unitCost,
        };
        db.StockChanges.Add(row);
        return row;
    }

    private async Task<Result<StockMoveResultDto>> ResultAsync(Guid locationId, Item item, CancellationToken ct)
        => Result<StockMoveResultDto>.Success(new StockMoveResultDto(
            item.Kind, item.Id, item.Name, item.Unit, await CountAsync(locationId, item, ct) ?? 0m, item.CostPerUnit));
}
