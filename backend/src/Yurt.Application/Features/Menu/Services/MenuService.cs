using Microsoft.EntityFrameworkCore;
using Yurt.Application.Common.Helpers;
using Yurt.Application.Common.Interfaces;
using Yurt.Application.Common.Models;
using Yurt.Application.Features.Combos;
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
        // Time menus: a dish or category tied to a schedule shows only while it runs, so the cache is per running set.
        var timed = await TimedMenu.LoadAsync(_db, locationId, DateTime.UtcNow, ct);
        var cacheKey = $"cats:{lang}:{locationId}:{timed.Signature}";
        var cached = await _menuCache.GetAsync<List<MenuCategoryDto>>(cacheKey);
        if (cached is not null) return cached;

        var query = _db.MenuCategories.AsQueryable();
        var hidden = timed.HiddenScheduleIds();

        if (hidden.Count > 0)
            query = query.Where(c => c.ScheduleId == null || !hidden.Contains(c.ScheduleId.Value));

        if (locationId.HasValue || hidden.Count > 0)
        {
            var soldOut = await SoldOutAsync(locationId, ct);
            query = query.Where(c => c.Items.Any(i =>
                i.IsAvailable && !soldOut.Contains(i.Id) &&
                (i.ScheduleId == null || !hidden.Contains(i.ScheduleId.Value)) &&
                (locationId == null || !i.MenuItemLocations.Any() || i.MenuItemLocations.Any(l => l.LocationId == locationId.Value))));
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
        var timed = await TimedMenu.LoadAsync(_db, locationId, DateTime.UtcNow, ct);
        // Only cache unfilitered/unasearched browsing — search results are not cached
        var cacheKey = string.IsNullOrWhiteSpace(search)
            ? $"items:{lang}:{categoryId}:{locationId}:{timed.Signature}"
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

        var items = (await query
            .OrderBy(i => i.Category.SortOrder).ThenBy(i => i.SortOrder).ThenBy(i => i.Name)
            .ToListAsync(ct)).Where(timed.IsOnMenu).ToList();

        // A combo is off the menu while a dish inside it is on the stop-list or not sold at this branch.
        var combos = await ComboSales.LoadAsync(_db, items, ct);
        items = items.Where(i => !i.IsCombo || combos.Problem(i, locationId) is null).ToList();

        var options = await DishOptionsForGuestAsync(items, lang, locationId, ct);
        var result = items.Select(i => MapItemToDto(i, lang, options.Toppings[i.Id], options.Rules[i.Id], locationId, combos, timed)).ToList();

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

        var timed = await TimedMenu.LoadAsync(_db, locationId, DateTime.UtcNow, ct);
        if (!timed.IsOnMenu(item))
            return Result<MenuItemDto>.Failure("This item is not on the menu at this time.", 403);

        var combos = await ComboSales.LoadAsync(_db, [item], ct);
        var options = await DishOptionsForGuestAsync([item], lang, locationId, ct);
        return Result<MenuItemDto>.Success(MapItemToDto(item, lang, options.Toppings[item.Id], options.Rules[item.Id], locationId, combos, timed));
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
            .Include(i => i.ToppingLinks)
            .Where(i => !i.IsCombo) // combos are managed on their own screen
            .OrderBy(i => i.Category.SortOrder).ThenBy(i => i.SortOrder).ThenBy(i => i.Name)
            .ToListAsync(ct);

        return items.Select(i => MapAdminItemToDto(i)).ToList();
    }

    public async Task<List<AdminMenuToppingDto>> AdminGetToppingsAsync(CancellationToken ct = default)
    {
        var toppings = await _db.MenuToppings
            .Include(t => t.ToppingCategories)
            .Include(t => t.LocationPrices)
            .OrderBy(t => t.Group).ThenBy(t => t.Name)
            .ToListAsync(ct);

        return toppings.Select(MapAdminTopping).ToList();
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
        Nutrition.Apply(item, dto.Nutrition);
        _db.MenuItems.Add(item);

        var newLinks = (dto.LocationIds?.Distinct() ?? [])
            .Select(l => new MenuItemLocation { MenuItemId = item.Id, LocationId = l, Price = PriceAt(dto.LocationPrices, l) }).ToList();
        foreach (var link in newLinks) _db.MenuItemLocations.Add(link);

        var topIds = await ValidToppingIdsAsync(dto.ToppingIds, ct);
        foreach (var tid in topIds)
            _db.MenuItemToppings.Add(new MenuItemTopping { MenuItemId = item.Id, ToppingId = tid });

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
        item.MenuItemLocations = newLinks;
        item.ToppingLinks = topIds.Select(t => new MenuItemTopping { MenuItemId = item.Id, ToppingId = t }).ToList();
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
            .Include(i => i.ToppingLinks)
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
        // Not sent (older admin app) = leave as it was.
        if (dto.Nutrition is not null) Nutrition.Apply(item, dto.Nutrition);
        item.UpdatedAt = DateTime.UtcNow;

        // Branch prices not sent are kept as they were.
        var oldPrices = item.MenuItemLocations.Where(l => l.Price != null).ToDictionary(l => l.LocationId, l => l.Price);
        foreach (var link in item.MenuItemLocations.ToList())
            _db.MenuItemLocations.Remove(link);

        foreach (var locId in dto.LocationIds?.Distinct() ?? [])
            _db.MenuItemLocations.Add(new MenuItemLocation
            {
                MenuItemId = item.Id, LocationId = locId,
                Price = dto.LocationPrices is null ? oldPrices.GetValueOrDefault(locId) : PriceAt(dto.LocationPrices, locId)
            });

        if (dto.ToppingIds is not null)
        {
            foreach (var link in item.ToppingLinks.ToList()) _db.MenuItemToppings.Remove(link);
            foreach (var tid in await ValidToppingIdsAsync(dto.ToppingIds, ct))
                _db.MenuItemToppings.Add(new MenuItemTopping { MenuItemId = item.Id, ToppingId = tid });
        }

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
        item.MenuItemLocations = await _db.MenuItemLocations.Where(l => l.MenuItemId == item.Id).ToListAsync(ct);
        item.ToppingLinks = await _db.MenuItemToppings.Where(l => l.MenuItemId == item.Id).ToListAsync(ct);
        await _audit.LogAsync("MenuItemUpdated", "MenuItem", id.ToString(), item.Name, ct);
        await _menuCache.InvalidateAllMenuAsync();
        return Result<AdminMenuItemDto>.Success(MapAdminItemToDto(item));
    }

    public async Task<Result<bool>> DeleteItemAsync(Guid id, CancellationToken ct = default)
    {
        var item = await _db.MenuItems.FindAsync([id], ct);
        if (item == null) return Result<bool>.NotFound();
        var inCombo = await _db.ComboParts.Where(p => p.ItemId == id).Select(p => p.Combo.Name).FirstOrDefaultAsync(ct);
        if (inCombo is not null)
            return Result<bool>.Failure($"'{item.Name}' is part of the combo '{inCombo}'. Take it out of the combo first.", 409);
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

        foreach (var lp in await ValidLocationPricesAsync(dto.LocationPrices, ct))
            _db.MenuToppingLocations.Add(new MenuToppingLocation { ToppingId = topping.Id, LocationId = lp.LocationId, Price = lp.Price });

        await _db.SaveChangesAsync(ct);
        await _audit.LogAsync("ToppingCreated", "MenuTopping", topping.Id.ToString(), topping.Name, ct);
        await _menuCache.InvalidateAllMenuAsync();
        return Result<AdminMenuToppingDto>.Success(
            new AdminMenuToppingDto(topping.Id, topping.Name, topping.NameRu, topping.NameKk,
                topping.Price, topping.IsAvailable, dto.CategoryIds.Distinct().ToList(), topping.Group,
                await ValidLocationPricesAsync(dto.LocationPrices, ct)), 201);
    }

    public async Task<Result<AdminMenuToppingDto>> UpdateToppingAsync(
        Guid id, UpdateToppingDto dto, CancellationToken ct = default)
    {
        var topping = await _db.MenuToppings
            .Include(t => t.ToppingCategories)
            .Include(t => t.LocationPrices)
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

        if (dto.LocationPrices is not null)
        {
            foreach (var lp in topping.LocationPrices.ToList()) _db.MenuToppingLocations.Remove(lp);
            foreach (var lp in await ValidLocationPricesAsync(dto.LocationPrices, ct))
                _db.MenuToppingLocations.Add(new MenuToppingLocation { ToppingId = topping.Id, LocationId = lp.LocationId, Price = lp.Price });
        }

        await _db.SaveChangesAsync(ct);
        await _audit.LogAsync("ToppingUpdated", "MenuTopping", id.ToString(), topping.Name, ct);
        await _menuCache.InvalidateAllMenuAsync();
        topping.LocationPrices = await _db.MenuToppingLocations.Where(l => l.ToppingId == id).ToListAsync(ct);
        return Result<AdminMenuToppingDto>.Success(MapAdminTopping(topping));
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

    // ── Option group rules ────────────────────────────────────────────────────

    public async Task<List<AdminOptionGroupDto>> AdminGetOptionGroupsAsync(CancellationToken ct = default)
        => await _db.MenuOptionGroups.OrderBy(g => g.Key)
            .Select(g => new AdminOptionGroupDto(g.Key, g.Title, g.TitleRu, g.TitleKk, g.Required, g.MinSelect, g.MaxSelect))
            .ToListAsync(ct);

    public async Task<Result<AdminOptionGroupDto>> SaveOptionGroupAsync(AdminOptionGroupDto dto, CancellationToken ct = default)
    {
        var key = dto.Key?.Trim() ?? "";
        if (key.Length is 0 or > 100) return Result<AdminOptionGroupDto>.Failure("The group needs a name (up to 100 characters).", 400);
        if (dto.MinSelect < 0 || dto.MaxSelect is < 1)
            return Result<AdminOptionGroupDto>.Failure("The smallest number cannot be below 0 and the largest not below 1.", 400);
        var min = Math.Max(dto.MinSelect, dto.Required ? 1 : 0);
        if (dto.MaxSelect is { } mx && mx < min)
            return Result<AdminOptionGroupDto>.Failure("The largest number cannot be smaller than the smallest.", 400);

        var row = await _db.MenuOptionGroups.FirstOrDefaultAsync(g => g.Key == key, ct);
        var created = row is null;
        if (row is null) { row = new MenuOptionGroup { Key = key }; _db.MenuOptionGroups.Add(row); }
        row.Title = string.IsNullOrWhiteSpace(dto.Title) ? null : dto.Title.Trim();
        row.TitleRu = string.IsNullOrWhiteSpace(dto.TitleRu) ? null : dto.TitleRu.Trim();
        row.TitleKk = string.IsNullOrWhiteSpace(dto.TitleKk) ? null : dto.TitleKk.Trim();
        row.Required = dto.Required || min > 0;
        row.MinSelect = min;
        row.MaxSelect = dto.MaxSelect;
        row.UpdatedAt = DateTime.UtcNow;
        await _db.SaveChangesAsync(ct);
        await _audit.LogAsync("OptionGroupSaved", "MenuOptionGroup", row.Id.ToString(), key, ct);
        await _menuCache.InvalidateAllMenuAsync();
        return Result<AdminOptionGroupDto>.Success(
            new AdminOptionGroupDto(row.Key, row.Title, row.TitleRu, row.TitleKk, row.Required, row.MinSelect, row.MaxSelect),
            created ? 201 : 200);
    }

    public async Task<Result<bool>> DeleteOptionGroupAsync(string key, CancellationToken ct = default)
    {
        var row = await _db.MenuOptionGroups.FirstOrDefaultAsync(g => g.Key == key, ct);
        if (row is null) return Result<bool>.NotFound();
        _db.MenuOptionGroups.Remove(row);
        await _db.SaveChangesAsync(ct);
        await _audit.LogAsync("OptionGroupDeleted", "MenuOptionGroup", row.Id.ToString(), key, ct);
        await _menuCache.InvalidateAllMenuAsync();
        return Result<bool>.Success(true);
    }

    // ── Private helpers ───────────────────────────────────────────────────────

    private async Task<(Dictionary<Guid, List<MenuToppingDto>> Toppings, Dictionary<Guid, List<OptionRuleDto>> Rules)>
        DishOptionsForGuestAsync(IReadOnlyCollection<MenuItem> items, string lang, Guid? locationId, CancellationToken ct)
    {
        // A topping whose ingredient has run out at this branch is left off the menu there.
        var soldOut = _stock is not null && locationId is not null
            ? await _stock.SoldOutToppingIdsAsync(locationId.Value, ct) : [];
        var offered = await DishOptions.LoadAsync(_db, items, locationId, soldOut, false, ct);
        var groups = await DishOptions.LoadGroupsAsync(_db, offered.Values.SelectMany(v => v).Select(o => o.Topping.Group), ct);
        var categoryIds = await _db.MenuToppingCategories
            .Select(c => new { c.ToppingId, c.CategoryId }).ToListAsync(ct);
        var catsByTopping = categoryIds.ToLookup(c => c.ToppingId, c => c.CategoryId);

        var toppings = new Dictionary<Guid, List<MenuToppingDto>>();
        var rules = new Dictionary<Guid, List<OptionRuleDto>>();
        foreach (var item in items)
        {
            var list = offered[item.Id];
            toppings[item.Id] = list.Select(o => new MenuToppingDto(
                o.Topping.Id,
                LocalizationHelper.Localize(o.Topping.Name, o.Topping.NameRu, o.Topping.NameKk, lang),
                o.Price, o.Topping.IsAvailable, catsByTopping[o.Topping.Id].ToList(), o.Topping.Group)).ToList();
            rules[item.Id] = list.Where(o => o.Topping.Group != null).Select(o => o.Topping.Group!).Distinct()
                .Select(k => DishOptions.RuleFor(k, groups.GetValueOrDefault(k), lang))
                .Select(r => new OptionRuleDto(r.Key, r.Title, r.Required, r.Min, r.Max)).ToList();
        }
        return (toppings, rules);
    }

    private static decimal? PriceAt(List<LocationPriceDto>? prices, Guid locationId)
        => prices?.FirstOrDefault(p => p.LocationId == locationId)?.Price;

    /// <summary>Only toppings of this cafe, no repeats.</summary>
    private async Task<List<Guid>> ValidToppingIdsAsync(List<Guid>? ids, CancellationToken ct)
    {
        var wanted = ids?.Distinct().ToList() ?? [];
        return wanted.Count == 0 ? [] : await _db.MenuToppings.Where(t => wanted.Contains(t.Id)).Select(t => t.Id).ToListAsync(ct);
    }

    private async Task<List<LocationPriceDto>> ValidLocationPricesAsync(List<LocationPriceDto>? prices, CancellationToken ct)
    {
        if (prices is not { Count: > 0 }) return [];
        var ids = prices.Select(p => p.LocationId).Distinct().ToList();
        var known = await _db.Locations.Where(l => ids.Contains(l.Id)).Select(l => l.Id).ToListAsync(ct);
        return prices.Where(p => known.Contains(p.LocationId) && p.Price >= 0)
            .GroupBy(p => p.LocationId).Select(g => g.Last()).ToList();
    }

    private static AdminMenuToppingDto MapAdminTopping(MenuTopping t) => new(
        t.Id, t.Name, t.NameRu, t.NameKk, t.Price, t.IsAvailable,
        t.ToppingCategories.Select(tc => tc.CategoryId).ToList(), t.Group,
        t.LocationPrices.Select(l => new LocationPriceDto(l.LocationId, l.Price)).ToList());

    /// <summary>Items at zero stock in the branch (the automatic stop-list); empty without stock counting.</summary>
    private async Task<List<Guid>> SoldOutAsync(Guid? locationId, CancellationToken ct)
        => _stock is null || locationId is null ? [] : await _stock.SoldOutItemIdsAsync(locationId.Value, ct);

    private static MenuItemDto MapItemToDto(
        MenuItem i, string lang, List<MenuToppingDto>? toppings = null, List<OptionRuleDto>? rules = null, Guid? locationId = null,
        ComboSales? combos = null, TimedMenu? timed = null)
    {
        var comboItems = i.IsCombo && combos is not null
            ? combos.PartsOf(i.Id).Select(p => new ComboItemDto(
                p.ItemId, LocalizationHelper.Localize(p.Item.Name, p.Item.NameRu, p.Item.NameKk, lang),
                combos.VariantOrNull(p) is { } v ? LocalizationHelper.Localize(v.Label, v.LabelRu, v.LabelKk, lang) : null,
                p.Quantity)).ToList()
            : null;
        var locationIds = i.MenuItemLocations?.Select(l => l.LocationId).ToList();
        // A time price shows as the price now; the usual price comes along so the app can show it crossed out.
        decimal? Was(decimal usual, decimal now) => now < usual ? usual : null;
        var variants = i.Variants?.Count > 0
            ? i.Variants.OrderBy(v => v.SortOrder)
                .Select(v =>
                {
                    var now = timed?.PriceOf(i, v.Price, sized: true) ?? v.Price;
                    return new MenuItemVariantDto(v.Id,
                        LocalizationHelper.Localize(v.Label, v.LabelRu, v.LabelKk, lang),
                        now, v.SortOrder, v.IsDefault, Was(v.Price, now));
                }).ToList()
            : null;
        var usualPrice = DishOptions.BranchPrice(i, locationId);
        var price = timed?.PriceOf(i, usualPrice) ?? usualPrice;
        return new(i.Id, i.CategoryId, i.Category?.Name ?? "",
               LocalizationHelper.Localize(i.Name, i.NameRu, i.NameKk, lang),
               LocalizationHelper.Localize(i.Description, i.DescriptionRu, i.DescriptionKk, lang),
               price, i.IsAvailable, i.IsNew, i.ImageUrl, i.CreatedAt, locationIds, toppings, variants,
               i.Name, i.NameRu, i.NameKk, rules is { Count: > 0 } ? rules : null, Was(usualPrice, price), i.IsCombo, comboItems, Nutrition.ToDto(i));
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
               i.IikoProductId, i.IikoProductSizeId,
               i.ToppingLinks?.Select(l => l.ToppingId).ToList() ?? [],
               i.MenuItemLocations?.Where(l => l.Price != null).Select(l => new LocationPriceDto(l.LocationId, l.Price!.Value)).ToList() ?? [],
               Nutrition.ToDto(i));
    }
}
