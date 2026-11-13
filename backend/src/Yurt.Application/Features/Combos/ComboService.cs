using Microsoft.EntityFrameworkCore;
using Yurt.Application.Common.Interfaces;
using Yurt.Application.Common.Models;
using Yurt.Application.Features.Menu.Services;
using Yurt.Application.Features.Profit;
using Yurt.Domain.Entities;
using Yurt.Domain.Enums;

namespace Yurt.Application.Features.Combos;

/// <summary>
/// Combos: several dishes sold together at one set price (coffee + pastry). A combo is a menu item with <c>IsCombo</c>, so
/// it sits in a category, can be limited to branches, and is ordered like any dish in the guest app and Yurt POS. Selling
/// one saves what was inside it; stock, cost and kitchen tickets follow the dishes inside.
/// </summary>
public class ComboService(IApplicationDbContext db, IAuditLogService audit, IMenuCacheService menuCache)
{
    public const int MaxParts = 12;
    public const int MaxPartQuantity = 20;

    public async Task<List<ComboDto>> ListAsync(CancellationToken ct = default)
    {
        var combos = await db.MenuItems.Include(m => m.Category).Include(m => m.MenuItemLocations)
            .Where(m => m.IsCombo).OrderBy(m => m.Category.SortOrder).ThenBy(m => m.SortOrder).ThenBy(m => m.Name)
            .ToListAsync(ct);
        return await MapAsync(combos, ct);
    }

    public async Task<Result<ComboDto>> CreateAsync(SaveComboDto dto, CancellationToken ct = default)
    {
        var problem = await CheckAsync(dto, ct);
        if (problem is not null) return Result<ComboDto>.Failure(problem, 400);

        var item = new MenuItem
        {
            IsCombo = true,
            SortOrder = await db.MenuItems.CountAsync(i => i.CategoryId == dto.CategoryId, ct),
        };
        Apply(item, dto);
        db.MenuItems.Add(item);
        await ReplaceChildrenAsync(item, dto, ct);
        await db.SaveChangesAsync(ct);

        await audit.LogAsync("ComboCreated", "MenuItem", item.Id.ToString(), $"{item.Name} · {item.Price:0.##}", ct);
        await menuCache.InvalidateAllMenuAsync();
        return Result<ComboDto>.Success(await OneAsync(item.Id, ct), 201);
    }

    public async Task<Result<ComboDto>> UpdateAsync(Guid id, SaveComboDto dto, CancellationToken ct = default)
    {
        var item = await db.MenuItems.Include(m => m.MenuItemLocations).FirstOrDefaultAsync(m => m.Id == id && m.IsCombo, ct);
        if (item is null) return Result<ComboDto>.NotFound("Combo not found.");
        var problem = await CheckAsync(dto, ct);
        if (problem is not null) return Result<ComboDto>.Failure(problem, 400);

        Apply(item, dto);
        item.UpdatedAt = DateTime.UtcNow;
        await ReplaceChildrenAsync(item, dto, ct);
        await db.SaveChangesAsync(ct);

        await audit.LogAsync("ComboUpdated", "MenuItem", item.Id.ToString(), $"{item.Name} · {item.Price:0.##}", ct);
        await menuCache.InvalidateAllMenuAsync();
        return Result<ComboDto>.Success(await OneAsync(item.Id, ct));
    }

    /// <summary>Removes a combo. One that was already sold stays in the old orders, so it is only switched off.</summary>
    public async Task<Result<bool>> DeleteAsync(Guid id, CancellationToken ct = default)
    {
        var item = await db.MenuItems.FirstOrDefaultAsync(m => m.Id == id && m.IsCombo, ct);
        if (item is null) return Result<bool>.NotFound("Combo not found.");

        var sold = await db.OrderItems.AnyAsync(o => o.MenuItemId == id, ct);
        if (sold)
        {
            item.IsAvailable = false;
            item.UpdatedAt = DateTime.UtcNow;
        }
        else db.MenuItems.Remove(item);
        await db.SaveChangesAsync(ct);

        await audit.LogAsync("ComboDeleted", "MenuItem", id.ToString(), sold ? $"{item.Name} (switched off: already sold)" : item.Name, ct);
        await menuCache.InvalidateAllMenuAsync();
        // true = gone for good, false = only switched off because old orders still show it.
        return Result<bool>.Success(!sold);
    }

    // ── report ──

