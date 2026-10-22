using Microsoft.EntityFrameworkCore;
using Yurt.Application.Common.Helpers;
using Yurt.Application.Common.Interfaces;
using Yurt.Application.Common.Models;
using Yurt.Application.Features.Menu.DTOs;
using Yurt.Application.Features.Stock;
using Yurt.Domain.Entities;

namespace Yurt.Application.Features.Menu.Services;

public class MenuService
{
    private readonly IApplicationDbContext _db;
    private readonly IAuditLogService _audit;
    private readonly IMenuCacheService _menuCache;
    private readonly StockService? _stock;

    public MenuService(IApplicationDbContext db, IAuditLogService audit, IMenuCacheService menuCache, StockService? stock = null)
    {
        _stock = stock;
        _db = db;
        _audit = audit;
        _menuCache = menuCache;
    }

    // ── Customer endpoints (localized) ────────────────────────────────────────

    public async Task<List<MenuCategoryDto>> GetCategoriesAsync(
        string lang = "ru", Guid? locationId = null, CancellationToken ct = default)
    {
        var cacheKey = $"cats:{lang}:{locationId}";
        var cached = await _menuCache.GetAsync<List<MenuCategoryDto>>(cacheKey);
        if (cached is not null) return cached;

        var query = _db.MenuCategories.AsQueryable();

        if (locationId.HasValue)
        {
            var soldOut = await SoldOutAsync(locationId, ct);
            query = query.Where(c => c.Items.Any(i =>
                i.IsAvailable && !soldOut.Contains(i.Id) &&
                (!i.MenuItemLocations.Any() || i.MenuItemLocations.Any(l => l.LocationId == locationId.Value))));
        }

        var result = await query
            .OrderBy(c => c.SortOrder).ThenBy(c => c.Name)
            .Select(c => new MenuCategoryDto(
                c.Id,
                LocalizationHelper.Localize(c.Name, c.NameRu, c.NameKk, lang),
                c.SortOrder))
            .ToListAsync(ct);

        await _menuCache.SetAsync(cacheKey, result);
        return result;
    }

    public async Task<List<MenuItemDto>> GetItemsAsync(
        Guid? categoryId, string? search, string lang = "ru", Guid? locationId = null, CancellationToken ct = default)
    {
        // Only cache unfilitered/unasearched browsing — search results are not cached
        var cacheKey = string.IsNullOrWhiteSpace(search)
            ? $"items:{lang}:{categoryId}:{locationId}"
            : null;

        if (cacheKey is not null)
        {
            var cached = await _menuCache.GetAsync<List<MenuItemDto>>(cacheKey);
            if (cached is not null) return cached;
        }

        var query = _db.MenuItems
            .Include(i => i.Category)
            .Include(i => i.MenuItemLocations)
            .Include(i => i.Variants)
            .Where(i => i.IsAvailable)
            .AsQueryable();

        if (categoryId.HasValue)
            query = query.Where(i => i.CategoryId == categoryId.Value);

        if (!string.IsNullOrWhiteSpace(search))
        {
            var term = search.Trim().ToLower();
            query = query.Where(i =>
                i.Name.ToLower().Contains(term) ||
                (i.NameRu != null && i.NameRu.ToLower().Contains(term)) ||
                (i.NameKk != null && i.NameKk.ToLower().Contains(term)) ||
                i.Description.ToLower().Contains(term));
        }

        if (locationId.HasValue)
        {
            var soldOut = await SoldOutAsync(locationId, ct);
            query = query.Where(i =>
                !soldOut.Contains(i.Id) &&
                (!i.MenuItemLocations.Any() ||
                i.MenuItemLocations.Any(l => l.LocationId == locationId.Value)));
        }

        var items = await query
            .OrderBy(i => i.Category.SortOrder).ThenBy(i => i.SortOrder).ThenBy(i => i.Name)
            .ToListAsync(ct);

        var categoryIds = items.Select(i => i.CategoryId).Distinct().ToList();
        var toppingsByCategory = await GetToppingsByCategoryIdsAsync(categoryIds, lang, locationId, ct);

        var result = items.Select(i => MapItemToDto(i, lang,
            toppingsByCategory.TryGetValue(i.CategoryId, out var t) ? t : null)).ToList();

        if (cacheKey is not null) await _menuCache.SetAsync(cacheKey, result);
        return result;
    }

