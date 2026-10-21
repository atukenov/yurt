using Microsoft.EntityFrameworkCore;
using Yurt.Application.Common.Interfaces;
using Yurt.Application.Common.Models;
using Yurt.Domain.Entities;

namespace Yurt.Application.Features.Stock;

/// <summary>Staff-facing ingredients, their counts per branch and the recipe of each menu item (Phase 6, <c>own_pos</c>).</summary>
public class IngredientService(IApplicationDbContext db, IAuditLogService audit, IMenuCacheService menuCache)
{
    private const int MaxName = 100, MaxUnit = 20, MaxRecipeLines = 50;

    public async Task<Result<List<IngredientDto>>> ListAsync(Guid locationId, CancellationToken ct = default)
    {
        if (!await db.Locations.AnyAsync(l => l.Id == locationId, ct))
            return Result<List<IngredientDto>>.Failure("Location not found.", 404);

        var ingredients = await db.Ingredients.OrderBy(i => i.Name).ToListAsync(ct);
        var counts = await db.IngredientStocks.Where(s => s.LocationId == locationId).ToDictionaryAsync(s => s.IngredientId, ct);
        var usage = await db.RecipeLines.GroupBy(r => r.IngredientId)
            .Select(g => new { g.Key, Items = g.Select(r => r.MenuItemId).Distinct().Count() })
            .ToDictionaryAsync(x => x.Key, x => x.Items, ct);

        return Result<List<IngredientDto>>.Success(ingredients.Select(i =>
        {
            counts.TryGetValue(i.Id, out var s);
            return ToDto(i, s, usage.GetValueOrDefault(i.Id));
        }).ToList());
    }

    public async Task<Result<IngredientDto>> CreateAsync(SaveIngredientDto dto, CancellationToken ct = default)
    {
        var (name, unit, error) = Validate(dto);
        if (error != null) return Result<IngredientDto>.Failure(error, 400);
        if (await db.Ingredients.AnyAsync(i => i.Name == name, ct))
            return Result<IngredientDto>.Failure("An ingredient with this name already exists.", 409);

        var ingredient = new Ingredient { Name = name, Unit = unit };
        db.Ingredients.Add(ingredient);
        await db.SaveChangesAsync(ct);
        await audit.LogAsync("IngredientCreated", "Ingredient", ingredient.Id.ToString(), name, ct);
        return Result<IngredientDto>.Success(ToDto(ingredient, null, 0), 201);
    }

    public async Task<Result<IngredientDto>> UpdateAsync(Guid id, SaveIngredientDto dto, CancellationToken ct = default)
    {
        var (name, unit, error) = Validate(dto);
        if (error != null) return Result<IngredientDto>.Failure(error, 400);
        var ingredient = await db.Ingredients.FirstOrDefaultAsync(i => i.Id == id, ct);
        if (ingredient is null) return Result<IngredientDto>.Failure("Ingredient not found.", 404);
        if (await db.Ingredients.AnyAsync(i => i.Name == name && i.Id != id, ct))
            return Result<IngredientDto>.Failure("An ingredient with this name already exists.", 409);

        ingredient.Name = name;
        ingredient.Unit = unit;
        ingredient.UpdatedAt = DateTime.UtcNow;
        await db.SaveChangesAsync(ct);
        await audit.LogAsync("IngredientUpdated", "Ingredient", id.ToString(), name, ct);
        return Result<IngredientDto>.Success(ToDto(ingredient, null, 0));
    }

    /// <summary>Deleting an ingredient also removes its counts and the recipe lines that use it.</summary>
    public async Task<Result<bool>> DeleteAsync(Guid id, CancellationToken ct = default)
    {
        var ingredient = await db.Ingredients.FirstOrDefaultAsync(i => i.Id == id, ct);
        if (ingredient is null) return Result<bool>.Failure("Ingredient not found.", 404);

        db.Ingredients.Remove(ingredient);
        await db.SaveChangesAsync(ct);
        await menuCache.InvalidateAllMenuAsync();
        await audit.LogAsync("IngredientDeleted", "Ingredient", id.ToString(), ingredient.Name, ct);
        return Result<bool>.Success(true);
    }

