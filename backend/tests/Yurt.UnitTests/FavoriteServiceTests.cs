using MockQueryable.NSubstitute;
using NSubstitute;
using Yurt.Application.Common.Interfaces;
using Yurt.Application.Features.Favorites.Services;
using Yurt.Domain.Entities;

namespace Yurt.UnitTests;

public class FavoriteServiceTests
{
    private static (FavoriteService svc, IApplicationDbContext db) Build(
        IEnumerable<Favorite>? favorites = null, IEnumerable<MenuItem>? menuItems = null)
    {
        var db = Substitute.For<IApplicationDbContext>();

        var favSet = (favorites ?? []).ToList().BuildMockDbSet();
        var itemSet = (menuItems ?? []).ToList().BuildMockDbSet();
        db.Favorites.Returns(favSet);
        db.MenuItems.Returns(itemSet);

        return (new FavoriteService(db), db);
    }

    [Fact]
    public async Task GetFavoritesAsync_ReturnsOnlyThatCustomersFavorites()
    {
        var customerId = Guid.NewGuid();
        var otherId = Guid.NewGuid();
        var cat = new MenuCategory { Name = "Coffee" };
        var item = new MenuItem { Name = "Latte", Category = cat, CategoryId = cat.Id, Price = 1500 };
        var otherItem = new MenuItem { Name = "Tea", Category = cat, CategoryId = cat.Id, Price = 900 };

        var mine = new Favorite { CustomerUserId = customerId, MenuItemId = item.Id, MenuItem = item };
        var theirs = new Favorite { CustomerUserId = otherId, MenuItemId = otherItem.Id, MenuItem = otherItem };
        var (svc, _) = Build([mine, theirs]);

        var result = await svc.GetFavoritesAsync(customerId);

        Assert.Single(result);
        Assert.Equal("Latte", result[0].Name);
    }

    [Fact]
    public async Task RemoveFavoriteAsync_RemovesExistingFavorite()
    {
        var customerId = Guid.NewGuid();
        var menuItemId = Guid.NewGuid();
        var fav = new Favorite { CustomerUserId = customerId, MenuItemId = menuItemId };
        var (svc, db) = Build([fav]);

        var result = await svc.RemoveFavoriteAsync(customerId, menuItemId);

        Assert.True(result.Succeeded);
        db.Favorites.Received(1).Remove(fav);
        await db.Received(1).SaveChangesAsync(Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task RemoveFavoriteAsync_NotFavorited_IsIdempotentSuccess()
    {
        var (svc, db) = Build();

        var result = await svc.RemoveFavoriteAsync(Guid.NewGuid(), Guid.NewGuid());

        Assert.True(result.Succeeded);
        db.Favorites.DidNotReceive().Remove(Arg.Any<Favorite>());
    }

    [Fact]
    public async Task AddFavoriteAsync_UnknownMenuItem_ReturnsNotFound()
    {
        // MockQueryable's DbSet mock cannot emulate DbSet.FindAsync, which this
        // path uses to look up the menu item — it always resolves as "not found"
        // under this mock, which happens to match the genuinely-missing-item case.
        var (svc, _) = Build();

        var result = await svc.AddFavoriteAsync(Guid.NewGuid(), Guid.NewGuid());

        Assert.False(result.Succeeded);
        Assert.Equal(404, result.StatusCode);
    }
}