    public async Task<Result<MenuItemDto>> GetItemByIdAsync(Guid id, string lang = "ru", Guid? locationId = null, CancellationToken ct = default)
    {
        var item = await _db.MenuItems
            .Include(i => i.Category)
            .Include(i => i.MenuItemLocations)
            .Include(i => i.Variants)
            .FirstOrDefaultAsync(i => i.Id == id, ct);

        if (item == null) return Result<MenuItemDto>.NotFound();

        if (locationId.HasValue && item.MenuItemLocations.Any() &&
            !item.MenuItemLocations.Any(l => l.LocationId == locationId.Value))
            return Result<MenuItemDto>.Failure("This item is not available at your selected location.", 403);

        var toppingsByCategory = await GetToppingsByCategoryIdsAsync([item.CategoryId], lang, locationId, ct);
        toppingsByCategory.TryGetValue(item.CategoryId, out var toppings);
        return Result<MenuItemDto>.Success(MapItemToDto(item, lang, toppings));
    }

    public async Task<List<MenuToppingDto>> GetToppingsAsync(
        Guid? categoryId, string lang = "ru", CancellationToken ct = default)
    {
        var cacheKey = $"tops:{lang}:{categoryId}";
        var cached = await _menuCache.GetAsync<List<MenuToppingDto>>(cacheKey);
        if (cached is not null) return cached;

        var query = _db.MenuToppings
            .Include(t => t.ToppingCategories)
            .AsQueryable();

        if (categoryId.HasValue)
            query = query.Where(t =>
                t.ToppingCategories.Any(tc => tc.CategoryId == categoryId.Value));

        var toppings = await query.OrderBy(t => t.Group).ThenBy(t => t.Name).ToListAsync(ct);

        var result = toppings.Select(t => new MenuToppingDto(
            t.Id,
            LocalizationHelper.Localize(t.Name, t.NameRu, t.NameKk, lang),
            t.Price, t.IsAvailable,
            t.ToppingCategories.Select(tc => tc.CategoryId).ToList(),
            t.Group)).ToList();

        await _menuCache.SetAsync(cacheKey, result);
        return result;
    }

    // ── Admin endpoints (all fields exposed) ──────────────────────────────────

    public async Task<List<AdminMenuCategoryDto>> AdminGetCategoriesAsync(CancellationToken ct = default)
        => await _db.MenuCategories
            .OrderBy(c => c.SortOrder).ThenBy(c => c.Name)
            .Select(c => new AdminMenuCategoryDto(c.Id, c.Name, c.NameRu, c.NameKk, c.SortOrder))
            .ToListAsync(ct);

    public async Task<List<AdminMenuItemDto>> AdminGetItemsAsync(CancellationToken ct = default)
    {
        var items = await _db.MenuItems
            .Include(i => i.Category)
            .Include(i => i.MenuItemLocations)
            .Include(i => i.Variants)
            .OrderBy(i => i.Category.SortOrder).ThenBy(i => i.SortOrder).ThenBy(i => i.Name)
            .ToListAsync(ct);

        return items.Select(i => MapAdminItemToDto(i)).ToList();
    }

    public async Task<List<AdminMenuToppingDto>> AdminGetToppingsAsync(CancellationToken ct = default)
    {
        var toppings = await _db.MenuToppings
            .Include(t => t.ToppingCategories)
            .OrderBy(t => t.Group).ThenBy(t => t.Name)
            .ToListAsync(ct);

        return toppings.Select(t => new AdminMenuToppingDto(
            t.Id, t.Name, t.NameRu, t.NameKk, t.Price, t.IsAvailable,
            t.ToppingCategories.Select(tc => tc.CategoryId).ToList(), t.Group)).ToList();
    }

    // ── Category CRUD ─────────────────────────────────────────────────────────

