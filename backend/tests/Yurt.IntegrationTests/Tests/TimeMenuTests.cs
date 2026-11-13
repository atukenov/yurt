using System.Net;
using System.Net.Http.Headers;
using System.Net.Http.Json;
using System.Text.Json;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Yurt.Application.Features.Menu.Services;
using Yurt.Domain.Entities;
using Yurt.Infrastructure.Persistence;
using Yurt.IntegrationTests.Helpers;

namespace Yurt.IntegrationTests.Tests;

/// <summary>Time menus: dishes and categories on the menu only in a window, prices that apply only in a window.</summary>
[Collection("Integration")]
public class TimeMenuTests(YurtWebAppFactory factory)
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

    /// <summary>Start / end ("HH:mm") of a window that is running now (or clearly is not) on the clock of the zone.</summary>
    private static (string Start, string End) Window(bool running, string? zone = null)
    {
        var now = TimeZoneInfo.ConvertTimeFromUtc(DateTime.UtcNow, TimedMenu.ZoneOf(zone));
        var m = now.Hour * 60 + now.Minute;
        var (s, e) = running ? ((m + 1410) % 1440, (m + 30) % 1440) : ((m + 60) % 1440, (m + 120) % 1440);
        return (TimedMenu.FormatMinute(s), TimedMenu.FormatMinute(e == 0 ? 1440 : e));
    }

    private async Task<Guid> ScheduleAsync(HttpClient admin, bool running, Guid[]? items = null, Guid[]? categories = null,
        object[]? prices = null, bool isActive = true, Guid? locationId = null, string? zone = null)
    {
        var (start, end) = Window(running, zone);
        var res = await admin.PostAsJsonAsync("/api/v1/admin/menu/schedules", new
        {
            name = Unique("Menu"), days = 127, start, end, locationId, isActive,
            itemIds = items ?? [], categoryIds = categories ?? [], prices = prices ?? [],
        });
        Assert.Equal(HttpStatusCode.Created, res.StatusCode);
        return (await res.Content.ReadFromJsonAsync<JsonElement>()).GetProperty("id").GetGuid();
    }

    /// <summary>A fresh category with one dish (price 10).</summary>
    private async Task<(Guid Category, Guid Item)> DishAsync()
    {
        await using var scope = factory.Services.CreateAsyncScope();
        var db = scope.ServiceProvider.GetRequiredService<ApplicationDbContext>();
        var cat = new MenuCategory { Name = Unique("Cat") };
        var item = new MenuItem { Category = cat, Name = Unique("Dish"), Description = "t", Price = 10m, IsAvailable = true };
        db.MenuCategories.Add(cat);
        db.MenuItems.Add(item);
        await db.SaveChangesAsync();
        return (cat.Id, item.Id);
    }

    private static object Body(Guid item, Guid? location = null) => new
    {
        locationId = location ?? LocationId, paymentMethod = "Cash", idempotencyKey = Guid.NewGuid().ToString(),
        items = new[] { new { menuItemId = item, quantity = 1, toppingIds = Array.Empty<Guid>() } },
    };

    private async Task<JsonElement?> GuestItemAsync(Guid item, Guid? location = null)
    {
        var res = await factory.CreateClient().GetAsync($"/api/v1/menu/items/{item}?lang=en&locationId={location ?? LocationId}");
        return res.StatusCode == HttpStatusCode.OK ? await res.Content.ReadFromJsonAsync<JsonElement>() : null;
    }

    private async Task<bool> OnGuestMenuAsync(Guid item, Guid category)
    {
        var guest = factory.CreateClient();
        var list = await guest.GetFromJsonAsync<JsonElement>($"/api/v1/menu?lang=en&locationId={LocationId}");
        var cats = await guest.GetFromJsonAsync<JsonElement>($"/api/v1/menu/categories?lang=en&locationId={LocationId}");
        var inList = list.EnumerateArray().Any(i => i.GetProperty("id").GetGuid() == item);
        var inCats = cats.EnumerateArray().Any(c => c.GetProperty("id").GetGuid() == category);
        Assert.Equal(inList, inCats); // a category shows exactly when it has something to sell
        return inList;
    }

    [Fact]
    public async Task ADishOutsideItsWindow_IsHidden_AndCannotBeSold()
    {
        var admin = await AdminAsync();
        var (cat, item) = await DishAsync();
        var schedule = await ScheduleAsync(admin, running: false, items: [item]);

        Assert.False(await OnGuestMenuAsync(item, cat));
        Assert.Null(await GuestItemAsync(item));
        var sale = await admin.PostAsJsonAsync(Sale, Body(item));
        Assert.Equal(HttpStatusCode.BadRequest, sale.StatusCode);
        Assert.Contains("not on the menu", await sale.Content.ReadAsStringAsync());

        var customer = factory.CreateClient();
        var (token, _) = await ApiHelpers.CreateCustomerAsync(customer, "+77002990811");
        ApiHelpers.Authorize(customer, token);
        var order = await customer.PostAsJsonAsync("/api/v1/orders", new
        {
            locationId = LocationId, paymentMethod = "Cash",
            items = new[] { new { menuItemId = item, quantity = 1, toppings = Array.Empty<object>() } },
        });
        Assert.Equal(HttpStatusCode.BadRequest, order.StatusCode);

        // Deleting the window puts the dish back on the menu all day.
        Assert.Equal(HttpStatusCode.OK, (await admin.DeleteAsync($"/api/v1/admin/menu/schedules/{schedule}")).StatusCode);
        Assert.True(await OnGuestMenuAsync(item, cat));
        Assert.Equal(HttpStatusCode.Created, (await admin.PostAsJsonAsync(Sale, Body(item))).StatusCode);
    }

    [Fact]
    public async Task ADishInsideItsWindow_IsSold_AndAWholeCategoryFollowsItsWindow()
    {
        var admin = await AdminAsync();
        var (cat, item) = await DishAsync();
        var (hiddenCat, hiddenItem) = await DishAsync();
        await ScheduleAsync(admin, running: true, items: [item]);
        await ScheduleAsync(admin, running: false, categories: [hiddenCat]);

        Assert.True(await OnGuestMenuAsync(item, cat));
        Assert.Equal(HttpStatusCode.Created, (await admin.PostAsJsonAsync(Sale, Body(item))).StatusCode);

        Assert.False(await OnGuestMenuAsync(hiddenItem, hiddenCat));
        Assert.Equal(HttpStatusCode.BadRequest, (await admin.PostAsJsonAsync(Sale, Body(hiddenItem))).StatusCode);
    }

    [Fact]
    public async Task ASwitchedOffWindow_IsIgnored()
    {
        var admin = await AdminAsync();
        var (cat, item) = await DishAsync();
        await ScheduleAsync(admin, running: false, items: [item], isActive: false);
        Assert.True(await OnGuestMenuAsync(item, cat));
        Assert.Equal(HttpStatusCode.Created, (await admin.PostAsJsonAsync(Sale, Body(item))).StatusCode);
    }

    [Fact]
    public async Task PercentPrice_AppliesToTheWholeCategory_AndShowsOnTheMenu()
    {
        var admin = await AdminAsync();
        var (cat, item) = await DishAsync();
        await ScheduleAsync(admin, running: true, prices: [new { categoryId = cat, percentOff = 20m }]);

        var shown = (await GuestItemAsync(item))!.Value;
        Assert.Equal(8m, shown.GetProperty("price").GetDecimal());
        Assert.Equal(10m, shown.GetProperty("regularPrice").GetDecimal());
        var sale = await admin.PostAsJsonAsync(Sale, Body(item));
        Assert.Equal(8m, (await sale.Content.ReadFromJsonAsync<JsonElement>()).GetProperty("total").GetDecimal());
    }

    [Fact]
    public async Task FixedPrice_ForADish_BeatsTheCategoryRule_AndOutsideTheWindowNothingChanges()
    {
        var admin = await AdminAsync();
        var (cat, item) = await DishAsync();
        var (otherCat, other) = await DishAsync();
        await ScheduleAsync(admin, running: true, prices: [new { categoryId = cat, percentOff = 50m }, new { menuItemId = item, price = 7m }]);
        await ScheduleAsync(admin, running: false, prices: [new { categoryId = otherCat, percentOff = 50m }]);

        Assert.Equal(7m, (await GuestItemAsync(item))!.Value.GetProperty("price").GetDecimal());
        var late = (await GuestItemAsync(other))!.Value;
        Assert.Equal(10m, late.GetProperty("price").GetDecimal());
        Assert.False(late.TryGetProperty("regularPrice", out var was) && was.ValueKind != JsonValueKind.Null);
        var sale = await admin.PostAsJsonAsync(Sale, Body(other));
        Assert.Equal(10m, (await sale.Content.ReadFromJsonAsync<JsonElement>()).GetProperty("total").GetDecimal());
        var fixedSale = await admin.PostAsJsonAsync(Sale, Body(item));
        Assert.Equal(7m, (await fixedSale.Content.ReadFromJsonAsync<JsonElement>()).GetProperty("total").GetDecimal());
    }

    [Fact]
    public async Task SizesFollowAPercentRule_ButNotAFixedPrice()
    {
        var admin = await AdminAsync();
        var (_, fixedItem) = await DishAsync();
        var (_, percentItem) = await DishAsync();
        var fixedSize = await ApiHelpers.AddVariantAsync(factory.Services, fixedItem, "Large", 20m);
        var percentSize = await ApiHelpers.AddVariantAsync(factory.Services, percentItem, "Large", 20m);
        await ScheduleAsync(admin, running: true, prices: [new { menuItemId = fixedItem, price = 5m }, new { menuItemId = percentItem, percentOff = 25m }]);

        Assert.Equal(20m, (await GuestItemAsync(fixedItem))!.Value.GetProperty("variants")[0].GetProperty("price").GetDecimal());
        var cut = (await GuestItemAsync(percentItem))!.Value.GetProperty("variants")[0];
        Assert.Equal(15m, cut.GetProperty("price").GetDecimal());
        Assert.Equal(20m, cut.GetProperty("regularPrice").GetDecimal());

        object Large(Guid item, Guid size) => new
        {
            locationId = LocationId, paymentMethod = "Cash", idempotencyKey = Guid.NewGuid().ToString(),
            items = new[] { new { menuItemId = item, variantId = size, quantity = 2, toppingIds = Array.Empty<Guid>() } },
        };
        var a = await admin.PostAsJsonAsync(Sale, Large(fixedItem, fixedSize));
        Assert.Equal(40m, (await a.Content.ReadFromJsonAsync<JsonElement>()).GetProperty("total").GetDecimal());
        var b = await admin.PostAsJsonAsync(Sale, Large(percentItem, percentSize));
        Assert.Equal(30m, (await b.Content.ReadFromJsonAsync<JsonElement>()).GetProperty("total").GetDecimal());
    }

    private static async Task<List<JsonElement>> Schedules(HttpClient admin)
        => (await admin.GetFromJsonAsync<JsonElement>("/api/v1/admin/menu/schedules")).EnumerateArray().ToList();

    [Fact]
    public async Task EachBranchUsesItsOwnClock()
    {
        var admin = await AdminAsync();
        Guid east;
        await using (var scope = factory.Services.CreateAsyncScope())
        {
            var db = scope.ServiceProvider.GetRequiredService<ApplicationDbContext>();
            var loc = new Location { Name = Unique("East"), Address = "a", WorkingHours = "9-18", ContactPhone = "1", TimeZone = "Pacific/Kiritimati" };
            db.Locations.Add(loc);
            await db.SaveChangesAsync();
            east = loc.Id;
        }
        var (cat, item) = await DishAsync();
        // The window is running on Kiritimati's clock (UTC+14) and not on the default Almaty clock (UTC+5).
        var (start, end) = Window(true, "Pacific/Kiritimati");
        var res = await admin.PostAsJsonAsync("/api/v1/admin/menu/schedules", new
        { name = Unique("East"), days = 127, start, end, itemIds = new[] { item } });
        Assert.Equal(HttpStatusCode.Created, res.StatusCode);

        Assert.NotNull(await GuestItemAsync(item, east));
        Assert.Null(await GuestItemAsync(item, LocationId));
    }

    [Fact]
    public async Task ABranchWindow_ControlsOnlyThatBranch()
    {
        var admin = await AdminAsync();
        Guid other;
        await using (var scope = factory.Services.CreateAsyncScope())
        {
            var db = scope.ServiceProvider.GetRequiredService<ApplicationDbContext>();
            var loc = new Location { Name = Unique("Other"), Address = "a", WorkingHours = "9-18", ContactPhone = "1" };
            db.Locations.Add(loc);
            await db.SaveChangesAsync();
            other = loc.Id;
        }
        var (_, item) = await DishAsync();
        await ScheduleAsync(admin, running: false, items: [item], locationId: other);
        // The window belongs to the other branch, so it is hidden there and untouched at the default branch.
        Assert.Null(await GuestItemAsync(item, other));
        Assert.NotNull(await GuestItemAsync(item, LocationId));
    }

    [Fact]
    public async Task TheMenuIsNotServedFromAStaleCache_WhenAWindowOpensOrCloses()
    {
        var admin = await AdminAsync();
        var (cat, item) = await DishAsync();
        var id = await ScheduleAsync(admin, running: false, items: [item]);
        Assert.False(await OnGuestMenuAsync(item, cat)); // cached as hidden

        var (start, end) = Window(true);
        var move = await admin.PutAsJsonAsync($"/api/v1/admin/menu/schedules/{id}", new
        { name = Unique("Menu"), days = 127, start, end, itemIds = new[] { item } });
        Assert.Equal(HttpStatusCode.OK, move.StatusCode);
        Assert.True(await OnGuestMenuAsync(item, cat));
    }

    [Fact]
    public async Task SavingChecksTheInput_AndAChangeIsReported()
    {
        var admin = await AdminAsync();
        var (cat, item) = await DishAsync();
        Task<HttpResponseMessage> Post(object body) => admin.PostAsJsonAsync("/api/v1/admin/menu/schedules", body);

        Assert.Equal(HttpStatusCode.BadRequest, (await Post(new { name = "", days = 127, start = "07:00", end = "11:00" })).StatusCode);
        Assert.Equal(HttpStatusCode.BadRequest, (await Post(new { name = "x", days = 0, start = "07:00", end = "11:00" })).StatusCode);
        Assert.Equal(HttpStatusCode.BadRequest, (await Post(new { name = "x", days = 127, start = "7", end = "11:00" })).StatusCode);
        Assert.Equal(HttpStatusCode.BadRequest, (await Post(new { name = "x", days = 127, start = "07:00", end = "11:00", prices = new[] { new { categoryId = cat, price = 5m, percentOff = 10m } } })).StatusCode);
        Assert.Equal(HttpStatusCode.BadRequest, (await Post(new { name = "x", days = 127, start = "07:00", end = "11:00", prices = new[] { new { menuItemId = item, categoryId = cat, price = 5m } } })).StatusCode);
        Assert.Equal(HttpStatusCode.BadRequest, (await Post(new { name = "x", days = 127, start = "07:00", end = "11:00", prices = new[] { new { menuItemId = item, percentOff = 150m } } })).StatusCode);

        var ok = await Post(new { name = "Breakfast", days = 31, start = "07:00", end = "11:00", categoryIds = new[] { cat } });
        Assert.Equal(HttpStatusCode.Created, ok.StatusCode);
        var body = await ok.Content.ReadFromJsonAsync<JsonElement>();
        Assert.Equal("07:00", body.GetProperty("start").GetString());
        Assert.Equal(cat, body.GetProperty("categoryIds")[0].GetGuid());

        var next = await factory.CreateClient().GetFromJsonAsync<JsonElement>($"/api/v1/menu/next-change?locationId={LocationId}");
        Assert.True(next.GetProperty("at").GetDateTime() > DateTime.UtcNow);
    }

    [Fact]
    public async Task ABranchTimeZone_MustBeAKnownName()
    {
        var admin = await AdminAsync();
        var list = await admin.GetFromJsonAsync<JsonElement>("/api/v1/admin/locations");
        var loc = list.EnumerateArray().First();
        var id = loc.GetProperty("id").GetGuid();
        object Update(string zone) => new
        {
            name = loc.GetProperty("name").GetString(), address = loc.GetProperty("address").GetString(),
            workingHours = loc.GetProperty("workingHours").GetString(), contactPhone = loc.GetProperty("contactPhone").GetString(),
            isActive = true, timeZone = zone,
        };
        Assert.Equal(HttpStatusCode.UnprocessableEntity, (await admin.PutAsJsonAsync($"/api/v1/admin/locations/{id}", Update("Mars/Olympus"))).StatusCode);
        var ok = await admin.PutAsJsonAsync($"/api/v1/admin/locations/{id}", Update("Asia/Almaty"));
        Assert.Equal(HttpStatusCode.OK, ok.StatusCode);
        Assert.Equal("Asia/Almaty", (await ok.Content.ReadFromJsonAsync<JsonElement>()).GetProperty("timeZone").GetString());
    }
}