    /// <summary>Set how much a branch has. A null quantity stops counting the ingredient there.</summary>
    public async Task<Result<IngredientDto>> SetStockAsync(Guid id, Guid locationId, SetIngredientStockDto dto, CancellationToken ct = default)
    {
        if (dto.Quantity is < 0 || dto.LowStockAt is < 0)
            return Result<IngredientDto>.Failure("Numbers cannot be negative.", 400);
        var ingredient = await db.Ingredients.FirstOrDefaultAsync(i => i.Id == id, ct);
        if (ingredient is null) return Result<IngredientDto>.Failure("Ingredient not found.", 404);
        if (!await db.Locations.AnyAsync(l => l.Id == locationId, ct))
            return Result<IngredientDto>.Failure("Location not found.", 404);

        var row = await db.IngredientStocks.FirstOrDefaultAsync(s => s.IngredientId == id && s.LocationId == locationId, ct);
        if (dto.Quantity is null)
        {
            if (row != null) db.IngredientStocks.Remove(row);
            row = null;
        }
        else if (row is null)
        {
            row = new IngredientStock { IngredientId = id, LocationId = locationId, Quantity = dto.Quantity.Value, LowStockAt = dto.LowStockAt };
            db.IngredientStocks.Add(row);
        }
        else
        {
            row.Quantity = dto.Quantity.Value;
            row.LowStockAt = dto.LowStockAt;
            row.UpdatedAt = DateTime.UtcNow;
        }
        await db.SaveChangesAsync(ct);
        await menuCache.InvalidateAllMenuAsync();
        await audit.LogAsync("IngredientStockSet", "Ingredient", id.ToString(),
            $"Branch {locationId}: {(dto.Quantity?.ToString() ?? "not counted")}", ct);
        return Result<IngredientDto>.Success(ToDto(ingredient, row, await db.RecipeLines.Where(r => r.IngredientId == id).Select(r => r.MenuItemId).Distinct().CountAsync(ct)));
    }

    public async Task<Result<List<RecipeLineDto>>> GetRecipeAsync(Guid menuItemId, CancellationToken ct = default)
    {
        if (!await db.MenuItems.AnyAsync(m => m.Id == menuItemId, ct))
            return Result<List<RecipeLineDto>>.Failure("Menu item not found.", 404);

        var lines = await db.RecipeLines.Where(r => r.MenuItemId == menuItemId)
            .OrderBy(r => r.Ingredient.Name)
            .Select(r => new RecipeLineDto(r.IngredientId, r.Ingredient.Name, r.Ingredient.Unit, r.VariantId, r.Quantity))
            .ToListAsync(ct);
        return Result<List<RecipeLineDto>>.Success(lines);
    }

    /// <summary>Replaces the whole recipe of a menu item. An empty list removes the recipe.</summary>
    public async Task<Result<List<RecipeLineDto>>> SaveRecipeAsync(Guid menuItemId, SaveRecipeDto dto, CancellationToken ct = default)
    {
        if (!await db.MenuItems.AnyAsync(m => m.Id == menuItemId, ct))
            return Result<List<RecipeLineDto>>.Failure("Menu item not found.", 404);
        var lines = dto.Lines ?? [];
        if (lines.Count > MaxRecipeLines) return Result<List<RecipeLineDto>>.Failure("Too many recipe lines.", 400);
        if (lines.Any(l => l.Quantity <= 0)) return Result<List<RecipeLineDto>>.Failure("Each amount must be more than zero.", 400);
        if (lines.GroupBy(l => (l.IngredientId, l.VariantId)).Any(g => g.Count() > 1))
            return Result<List<RecipeLineDto>>.Failure("An ingredient appears twice for the same size.", 400);

        var ingredientIds = lines.Select(l => l.IngredientId).Distinct().ToList();
        if (await db.Ingredients.CountAsync(i => ingredientIds.Contains(i.Id), ct) != ingredientIds.Count)
            return Result<List<RecipeLineDto>>.Failure("An ingredient was not found.", 400);
        var variantIds = lines.Where(l => l.VariantId != null).Select(l => l.VariantId!.Value).Distinct().ToList();
        if (await db.MenuItemVariants.CountAsync(v => v.MenuItemId == menuItemId && variantIds.Contains(v.Id), ct) != variantIds.Count)
            return Result<List<RecipeLineDto>>.Failure("A size does not belong to this menu item.", 400);

        db.RecipeLines.RemoveRange(await db.RecipeLines.Where(r => r.MenuItemId == menuItemId).ToListAsync(ct));
        foreach (var l in lines)
            db.RecipeLines.Add(new RecipeLine { MenuItemId = menuItemId, VariantId = l.VariantId, IngredientId = l.IngredientId, Quantity = l.Quantity });
        await db.SaveChangesAsync(ct);
        await menuCache.InvalidateAllMenuAsync();
        await audit.LogAsync("RecipeSaved", "MenuItem", menuItemId.ToString(), $"{lines.Count} lines", ct);
        return await GetRecipeAsync(menuItemId, ct);
    }