    public async Task<Result<AdminMenuCategoryDto>> CreateCategoryAsync(
        CreateCategoryDto dto, CancellationToken ct = default)
    {
        var cat = new MenuCategory
        {
            Name = dto.Name,
            NameRu = dto.NameRu,
            NameKk = dto.NameKk,
            SortOrder = dto.SortOrder
        };
        _db.MenuCategories.Add(cat);
        await _db.SaveChangesAsync(ct);
        await _audit.LogAsync("CategoryCreated", "MenuCategory", cat.Id.ToString(), cat.Name, ct);
        await _menuCache.InvalidateAllMenuAsync();
        return Result<AdminMenuCategoryDto>.Success(
            new AdminMenuCategoryDto(cat.Id, cat.Name, cat.NameRu, cat.NameKk, cat.SortOrder), 201);
    }

    public async Task<Result<AdminMenuCategoryDto>> UpdateCategoryAsync(
        Guid id, UpdateCategoryDto dto, CancellationToken ct = default)
    {
        var cat = await _db.MenuCategories.FindAsync([id], ct);
        if (cat == null) return Result<AdminMenuCategoryDto>.NotFound();

        cat.Name = dto.Name;
        cat.NameRu = dto.NameRu;
        cat.NameKk = dto.NameKk;
        cat.SortOrder = dto.SortOrder;
        cat.UpdatedAt = DateTime.UtcNow;
        await _db.SaveChangesAsync(ct);
        await _audit.LogAsync("CategoryUpdated", "MenuCategory", id.ToString(), cat.Name, ct);
        await _menuCache.InvalidateAllMenuAsync();
        return Result<AdminMenuCategoryDto>.Success(
            new AdminMenuCategoryDto(cat.Id, cat.Name, cat.NameRu, cat.NameKk, cat.SortOrder));
    }

    public async Task<Result<bool>> DeleteCategoryAsync(Guid id, CancellationToken ct = default)
    {
        var cat = await _db.MenuCategories.FindAsync([id], ct);
        if (cat == null) return Result<bool>.NotFound();
        var name = cat.Name;
        _db.MenuCategories.Remove(cat);
        await _db.SaveChangesAsync(ct);
        await _audit.LogAsync("CategoryDeleted", "MenuCategory", id.ToString(), name, ct);
        await _menuCache.InvalidateAllMenuAsync();
        return Result<bool>.Success(true);
    }

    public async Task<Result<bool>> ReorderCategoriesAsync(List<Guid> orderedIds, CancellationToken ct = default)
    {
        var cats = await _db.MenuCategories.Where(c => orderedIds.Contains(c.Id)).ToListAsync(ct);
        for (var i = 0; i < orderedIds.Count; i++)
        {
            var cat = cats.FirstOrDefault(c => c.Id == orderedIds[i]);
            if (cat == null) continue;
            cat.SortOrder = i;
            cat.UpdatedAt = DateTime.UtcNow;
        }

        await _db.SaveChangesAsync(ct);
        await _audit.LogAsync("CategoriesReordered", "MenuCategory", "", $"{cats.Count} categories", ct);
        await _menuCache.InvalidateAllMenuAsync();
        return Result<bool>.Success(true);
    }

    // ── MenuItem CRUD ─────────────────────────────────────────────────────────

    public async Task<Result<AdminMenuItemDto>> CreateItemAsync(
        CreateMenuItemDto dto, CancellationToken ct = default)
    {
        var cat = await _db.MenuCategories.FindAsync([dto.CategoryId], ct);
        if (cat == null)
            return Result<AdminMenuItemDto>.Failure("Category not found.", 400);

        var nextSortOrder = await _db.MenuItems.CountAsync(i => i.CategoryId == dto.CategoryId, ct);

        var item = new MenuItem
        {
            CategoryId = dto.CategoryId,
            Name = dto.Name,
            NameRu = dto.NameRu,
            NameKk = dto.NameKk,
            Description = dto.Description ?? string.Empty,
            DescriptionRu = dto.DescriptionRu,
            DescriptionKk = dto.DescriptionKk,
            Price = dto.Price,
            IsAvailable = dto.IsAvailable,
            IsNew = dto.IsNew,
            ImageUrl = dto.ImageUrl,
            SortOrder = nextSortOrder,
            IikoProductId = dto.IikoProductId,
            IikoProductSizeId = dto.IikoProductSizeId
        };
        _db.MenuItems.Add(item);

        foreach (var locId in dto.LocationIds?.Distinct() ?? [])
            _db.MenuItemLocations.Add(new MenuItemLocation { MenuItemId = item.Id, LocationId = locId });

        foreach (var v in dto.Variants ?? [])
            _db.MenuItemVariants.Add(new MenuItemVariant
            {
                MenuItemId = item.Id,
                Label = v.Label,
                LabelRu = v.LabelRu,
                LabelKk = v.LabelKk,
                Price = v.Price,
                SortOrder = v.SortOrder,
                IsDefault = v.IsDefault,
                IikoProductSizeId = v.IikoProductSizeId
            });

        await _db.SaveChangesAsync(ct);

        item.Category = cat;
        item.MenuItemLocations = [.. (dto.LocationIds?.Distinct().Select(l => new MenuItemLocation { MenuItemId = item.Id, LocationId = l }) ?? [])];
        item.Variants = await _db.MenuItemVariants.Where(v => v.MenuItemId == item.Id).OrderBy(v => v.SortOrder).ToListAsync(ct);
        await _audit.LogAsync("MenuItemCreated", "MenuItem", item.Id.ToString(), item.Name, ct);
        await _menuCache.InvalidateAllMenuAsync();
        return Result<AdminMenuItemDto>.Success(MapAdminItemToDto(item), 201);
    }