    /// <summary>What combos sold in a month, what the guests saved and what is left after the dishes inside.</summary>
    public async Task<Result<ComboReportDto>> ReportAsync(string month, Guid? locationId, CancellationToken ct = default)
    {
        if (!ProfitService.IsValidMonth(month)) return Result<ComboReportDto>.Failure("Month must look like 2026-11.", 422);
        if (locationId is { } loc && !await db.Locations.AnyAsync(l => l.Id == loc, ct))
            return Result<ComboReportDto>.NotFound("Location not found.");

        var (start, end) = ProfitService.MonthRangeUtc(month);
        var orders = await db.Orders.AsNoTracking()
            .Where(o => o.Status == OrderStatus.Completed && o.PaymentStatus != PaymentStatus.Refunded
                && (o.CompletedAt ?? o.CreatedAt) >= start && (o.CompletedAt ?? o.CreatedAt) < end
                && (locationId == null || o.LocationId == locationId))
            .Select(o => new
            {
                o.Subtotal, o.Total,
                Combos = o.Items.Where(i => i.ComboParts.Any()).Select(i => new
                {
                    i.MenuItemId, i.MenuItemName, i.Quantity, i.LineTotal, i.UnitCost,
                    Parts = i.ComboParts.Select(p => new { p.Quantity, p.RegularPrice }).ToList(),
                }).ToList(),
            })
            .ToListAsync(ct);

        var lines = new List<(Guid Id, string Name, int Qty, decimal Revenue, decimal Regular, decimal Saved, decimal? Cost)>();
        foreach (var o in orders)
        {
            var share = o.Subtotal > 0 ? o.Total / o.Subtotal : 1m;
            foreach (var c in o.Combos)
            {
                var regular = c.Parts.Sum(p => p.Quantity * p.RegularPrice) * c.Quantity;
                lines.Add((c.MenuItemId, c.MenuItemName, c.Quantity, c.LineTotal * share, regular, Math.Max(0m, regular - c.LineTotal),
                    c.UnitCost is { } uc ? uc * c.Quantity : null));
            }
        }

        static decimal? SumCost(IEnumerable<decimal?> costs) => costs.Any(c => c is null) ? null : Math.Round(costs.Sum(c => c!.Value), 2);

        var items = lines.GroupBy(l => l.Id).Select(g =>
        {
            var revenue = Math.Round(g.Sum(l => l.Revenue), 2);
            var cost = SumCost(g.Select(l => l.Cost));
            return new ComboReportItemDto(g.Key, g.Last().Name, g.Sum(l => l.Qty), revenue,
                Math.Round(g.Sum(l => l.Regular), 2), Math.Round(g.Sum(l => l.Saved), 2), cost, cost is { } c ? revenue - c : null);
        }).OrderByDescending(i => i.Revenue).ToList();

        var totalRevenue = items.Sum(i => i.Revenue);
        var totalCost = items.Count == 0 ? 0m : SumCost(items.Select(i => i.Cost));
        var netSales = orders.Sum(o => o.Total);
        var totals = new ComboReportTotalsDto(
            items.Sum(i => i.Sold), totalRevenue, items.Sum(i => i.RegularValue), items.Sum(i => i.Saved),
            totalCost, totalCost is { } tc ? totalRevenue - tc : null,
            netSales > 0 ? Math.Round(totalRevenue / netSales * 100m, 1) : 0m,
            orders.Count(o => o.Combos.Count > 0));
        return Result<ComboReportDto>.Success(new ComboReportDto(month, locationId, totals, items));
    }

    // ── helpers ──

    private async Task<ComboDto> OneAsync(Guid id, CancellationToken ct)
    {
        var item = await db.MenuItems.Include(m => m.Category).Include(m => m.MenuItemLocations).FirstAsync(m => m.Id == id, ct);
        return (await MapAsync([item], ct))[0];
    }

    private async Task<List<ComboDto>> MapAsync(List<MenuItem> combos, CancellationToken ct)
    {
        var sales = await ComboSales.LoadAsync(db, combos, ct);
        var ids = combos.Select(c => c.Id).ToList();
        var variantIds = combos.SelectMany(c => sales.PartsOf(c.Id)).Where(p => p.VariantId != null).Select(p => p.VariantId!.Value).ToList();
        var variants = variantIds.Count == 0 ? [] : await db.MenuItemVariants.Where(v => variantIds.Contains(v.Id)).ToDictionaryAsync(v => v.Id, ct);
        var sold = await db.OrderItems.Where(o => ids.Contains(o.MenuItemId) && o.Order.Status == OrderStatus.Completed)
            .GroupBy(o => o.MenuItemId).Select(g => new { Id = g.Key, Qty = g.Sum(o => o.Quantity) })
            .ToDictionaryAsync(g => g.Id, g => g.Qty, ct);
        // Combos that can't be sold right now because a dish inside is sold out (the automatic stop-list).
        var stockBlocked = await SoldOutPartsAsync(ids, ct);

        return combos.Select(c =>
        {
            var parts = sales.PartsOf(c.Id).Select(p =>
            {
                variants.TryGetValue(p.VariantId ?? Guid.Empty, out var v);
                if (v is not null && v.MenuItemId != p.ItemId) v = null;
                return new ComboPartDto(p.ItemId, p.Item.Name, v?.Id, v?.Label, p.Quantity, v?.Price ?? p.Item.Price);
            }).ToList();
            var regular = parts.Sum(p => p.RegularPrice * p.Quantity);
            return new ComboDto(c.Id, c.Name, c.NameRu, c.NameKk, c.Description, c.DescriptionRu, c.DescriptionKk,
                c.CategoryId, c.Category.Name, c.Price, c.IsAvailable, c.ImageUrl,
                c.MenuItemLocations.Select(l => l.LocationId).ToList(), parts, regular, Math.Max(0m, regular - c.Price),
                sales.Problem(c, null) is not null || stockBlocked.Contains(c.Id), sold.GetValueOrDefault(c.Id));
        }).ToList();
    }

