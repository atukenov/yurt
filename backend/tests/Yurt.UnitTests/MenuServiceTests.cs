using MockQueryable.NSubstitute;
using NSubstitute;
using Yurt.Application.Common.Interfaces;
using Yurt.Application.Features.Menu.DTOs;
using Yurt.Application.Features.Menu.Services;
using Yurt.Domain.Entities;

namespace Yurt.UnitTests;

/// <summary>
/// Tests for <see cref="MenuService"/> category/item ordering and reordering —
/// the drag-and-drop admin reorder endpoints had no coverage before this.
/// </summary>
public class MenuServiceTests
{
    private static (MenuService svc, IApplicationDbContext db) Build(
        IEnumerable<MenuCategory>? categories = null, IEnumerable<MenuItem>? items = null)
    {
        var db = Substitute.For<IApplicationDbContext>();
        var audit = Substitute.For<IAuditLogService>();
        var cache = Substitute.For<IMenuCacheService>();

        var catSet = (categories ?? []).ToList().BuildMockDbSet();
        var itemSet = (items ?? []).ToList().BuildMockDbSet();
        db.MenuCategories.Returns(catSet);
        db.MenuItems.Returns(itemSet);

        return (new MenuService(db, audit, cache), db);
    }

    [Fact]
    public async Task AdminGetCategoriesAsync_OrdersBySortOrderThenName()
    {
        var a = new MenuCategory { Name = "Zebra", SortOrder = 1 };
        var b = new MenuCategory { Name = "Apple", SortOrder = 0 };
        var c = new MenuCategory { Name = "Mango", SortOrder = 1 };
        var (svc, _) = Build(categories: [a, b, c]);

        var result = await svc.AdminGetCategoriesAsync();

        Assert.Equal(["Apple", "Mango", "Zebra"], result.Select(r => r.Name));
    }

    [Fact]
    public async Task ReorderCategoriesAsync_AssignsSequentialSortOrderInGivenOrder()
    {
        var a = new MenuCategory { Name = "A", SortOrder = 0 };
        var b = new MenuCategory { Name = "B", SortOrder = 1 };
        var c = new MenuCategory { Name = "C", SortOrder = 2 };
        var (svc, _) = Build(categories: [a, b, c]);

        var result = await svc.ReorderCategoriesAsync([c.Id, a.Id, b.Id]);

        Assert.True(result.Succeeded);
        Assert.Equal(0, c.SortOrder);
        Assert.Equal(1, a.SortOrder);
        Assert.Equal(2, b.SortOrder);
    }

    [Fact]
    public async Task ReorderCategoriesAsync_IgnoresUnknownIds()
    {
        var a = new MenuCategory { Name = "A", SortOrder = 0 };
        var (svc, _) = Build(categories: [a]);

        var result = await svc.ReorderCategoriesAsync([Guid.NewGuid(), a.Id]);

        Assert.True(result.Succeeded);
        Assert.Equal(1, a.SortOrder);
    }

    [Fact]
    public async Task ReorderItemsAsync_OnlyReordersItemsInTheGivenCategory()
    {
        var catA = Guid.NewGuid();
        var catB = Guid.NewGuid();
        var item1 = new MenuItem { CategoryId = catA, Name = "Item1", SortOrder = 0 };
        var item2 = new MenuItem { CategoryId = catA, Name = "Item2", SortOrder = 1 };
        var otherCatItem = new MenuItem { CategoryId = catB, Name = "Other", SortOrder = 0 };
        var (svc, _) = Build(items: [item1, item2, otherCatItem]);

        // Reorder catA items; also pass otherCatItem's id — it must be filtered out by category.
        var result = await svc.ReorderItemsAsync(catA, [item2.Id, item1.Id, otherCatItem.Id]);

        Assert.True(result.Succeeded);
        Assert.Equal(1, item1.SortOrder);
        Assert.Equal(0, item2.SortOrder);
        Assert.Equal(0, otherCatItem.SortOrder); // untouched — different category
    }

    [Fact]
    public async Task AdminGetItemsAsync_OrdersByCategorySortOrderThenItemSortOrderThenName()
    {
        var catA = new MenuCategory { Name = "CatA", SortOrder = 0 };
        var catB = new MenuCategory { Name = "CatB", SortOrder = 1 };
        var a2 = new MenuItem { Category = catA, CategoryId = catA.Id, Name = "A2", SortOrder = 1 };
        var a1 = new MenuItem { Category = catA, CategoryId = catA.Id, Name = "A1", SortOrder = 0 };
        var b1 = new MenuItem { Category = catB, CategoryId = catB.Id, Name = "B1", SortOrder = 0 };
        var (svc, _) = Build(items: [a2, a1, b1]);

        var result = await svc.AdminGetItemsAsync();

        Assert.Equal(["A1", "A2", "B1"], result.Select(r => r.Name));
    }

    [Fact]
    public async Task CreateCategoryAsync_AddsCategoryAndSaves()
    {
        var (svc, db) = Build();
        var dto = new CreateCategoryDto("Coffee", "Кофе", "Кофе", 0);

        var result = await svc.CreateCategoryAsync(dto);

        Assert.True(result.Succeeded);
        Assert.Equal(201, result.StatusCode);
        db.MenuCategories.Received(1).Add(Arg.Is<MenuCategory>(c => c.Name == "Coffee"));
        await db.Received(1).SaveChangesAsync(Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task CreateItemAsync_UnknownCategory_ReturnsFailure()
    {
        var (svc, _) = Build(); // no categories seeded
        var dto = new CreateMenuItemDto(Guid.NewGuid(), "Latte", null, null, null, null, null, 1500, true, null);

        var result = await svc.CreateItemAsync(dto);

        Assert.False(result.Succeeded);
        Assert.Equal(400, result.StatusCode);
    }

    [Fact]
    public async Task GetItemsAsync_ExposesNamesInAllLanguages_AndSearchMatchesAnyOfThem()
    {
        var cat = new MenuCategory { Name = "Drinks" };
        var apple = new MenuItem { Name = "Apple Juice", NameRu = "Яблочный сок", NameKk = "Алма шырыны", Category = cat, CategoryId = cat.Id, IsAvailable = true };
        var cola = new MenuItem { Name = "Cola", NameRu = "Кола", Category = cat, CategoryId = cat.Id, IsAvailable = true };
        var (svc, db) = Build(items: [apple, cola]);
        var links = new List<MenuToppingCategory>().BuildMockDbSet();
        db.MenuToppingCategories.Returns(links);

        // App is in Russian, customer searches in English
        var result = await svc.GetItemsAsync(null, "apple", "ru");

        var dto = Assert.Single(result);
        Assert.Equal("Яблочный сок", dto.Name);
        Assert.Equal("Apple Juice", dto.NameEn);
        Assert.Equal("Алма шырыны", dto.NameKk);

        // App is in English, customer searches in Kazakh
        Assert.Single(await svc.GetItemsAsync(null, "АЛМА", "en"));
    }
}