    public async Task<Result<AdminMenuItemDto>> UpdateItemAsync(
        Guid id, UpdateMenuItemDto dto, CancellationToken ct = default)
    {
        var item = await _db.MenuItems
            .Include(i => i.Category)
            .Include(i => i.MenuItemLocations)
            .Include(i => i.Variants)
            .FirstOrDefaultAsync(i => i.Id == id, ct);

        if (item == null) return Result<AdminMenuItemDto>.NotFound();
        if (item.CategoryId != dto.CategoryId && !await _db.MenuCategories.AnyAsync(c => c.Id == dto.CategoryId, ct))
            return Result<AdminMenuItemDto>.Failure("Category not found.", 400);

        item.CategoryId = dto.CategoryId;
        item.Name = dto.Name;
        item.NameRu = dto.NameRu;
        item.NameKk = dto.NameKk;
        item.Description = dto.Description ?? string.Empty;
        item.DescriptionRu = dto.DescriptionRu;
        item.DescriptionKk = dto.DescriptionKk;
        item.Price = dto.Price;
        item.IsAvailable = dto.IsAvailable;
        item.IsNew = dto.IsNew;
        item.ImageUrl = dto.ImageUrl;
        item.IikoProductId = dto.IikoProductId;
        item.IikoProductSizeId = dto.IikoProductSizeId;
        item.UpdatedAt = DateTime.UtcNow;

        foreach (var link in item.MenuItemLocations.ToList())
            _db.MenuItemLocations.Remove(link);

        foreach (var locId in dto.LocationIds?.Distinct() ?? [])
            _db.MenuItemLocations.Add(new MenuItemLocation { MenuItemId = item.Id, LocationId = locId });

        foreach (var v in item.Variants.ToList())
            _db.MenuItemVariants.Remove(v);

        foreach (var v in dto.Variants ?? [])
            _db.MenuItemVariants.Add(new MenuItemVariant
            {
                MenuItemId = item.Id,
                Label = v.Label,
                LabelRu = v.LabelRu,
                LabelKk = v.LabelKk,
                Price = v.Price,
                SortOrder = v.SortOrder,
                IsDefault = v.IsDefault,
                IikoProductSizeId = v.IikoProductSizeId
            });

        await _db.SaveChangesAsync(ct);
        item.Variants = await _db.MenuItemVariants.Where(v => v.MenuItemId == item.Id).OrderBy(v => v.SortOrder).ToListAsync(ct);
        await _audit.LogAsync("MenuItemUpdated", "MenuItem", id.ToString(), item.Name, ct);
        await _menuCache.InvalidateAllMenuAsync();
        return Result<AdminMenuItemDto>.Success(MapAdminItemToDto(item));
    }

    public async Task<Result<bool>> DeleteItemAsync(Guid id, CancellationToken ct = default)
    {
        var item = await _db.MenuItems.FindAsync([id], ct);
        if (item == null) return Result<bool>.NotFound();
        var name = item.Name;
        _db.MenuItems.Remove(item);
        await _db.SaveChangesAsync(ct);
        await _audit.LogAsync("MenuItemDeleted", "MenuItem", id.ToString(), name, ct);
        await _menuCache.InvalidateAllMenuAsync();
        return Result<bool>.Success(true);
    }

