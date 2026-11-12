using System.Net;
using System.Net.Http.Headers;
using System.Net.Http.Json;
using System.Text.Json;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Yurt.Domain.Entities;
using Yurt.Infrastructure.Persistence;
using Yurt.IntegrationTests.Helpers;

namespace Yurt.IntegrationTests.Tests;

/// <summary>Option rules: required choices, smallest / largest number, options per dish, prices per branch.</summary>
[Collection("Integration")]
public class OptionRulesTests(YurtWebAppFactory factory)
{
    private static readonly Guid LocationId = Guid.Parse("11111111-0000-0000-0000-000000000001");
    private const string Sale = "/api/v1/admin/counter-sales";

    private async Task<HttpClient> AdminAsync()
    {
        var admin = factory.CreateClient();
        admin.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer",
            await ApiHelpers.CreateAdminTokenAsync(factory.Services, admin));
        return admin;
    }

    private static string Unique(string p) => $"{p}{Guid.NewGuid():N}"[..14];

    /// <summary>A fresh category with one dish (price 10).</summary>
    private async Task<(Guid Category, Guid Item)> DishAsync(Action<ApplicationDbContext, MenuCategory, MenuItem>? more = null)
    {
        await using var scope = factory.Services.CreateAsyncScope();
        var db = scope.ServiceProvider.GetRequiredService<ApplicationDbContext>();
        var cat = new MenuCategory { Name = Unique("Cat") };
        var item = new MenuItem { Category = cat, Name = Unique("Dish"), Description = "t", Price = 10m, IsAvailable = true };
        db.MenuCategories.Add(cat);
        db.MenuItems.Add(item);
        more?.Invoke(db, cat, item);
        await db.SaveChangesAsync();
        return (cat.Id, item.Id);
    }

    private static MenuTopping Option(ApplicationDbContext db, MenuCategory cat, string group, decimal price)
    {
        var t = new MenuTopping { Name = Unique("Opt"), Price = price, Group = group };
        db.MenuToppings.Add(t);
        db.MenuToppingCategories.Add(new MenuToppingCategory { Topping = t, Category = cat });
        return t;
    }

    private static object Body(Guid item, params Guid[] picked) => new
    {
        locationId = LocationId, paymentMethod = "Cash", idempotencyKey = Guid.NewGuid().ToString(),
        items = new[] { new { menuItemId = item, quantity = 1, toppingIds = picked } },
    };

    [Fact]
    public async Task RequiredGroup_MustBeChosen_AndOnlyOnce()
    {
        var admin = await AdminAsync();
        var group = Unique("milk");
        Guid a = default, b = default;
        var (_, item) = await DishAsync((db, cat, _) =>
        {
            db.MenuOptionGroups.Add(new MenuOptionGroup { Key = group, Title = "milk", Required = true, MinSelect = 1, MaxSelect = 1 });
            var x = Option(db, cat, group, 1m); var y = Option(db, cat, group, 2m);
            a = x.Id; b = y.Id;
        });

        var none = await admin.PostAsJsonAsync(Sale, Body(item));
        Assert.Equal(HttpStatusCode.BadRequest, none.StatusCode);
        Assert.Contains("choose", await none.Content.ReadAsStringAsync(), StringComparison.OrdinalIgnoreCase);

        Assert.Equal(HttpStatusCode.BadRequest, (await admin.PostAsJsonAsync(Sale, Body(item, a, b))).StatusCode);

        var ok = await admin.PostAsJsonAsync(Sale, Body(item, b));
        Assert.Equal(HttpStatusCode.Created, ok.StatusCode);
        Assert.Equal(12m, (await ok.Content.ReadFromJsonAsync<JsonElement>()).GetProperty("total").GetDecimal());
    }

    [Fact]
    public async Task MinAndMax_AreEnforced_AndAnUnsetGroupStaysAtMostOne()
    {
        var admin = await AdminAsync();
        var many = Unique("many"); var legacy = Unique("old");
        var ids = new List<Guid>(); var old = new List<Guid>();
        var (_, item) = await DishAsync((db, cat, _) =>
        {
            db.MenuOptionGroups.Add(new MenuOptionGroup { Key = many, MinSelect = 2, MaxSelect = 3 });
            for (var i = 0; i < 4; i++) ids.Add(Option(db, cat, many, 1m).Id);
            for (var i = 0; i < 2; i++) old.Add(Option(db, cat, legacy, 1m).Id);
        });

        Assert.Equal(HttpStatusCode.BadRequest, (await admin.PostAsJsonAsync(Sale, Body(item, ids[0]))).StatusCode);          // too few
        Assert.Equal(HttpStatusCode.BadRequest, (await admin.PostAsJsonAsync(Sale, Body(item, ids[0], ids[1], ids[2], ids[3]))).StatusCode); // too many
        Assert.Equal(HttpStatusCode.BadRequest, (await admin.PostAsJsonAsync(Sale, Body(item, ids[0], ids[1], old[0], old[1]))).StatusCode); // old group: one only
        Assert.Equal(HttpStatusCode.Created, (await admin.PostAsJsonAsync(Sale, Body(item, ids[0], ids[1], old[0]))).StatusCode);
    }

    [Fact]
    public async Task OptionsSetPerDish_OverrideTheCategory_AndShowOnTheMenu()
    {
        var admin = await AdminAsync();
        Guid chosen = default, other = default;
        var (_, item) = await DishAsync((db, cat, dish) =>
        {
            var t1 = Option(db, cat, null!, 1m); var t2 = Option(db, cat, null!, 1m);
            chosen = t1.Id; other = t2.Id;
            db.MenuItemToppings.Add(new MenuItemTopping { MenuItem = dish, Topping = t1 });
        });

        Assert.Equal(HttpStatusCode.BadRequest, (await admin.PostAsJsonAsync(Sale, Body(item, other))).StatusCode);
        Assert.Equal(HttpStatusCode.Created, (await admin.PostAsJsonAsync(Sale, Body(item, chosen))).StatusCode);

        var guest = factory.CreateClient();
        var shown = await guest.GetFromJsonAsync<JsonElement>($"/api/v1/menu/items/{item}?lang=en");
        var tops = shown.GetProperty("availableToppings").EnumerateArray().Select(t => t.GetProperty("id").GetGuid()).ToList();
        Assert.Equal([chosen], tops);
    }

    [Fact]
    public async Task BranchPrices_ApplyToTheDishAndItsOptions_AndTheMenuShowsThem()
    {
        var admin = await AdminAsync();
        Guid second = default, opt = default;
        var (_, item) = await DishAsync((db, cat, dish) =>
        {
            var loc = new Location { Name = Unique("Branch"), Address = "a", WorkingHours = "9-18", ContactPhone = "1" };
            db.Locations.Add(loc);
            second = loc.Id;
            var t = Option(db, cat, null!, 3m);
            opt = t.Id;
            db.MenuToppingLocations.Add(new MenuToppingLocation { Topping = t, Location = loc, Price = 5m });
            db.MenuItemLocations.Add(new MenuItemLocation { MenuItem = dish, Location = loc, Price = 7m });
            db.MenuItemLocations.Add(new MenuItemLocation { MenuItem = dish, LocationId = LocationId });
        });

        object At(Guid loc) => new
        {
            locationId = loc, paymentMethod = "Cash", idempotencyKey = Guid.NewGuid().ToString(),
            items = new[] { new { menuItemId = item, quantity = 1, toppingIds = new[] { opt } } },
        };
        var first = await admin.PostAsJsonAsync(Sale, At(LocationId));
        Assert.Equal(13m, (await first.Content.ReadFromJsonAsync<JsonElement>()).GetProperty("total").GetDecimal()); // 10 + 3
        var branch = await admin.PostAsJsonAsync(Sale, At(second));
        Assert.Equal(12m, (await branch.Content.ReadFromJsonAsync<JsonElement>()).GetProperty("total").GetDecimal()); // 7 + 5

        var shown = await factory.CreateClient().GetFromJsonAsync<JsonElement>($"/api/v1/menu/items/{item}?lang=en&locationId={second}");
        Assert.Equal(7m, shown.GetProperty("price").GetDecimal());
        Assert.Equal(5m, shown.GetProperty("availableToppings")[0].GetProperty("price").GetDecimal());
    }

    [Fact]
    public async Task Admin_SetsRules_AndTheMenuCarriesThemForTheDish()
    {
        var admin = await AdminAsync();
        var group = Unique("size");
        var (_, item) = await DishAsync((db, cat, _) => { Option(db, cat, group, 1m); Option(db, cat, group, 1m); });

        var save = await admin.PutAsJsonAsync("/api/v1/admin/menu/option-groups",
            new { key = group, title = "Pick a size", titleRu = "Выберите размер", required = true, minSelect = 0, maxSelect = 1 });
        Assert.True(save.IsSuccessStatusCode);
        var bad = await admin.PutAsJsonAsync("/api/v1/admin/menu/option-groups", new { key = group, required = false, minSelect = 3, maxSelect = 2 });
        Assert.Equal(HttpStatusCode.BadRequest, bad.StatusCode);

        var shown = await factory.CreateClient().GetFromJsonAsync<JsonElement>($"/api/v1/menu/items/{item}?lang=ru");
        var rule = shown.GetProperty("optionRules").EnumerateArray().Single();
        Assert.Equal(group, rule.GetProperty("group").GetString());
        Assert.Equal("Выберите размер", rule.GetProperty("title").GetString());
        Assert.True(rule.GetProperty("required").GetBoolean());
        Assert.Equal(1, rule.GetProperty("min").GetInt32());
        Assert.Equal(1, rule.GetProperty("max").GetInt32());

        (await admin.DeleteAsync($"/api/v1/admin/menu/option-groups?key={group}")).EnsureSuccessStatusCode();
    }
}