    private async Task<HashSet<Guid>> SoldOutPartsAsync(List<Guid> comboIds, CancellationToken ct)
    {
        var empty = await db.ComboParts
            .Where(p => comboIds.Contains(p.ComboId) && db.MenuItemStocks.Any(s => s.MenuItemId == p.ItemId && s.Quantity <= 0))
            .Select(p => p.ComboId).Distinct().ToListAsync(ct);
        return empty.ToHashSet();
    }

    private static void Apply(MenuItem item, SaveComboDto dto)
    {
        item.CategoryId = dto.CategoryId;
        item.Name = dto.Name.Trim();
        item.NameRu = Clean(dto.NameRu);
        item.NameKk = Clean(dto.NameKk);
        item.Description = dto.Description?.Trim() ?? string.Empty;
        item.DescriptionRu = Clean(dto.DescriptionRu);
        item.DescriptionKk = Clean(dto.DescriptionKk);
        item.Price = dto.Price;
        item.IsAvailable = dto.IsAvailable;
        item.ImageUrl = Clean(dto.ImageUrl);
    }

    private static string? Clean(string? s) => string.IsNullOrWhiteSpace(s) ? null : s.Trim();

    /// <summary>Returns a message for the first thing wrong with the combo, or null.</summary>
    private async Task<string?> CheckAsync(SaveComboDto dto, CancellationToken ct)
    {
        if (string.IsNullOrWhiteSpace(dto.Name)) return "A combo needs a name.";
        if (dto.Name.Trim().Length > 200) return "The name is too long (200 characters at most).";
        if (dto.Price < 0 || dto.Price > 10_000_000m) return "Check the combo price.";
        if (!await db.MenuCategories.AnyAsync(c => c.Id == dto.CategoryId, ct)) return "Choose a category for the combo.";
        if (dto.Parts is not { Count: > 0 }) return "Add the dishes that are in the combo.";
        if (dto.Parts.Count > MaxParts) return $"A combo can have at most {MaxParts} different dishes.";
        if (dto.Parts.Any(p => p.Quantity < 1 || p.Quantity > MaxPartQuantity))
            return $"Each dish in a combo can be there 1 to {MaxPartQuantity} times.";
        if (dto.Parts.Sum(p => p.Quantity) < 2) return "A combo needs at least two pieces; for one dish just change its price.";

        var itemIds = dto.Parts.Select(p => p.ItemId).Distinct().ToList();
        var items = await db.MenuItems.Where(m => itemIds.Contains(m.Id)).Select(m => new { m.Id, m.Name, m.IsCombo }).ToListAsync(ct);
        if (items.Count != itemIds.Count) return "One of the dishes is no longer on the menu.";
        var nested = items.FirstOrDefault(i => i.IsCombo);
        if (nested is not null) return $"'{nested.Name}' is a combo itself. A combo can't contain another combo.";

        var variantIds = dto.Parts.Where(p => p.VariantId != null).Select(p => p.VariantId!.Value).Distinct().ToList();
        if (variantIds.Count > 0)
        {
            var found = await db.MenuItemVariants.Where(v => variantIds.Contains(v.Id)).Select(v => new { v.Id, v.MenuItemId }).ToListAsync(ct);
            foreach (var p in dto.Parts.Where(p => p.VariantId != null))
                if (!found.Any(v => v.Id == p.VariantId && v.MenuItemId == p.ItemId))
                    return $"That size of '{items.First(i => i.Id == p.ItemId).Name}' no longer exists.";
        }

        if (dto.LocationIds is { Count: > 0 })
        {
            var locIds = dto.LocationIds.Distinct().ToList();
            if (await db.Locations.CountAsync(l => locIds.Contains(l.Id), ct) != locIds.Count) return "One of the branches was not found.";
        }
        return null;
    }

    private async Task ReplaceChildrenAsync(MenuItem item, SaveComboDto dto, CancellationToken ct)
    {
        db.ComboParts.RemoveRange(await db.ComboParts.Where(p => p.ComboId == item.Id).ToListAsync(ct));
        var order = 0;
        // The same dish in the same size twice is one line with the quantities added.
        foreach (var g in dto.Parts.GroupBy(p => (p.ItemId, p.VariantId)))
            db.ComboParts.Add(new ComboPart
            {
                ComboId = item.Id, ItemId = g.Key.ItemId, VariantId = g.Key.VariantId,
                Quantity = Math.Min(MaxPartQuantity, g.Sum(p => p.Quantity)), SortOrder = order++,
            });

        db.MenuItemLocations.RemoveRange(await db.MenuItemLocations.Where(l => l.MenuItemId == item.Id).ToListAsync(ct));
        foreach (var loc in (dto.LocationIds ?? []).Distinct())
            db.MenuItemLocations.Add(new MenuItemLocation { MenuItemId = item.Id, LocationId = loc });
    }
}