    public async Task<Result<bool>> ReorderItemsAsync(Guid categoryId, List<Guid> orderedIds, CancellationToken ct = default)
    {
        var items = await _db.MenuItems
            .Where(i => i.CategoryId == categoryId && orderedIds.Contains(i.Id))
            .ToListAsync(ct);

        for (var i = 0; i < orderedIds.Count; i++)
        {
            var item = items.FirstOrDefault(x => x.Id == orderedIds[i]);
            if (item == null) continue;
            item.SortOrder = i;
            item.UpdatedAt = DateTime.UtcNow;
        }

        await _db.SaveChangesAsync(ct);
        await _audit.LogAsync("MenuItemsReordered", "MenuItem", categoryId.ToString(), $"{items.Count} items", ct);
        await _menuCache.InvalidateAllMenuAsync();
        return Result<bool>.Success(true);
    }

    // ── Topping CRUD ──────────────────────────────────────────────────────────

    public async Task<Result<AdminMenuToppingDto>> CreateToppingAsync(
        CreateToppingDto dto, CancellationToken ct = default)
    {
        var topping = new MenuTopping
        {
            Name = dto.Name,
            NameRu = dto.NameRu,
            NameKk = dto.NameKk,
            Price = dto.Price,
            IsAvailable = dto.IsAvailable,
            Group = string.IsNullOrWhiteSpace(dto.Group) ? null : dto.Group
        };
        _db.MenuToppings.Add(topping);

        foreach (var catId in dto.CategoryIds.Distinct())
            _db.MenuToppingCategories.Add(new MenuToppingCategory
            {
                ToppingId = topping.Id,
                CategoryId = catId
            });

        await _db.SaveChangesAsync(ct);
        await _audit.LogAsync("ToppingCreated", "MenuTopping", topping.Id.ToString(), topping.Name, ct);
        await _menuCache.InvalidateAllMenuAsync();
        return Result<AdminMenuToppingDto>.Success(
            new AdminMenuToppingDto(topping.Id, topping.Name, topping.NameRu, topping.NameKk,
                topping.Price, topping.IsAvailable, dto.CategoryIds.Distinct().ToList(), topping.Group), 201);
    }

    public async Task<Result<AdminMenuToppingDto>> UpdateToppingAsync(
        Guid id, UpdateToppingDto dto, CancellationToken ct = default)
    {
        var topping = await _db.MenuToppings
            .Include(t => t.ToppingCategories)
            .FirstOrDefaultAsync(t => t.Id == id, ct);

        if (topping == null) return Result<AdminMenuToppingDto>.NotFound();

        topping.Name = dto.Name;
        topping.NameRu = dto.NameRu;
        topping.NameKk = dto.NameKk;
        topping.Price = dto.Price;
        topping.IsAvailable = dto.IsAvailable;
        topping.Group = string.IsNullOrWhiteSpace(dto.Group) ? null : dto.Group;
        topping.UpdatedAt = DateTime.UtcNow;

        foreach (var link in topping.ToppingCategories.ToList())
            _db.MenuToppingCategories.Remove(link);

        foreach (var catId in dto.CategoryIds.Distinct())
            _db.MenuToppingCategories.Add(new MenuToppingCategory
            {
                ToppingId = topping.Id,
                CategoryId = catId
            });

        await _db.SaveChangesAsync(ct);
        await _audit.LogAsync("ToppingUpdated", "MenuTopping", id.ToString(), topping.Name, ct);
        await _menuCache.InvalidateAllMenuAsync();
        return Result<AdminMenuToppingDto>.Success(
            new AdminMenuToppingDto(topping.Id, topping.Name, topping.NameRu, topping.NameKk,
                topping.Price, topping.IsAvailable, dto.CategoryIds.Distinct().ToList(), topping.Group));
    }

    public async Task<Result<bool>> DeleteToppingAsync(Guid id, CancellationToken ct = default)
    {
        var topping = await _db.MenuToppings.FindAsync([id], ct);
        if (topping == null) return Result<bool>.NotFound();
        var name = topping.Name;
        _db.MenuToppings.Remove(topping);
        await _db.SaveChangesAsync(ct);
        await _audit.LogAsync("ToppingDeleted", "MenuTopping", id.ToString(), name, ct);
        await _menuCache.InvalidateAllMenuAsync();
        return Result<bool>.Success(true);
    }