    public async Task<Result<List<ToppingUseDto>>> GetToppingUsesAsync(Guid toppingId, CancellationToken ct = default)
    {
        if (!await db.MenuToppings.AnyAsync(t => t.Id == toppingId, ct))
            return Result<List<ToppingUseDto>>.Failure("Topping not found.", 404);

        var lines = await db.ToppingUses.Where(u => u.ToppingId == toppingId)
            .OrderBy(u => u.Ingredient.Name)
            .Select(u => new ToppingUseDto(u.IngredientId, u.Ingredient.Name, u.Ingredient.Unit, u.Quantity))
            .ToListAsync(ct);
        return Result<List<ToppingUseDto>>.Success(lines);
    }

    /// <summary>Replaces what a topping uses. An empty list means the topping is not counted.</summary>
    public async Task<Result<List<ToppingUseDto>>> SaveToppingUsesAsync(Guid toppingId, SaveToppingUsesDto dto, CancellationToken ct = default)
    {
        if (!await db.MenuToppings.AnyAsync(t => t.Id == toppingId, ct))
            return Result<List<ToppingUseDto>>.Failure("Topping not found.", 404);
        var lines = dto.Lines ?? [];
        if (lines.Count > MaxRecipeLines) return Result<List<ToppingUseDto>>.Failure("Too many lines.", 400);
        if (lines.Any(l => l.Quantity <= 0)) return Result<List<ToppingUseDto>>.Failure("Each amount must be more than zero.", 400);
        if (lines.GroupBy(l => l.IngredientId).Any(g => g.Count() > 1))
            return Result<List<ToppingUseDto>>.Failure("An ingredient appears twice.", 400);
        var ingredientIds = lines.Select(l => l.IngredientId).ToList();
        if (await db.Ingredients.CountAsync(i => ingredientIds.Contains(i.Id), ct) != ingredientIds.Count)
            return Result<List<ToppingUseDto>>.Failure("An ingredient was not found.", 400);

        db.ToppingUses.RemoveRange(await db.ToppingUses.Where(u => u.ToppingId == toppingId).ToListAsync(ct));
        foreach (var l in lines)
            db.ToppingUses.Add(new ToppingUse { ToppingId = toppingId, IngredientId = l.IngredientId, Quantity = l.Quantity });
        await db.SaveChangesAsync(ct);
        await menuCache.InvalidateAllMenuAsync();
        await audit.LogAsync("ToppingUseSaved", "MenuTopping", toppingId.ToString(), $"{lines.Count} lines", ct);
        return await GetToppingUsesAsync(toppingId, ct);
    }

    private static (string Name, string Unit, string? Error) Validate(SaveIngredientDto dto)
    {
        var name = dto.Name?.Trim() ?? "";
        var unit = string.IsNullOrWhiteSpace(dto.Unit) ? "pcs" : dto.Unit.Trim();
        if (name.Length == 0 || name.Length > MaxName) return (name, unit, $"Name is required (up to {MaxName} characters).");
        if (unit.Length > MaxUnit) return (name, unit, $"Unit is too long (up to {MaxUnit} characters).");
        return (name, unit, null);
    }

    private static IngredientDto ToDto(Ingredient i, IngredientStock? s, int usedIn) => new(
        i.Id, i.Name, i.Unit, s?.Quantity, s?.LowStockAt,
        IsLow: s is { LowStockAt: not null } && s.Quantity <= s.LowStockAt && s.Quantity > 0,
        IsOut: s is { Quantity: <= 0 }, usedIn);
}
