using Microsoft.EntityFrameworkCore;
using Yurt.Application.Common.Helpers;
using Yurt.Application.Common.Interfaces;
using Yurt.Application.Common.Models;
using Yurt.Application.Features.Menu.DTOs;

namespace Yurt.Application.Features.Favorites.Services;

public class FavoriteService
{
    private readonly IApplicationDbContext _db;

    public FavoriteService(IApplicationDbContext db) => _db = db;

    public async Task<List<MenuItemDto>> GetFavoritesAsync(
        Guid customerId, string lang = "ru", CancellationToken ct = default)
    {
        var favs = await _db.Favorites
            .Where(f => f.CustomerUserId == customerId)
            .Include(f => f.MenuItem).ThenInclude(m => m.Category)
            .ToListAsync(ct);

        return favs.Select(f => new MenuItemDto(
            f.MenuItem.Id,
            f.MenuItem.CategoryId,
            f.MenuItem.Category?.Name ?? "",
            LocalizationHelper.Localize(f.MenuItem.Name, f.MenuItem.NameRu, f.MenuItem.NameKk, lang),
            LocalizationHelper.Localize(f.MenuItem.Description, f.MenuItem.DescriptionRu, f.MenuItem.DescriptionKk, lang),
            f.MenuItem.Price,
            f.MenuItem.IsAvailable,
            f.MenuItem.IsNew,
            f.MenuItem.ImageUrl,
            NameEn: f.MenuItem.Name,
            NameRu: f.MenuItem.NameRu,
            NameKk: f.MenuItem.NameKk)).ToList();
    }

    public async Task<Result<bool>> AddFavoriteAsync(
        Guid customerId, Guid menuItemId, CancellationToken ct = default)
    {
        var item = await _db.MenuItems.FindAsync([menuItemId], ct);
        if (item == null) return Result<bool>.NotFound("Menu item not found.");

        var exists = await _db.Favorites
            .AnyAsync(f => f.CustomerUserId == customerId && f.MenuItemId == menuItemId, ct);
        if (exists) return Result<bool>.Success(true); // idempotent

        _db.Favorites.Add(new Domain.Entities.Favorite
        {
            CustomerUserId = customerId,
            MenuItemId = menuItemId
        });
        await _db.SaveChangesAsync(ct);
        return Result<bool>.Success(true, 201);
    }

    public async Task<Result<bool>> RemoveFavoriteAsync(
        Guid customerId, Guid menuItemId, CancellationToken ct = default)
    {
        var fav = await _db.Favorites
            .FirstOrDefaultAsync(f => f.CustomerUserId == customerId && f.MenuItemId == menuItemId, ct);

        if (fav == null) return Result<bool>.Success(true); // idempotent
        _db.Favorites.Remove(fav);
        await _db.SaveChangesAsync(ct);
        return Result<bool>.Success(true);
    }
}