    // ── Private helpers ───────────────────────────────────────────────────────

    private async Task<Dictionary<Guid, List<MenuToppingDto>>> GetToppingsByCategoryIdsAsync(
        List<Guid> categoryIds, string lang, Guid? locationId, CancellationToken ct)
    {
        var links = await _db.MenuToppingCategories
            .Include(tc => tc.Topping)
                .ThenInclude(t => t.ToppingCategories)
            .Where(tc => categoryIds.Contains(tc.CategoryId) && tc.Topping.IsAvailable)
            .ToListAsync(ct);

        // A topping whose ingredient has run out at this branch is left off the menu there.
        if (_stock is not null && locationId is not null)
        {
            var soldOutToppings = await _stock.SoldOutToppingIdsAsync(locationId.Value, ct);
            if (soldOutToppings.Count > 0) links.RemoveAll(l => soldOutToppings.Contains(l.ToppingId));
        }

        var result = new Dictionary<Guid, List<MenuToppingDto>>();
        foreach (var link in links)
        {
            if (!result.TryGetValue(link.CategoryId, out var list))
            {
                list = new List<MenuToppingDto>();
                result[link.CategoryId] = list;
            }

            var allCatIds = link.Topping.ToppingCategories?
                .Select(tc => tc.CategoryId).ToList() ?? [link.CategoryId];
            list.Add(new MenuToppingDto(
                link.Topping.Id,
                LocalizationHelper.Localize(link.Topping.Name, link.Topping.NameRu, link.Topping.NameKk, lang),
                link.Topping.Price, link.Topping.IsAvailable, allCatIds, link.Topping.Group));
        }
        return result;
    }

    /// <summary>Items at zero stock in the branch (the automatic stop-list); empty without stock counting.</summary>
    private async Task<List<Guid>> SoldOutAsync(Guid? locationId, CancellationToken ct)
        => _stock is null || locationId is null ? [] : await _stock.SoldOutItemIdsAsync(locationId.Value, ct);

    private static MenuItemDto MapItemToDto(MenuItem i, string lang, List<MenuToppingDto>? toppings = null)
    {
        var locationIds = i.MenuItemLocations?.Select(l => l.LocationId).ToList();
        var variants = i.Variants?.Count > 0
            ? i.Variants.OrderBy(v => v.SortOrder)
                .Select(v => new MenuItemVariantDto(v.Id,
                    LocalizationHelper.Localize(v.Label, v.LabelRu, v.LabelKk, lang),
                    v.Price, v.SortOrder, v.IsDefault)).ToList()
            : null;
        return new(i.Id, i.CategoryId, i.Category?.Name ?? "",
               LocalizationHelper.Localize(i.Name, i.NameRu, i.NameKk, lang),
               LocalizationHelper.Localize(i.Description, i.DescriptionRu, i.DescriptionKk, lang),
               i.Price, i.IsAvailable, i.IsNew, i.ImageUrl, i.CreatedAt, locationIds, toppings, variants,
               i.Name, i.NameRu, i.NameKk);
    }

    private static AdminMenuItemDto MapAdminItemToDto(MenuItem i, List<MenuToppingDto>? toppings = null)
    {
        var locationIds = i.MenuItemLocations?.Select(l => l.LocationId).ToList();
        var variants = i.Variants?.Count > 0
            ? i.Variants.OrderBy(v => v.SortOrder)
                .Select(v => new AdminMenuItemVariantDto(v.Id, v.Label, v.LabelRu, v.LabelKk, v.Price, v.SortOrder, v.IsDefault, v.IikoProductSizeId)).ToList()
            : null;
        return new(i.Id, i.CategoryId, i.Category?.Name ?? "",
               i.Name, i.NameRu, i.NameKk,
               i.Description, i.DescriptionRu, i.DescriptionKk,
               i.Price, i.IsAvailable, i.IsNew, i.ImageUrl, i.SortOrder, locationIds, toppings, variants,
               i.IikoProductId, i.IikoProductSizeId);
    }
}
