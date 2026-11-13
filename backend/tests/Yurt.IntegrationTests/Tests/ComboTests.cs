using System.Net;
using System.Net.Http.Json;
using System.Text.Json;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Yurt.Application.Common.Interfaces;
using Yurt.Domain.Entities;
using Yurt.Domain.Enums;
using Yurt.Infrastructure.Persistence;
using Yurt.IntegrationTests.Helpers;

namespace Yurt.IntegrationTests.Tests;

/// <summary>
/// Combos: dishes sold together at a set price. Guests and the till pay the combo price, stock and cost follow the dishes
/// inside, a combo is off the menu while one of them is sold out, and the CEO's report shows what guests saved.
/// </summary>
[Collection("Integration")]
public class ComboTests(YurtWebAppFactory factory)
{
    private static readonly Guid LocationId = Guid.Parse("11111111-0000-0000-0000-000000000001");
    private const string Combos = "/api/v1/admin/combos";

    private async Task<HttpClient> ClientAsync(string username = "testadmin", string password = "TestAdmin@123", AdminRole role = AdminRole.Admin)
    {
        var client = factory.CreateClient();
        ApiHelpers.Authorize(client, await ApiHelpers.CreateAdminTokenAsync(factory.Services, client, username, password, role: role));
        return client;
    }

    private async Task<HttpClient> GuestAsync()
    {
        var guest = factory.CreateClient();
        var (token, _) = await ApiHelpers.CreateCustomerAsync(guest, $"+7701{Random.Shared.Next(1000000, 9999999)}");
        ApiHelpers.Authorize(guest, token);
        return guest;
    }

    private async Task<(Guid Category, Guid Item)> NewItemAsync(string name, decimal price)
    {
        await using var scope = factory.Services.CreateAsyncScope();
        var db = scope.ServiceProvider.GetRequiredService<ApplicationDbContext>();
        var category = await db.MenuCategories.FirstAsync();
        var item = new MenuItem { CategoryId = category.Id, Name = name, Description = "t", Price = price, IsAvailable = true };
        db.MenuItems.Add(item);
        await db.SaveChangesAsync();
        await scope.ServiceProvider.GetRequiredService<IMenuCacheService>().InvalidateAllMenuAsync();
        return (category.Id, item.Id);
    }

    private static string Unique(string p) => $"{p} {Guid.NewGuid():N}"[..16];

    private static object Save(Guid category, string name, decimal price, params (Guid Item, int Qty)[] parts) => new
    {
        name, categoryId = category, price, isAvailable = true,
        parts = parts.Select(p => new { itemId = p.Item, quantity = p.Qty }).ToArray(),
    };

    private static async Task<bool> OnMenuAsync(HttpClient client, Guid itemId)
    {
        var json = await client.GetFromJsonAsync<JsonElement>($"/api/v1/menu?locationId={LocationId}");
        var items = json.ValueKind == JsonValueKind.Array ? json : json.GetProperty("items");
        return items.EnumerateArray().Any(i => i.GetProperty("id").GetGuid() == itemId);
    }

    private async Task<int> StockOfAsync(HttpClient admin, Guid item)
    {
        var list = await admin.GetFromJsonAsync<List<JsonElement>>($"/api/v1/admin/stock?locationId={LocationId}");
        return list!.Single(r => r.GetProperty("menuItemId").GetGuid() == item).GetProperty("quantity").GetInt32();
    }

    [Fact]
    public async Task Combo_IsOrderedAtItsPrice_StockFollowsTheDishes_AndSoldOutHidesIt()
    {
        var admin = await ClientAsync();
        var (cat, coffee) = await NewItemAsync(Unique("Coffee"), 5m);
        var (_, cake) = await NewItemAsync(Unique("Cake"), 5m);
        var name = Unique("Combo");

        var made = await admin.PostAsJsonAsync(Combos, Save(cat, name, 8m, (coffee, 1), (cake, 1)));
        Assert.Equal(HttpStatusCode.Created, made.StatusCode);
        var combo = await made.Content.ReadFromJsonAsync<JsonElement>();
        var comboId = combo.GetProperty("id").GetGuid();
        Assert.Equal(10m, combo.GetProperty("regularTotal").GetDecimal());
        Assert.Equal(2m, combo.GetProperty("saving").GetDecimal());

        // The guest menu lists it with what is inside.
        var guest = await GuestAsync();
        var shown = await guest.GetFromJsonAsync<JsonElement>($"/api/v1/menu/items/{comboId}?lang=en");
        Assert.True(shown.GetProperty("isCombo").GetBoolean());
        Assert.Equal(2, shown.GetProperty("comboItems").GetArrayLength());
        Assert.Equal(8m, shown.GetProperty("price").GetDecimal());
        Assert.True(await OnMenuAsync(guest, comboId));

        // Counted dishes: 3 coffees, 1 cake.
        (await admin.PutAsJsonAsync($"/api/v1/admin/stock/{coffee}/{LocationId}", new { quantity = 3, lowStockAt = (int?)null })).EnsureSuccessStatusCode();
        (await admin.PutAsJsonAsync($"/api/v1/admin/stock/{cake}/{LocationId}", new { quantity = 1, lowStockAt = (int?)null })).EnsureSuccessStatusCode();

        // Two combos need two cakes: refused and says which dish.
        var two = await guest.PostAsJsonAsync("/api/v1/orders", new
        {
            locationId = LocationId, paymentMethod = "Cash", items = new[] { new { menuItemId = comboId, quantity = 2 } },
        });
        Assert.Equal(HttpStatusCode.BadRequest, two.StatusCode);
        Assert.Contains("Only 1 left", await two.Content.ReadAsStringAsync());

        var placed = await guest.PostAsJsonAsync("/api/v1/orders", new
        {
            locationId = LocationId, paymentMethod = "Cash", items = new[] { new { menuItemId = comboId, quantity = 1 } },
        });
        Assert.Equal(HttpStatusCode.Created, placed.StatusCode);
        var order = await placed.Content.ReadFromJsonAsync<JsonElement>();
        Assert.Equal(8m, order.GetProperty("total").GetDecimal());
        var line = order.GetProperty("items")[0];
        Assert.Equal(2, line.GetProperty("comboParts").GetArrayLength());

        var orderId = order.GetProperty("id").GetGuid();
        (await admin.PostAsJsonAsync($"/api/v1/admin/orders/{orderId}/payment", new { paymentStatus = "Paid", paymentMethod = "Cash" })).EnsureSuccessStatusCode();
        (await admin.PostAsJsonAsync($"/api/v1/admin/orders/{orderId}/accept", new { etaMinutes = 5 })).EnsureSuccessStatusCode();

        // Stock of the dishes went down; the cake is out, so the combo is off the menu and can't be ordered.
        Assert.Equal(2, await StockOfAsync(admin, coffee));
        Assert.Equal(0, await StockOfAsync(admin, cake));
        Assert.False(await OnMenuAsync(guest, comboId));
        var again = await guest.PostAsJsonAsync("/api/v1/orders", new
        {
            locationId = LocationId, paymentMethod = "Cash", items = new[] { new { menuItemId = comboId, quantity = 1 } },
        });
        Assert.Equal(HttpStatusCode.BadRequest, again.StatusCode);

        // Refilling the cake brings the combo back.
        (await admin.PutAsJsonAsync($"/api/v1/admin/stock/{cake}/{LocationId}", new { quantity = 4, lowStockAt = (int?)null })).EnsureSuccessStatusCode();
        Assert.True(await OnMenuAsync(guest, comboId));
    }

    [Fact]
    public async Task CounterSale_OfACombo_UsesTheSetPrice_AddsNoOptions_AndTakesStockOffTheDishes()
    {
        var admin = await ClientAsync();
        var (cat, coffee) = await NewItemAsync(Unique("Coffee"), 6m);
        var (_, bun) = await NewItemAsync(Unique("Bun"), 4m);
        var combo = (await (await admin.PostAsJsonAsync(Combos, Save(cat, Unique("Combo"), 8m, (coffee, 1), (bun, 2))))
            .Content.ReadFromJsonAsync<JsonElement>()).GetProperty("id").GetGuid();
        (await admin.PutAsJsonAsync($"/api/v1/admin/stock/{bun}/{LocationId}", new { quantity = 10, lowStockAt = (int?)null })).EnsureSuccessStatusCode();

        object Sale(object[] items) => new { locationId = LocationId, paymentMethod = "Cash", idempotencyKey = Guid.NewGuid().ToString(), items };

        var sale = await admin.PostAsJsonAsync("/api/v1/admin/counter-sales", Sale([new { menuItemId = combo, quantity = 3 }]));
        Assert.Equal(HttpStatusCode.Created, sale.StatusCode);
        var body = await sale.Content.ReadFromJsonAsync<JsonElement>();
        Assert.Equal(24m, body.GetProperty("total").GetDecimal());
        Assert.Equal(2, body.GetProperty("items")[0].GetProperty("comboParts").GetArrayLength());
        Assert.Equal(4, await StockOfAsync(admin, bun)); // 3 combos × 2 buns

        // A combo has a set price: add-ons are refused.
        var withOption = await admin.PostAsJsonAsync("/api/v1/admin/counter-sales",
            Sale([new { menuItemId = combo, quantity = 1, toppingIds = new[] { Guid.NewGuid() } }]));
        Assert.Equal(HttpStatusCode.BadRequest, withOption.StatusCode);
    }

    [Fact]
    public async Task ComboWithADishOnTheStopList_CannotBeSold()
    {
        var admin = await ClientAsync();
        var (cat, a) = await NewItemAsync(Unique("A"), 3m);
        var (_, b) = await NewItemAsync(Unique("B"), 3m);
        var combo = (await (await admin.PostAsJsonAsync(Combos, Save(cat, Unique("Combo"), 5m, (a, 1), (b, 1))))
            .Content.ReadFromJsonAsync<JsonElement>()).GetProperty("id").GetGuid();

        await using (var scope = factory.Services.CreateAsyncScope())
        {
            var db = scope.ServiceProvider.GetRequiredService<ApplicationDbContext>();
            (await db.MenuItems.FirstAsync(m => m.Id == b)).IsAvailable = false;
            await db.SaveChangesAsync();
            await scope.ServiceProvider.GetRequiredService<IMenuCacheService>().InvalidateAllMenuAsync();
        }

        var guest = await GuestAsync();
        Assert.False(await OnMenuAsync(guest, combo));
        var order = await guest.PostAsJsonAsync("/api/v1/orders", new
        {
            locationId = LocationId, paymentMethod = "Cash", items = new[] { new { menuItemId = combo, quantity = 1 } },
        });
        Assert.Equal(HttpStatusCode.BadRequest, order.StatusCode);
        Assert.Contains("stop-list", await order.Content.ReadAsStringAsync());

        var listed = (await admin.GetFromJsonAsync<List<JsonElement>>(Combos))!.Single(c => c.GetProperty("id").GetGuid() == combo);
        Assert.True(listed.GetProperty("blocked").GetBoolean());
    }

    [Fact]
    public async Task ComboRules_AreChecked_AndADishInsideCannotBeDeleted()
    {
        var admin = await ClientAsync();
        var (cat, a) = await NewItemAsync(Unique("A"), 3m);
        var (_, b) = await NewItemAsync(Unique("B"), 3m);

        Assert.Equal(HttpStatusCode.BadRequest, (await admin.PostAsJsonAsync(Combos, Save(cat, "", 5m, (a, 1), (b, 1)))).StatusCode);        // no name
        Assert.Equal(HttpStatusCode.BadRequest, (await admin.PostAsJsonAsync(Combos, Save(cat, Unique("C"), 5m, (a, 1)))).StatusCode);        // one piece only
        Assert.Equal(HttpStatusCode.BadRequest, (await admin.PostAsJsonAsync(Combos, Save(cat, Unique("C"), 5m))).StatusCode);                // nothing inside
        Assert.Equal(HttpStatusCode.BadRequest, (await admin.PostAsJsonAsync(Combos, Save(cat, Unique("C"), 5m, (Guid.NewGuid(), 2)))).StatusCode); // unknown dish
        Assert.Equal(HttpStatusCode.BadRequest, (await admin.PostAsJsonAsync(Combos, Save(cat, Unique("C"), -1m, (a, 1), (b, 1)))).StatusCode);   // negative price

        var ok = await admin.PostAsJsonAsync(Combos, Save(cat, Unique("Combo"), 5m, (a, 1), (a, 1), (b, 1))); // the same dish twice adds up
        Assert.Equal(HttpStatusCode.Created, ok.StatusCode);
        var made = await ok.Content.ReadFromJsonAsync<JsonElement>();
        var comboId = made.GetProperty("id").GetGuid();
        Assert.Equal(2, made.GetProperty("parts").GetArrayLength());
        Assert.Equal(2, made.GetProperty("parts").EnumerateArray().Single(p => p.GetProperty("itemId").GetGuid() == a).GetProperty("quantity").GetInt32());

        // A combo can't hold another combo.
        Assert.Equal(HttpStatusCode.BadRequest, (await admin.PostAsJsonAsync(Combos, Save(cat, Unique("C"), 5m, (comboId, 1), (b, 1)))).StatusCode);

        // The dish is in a combo: the menu refuses to delete it until the combo drops it.
        var del = await admin.DeleteAsync($"/api/v1/admin/menu/items/{a}");
        Assert.Equal(HttpStatusCode.Conflict, del.StatusCode);

        // Changing the combo works; deleting an unsold combo removes it.
        var edit = await admin.PutAsJsonAsync($"{Combos}/{comboId}", Save(cat, Unique("Renamed"), 6m, (a, 1), (b, 1)));
        Assert.Equal(HttpStatusCode.OK, edit.StatusCode);
        Assert.Equal(6m, (await edit.Content.ReadFromJsonAsync<JsonElement>()).GetProperty("price").GetDecimal());
        Assert.Equal(HttpStatusCode.OK, (await admin.DeleteAsync($"{Combos}/{comboId}")).StatusCode);
        Assert.DoesNotContain((await admin.GetFromJsonAsync<List<JsonElement>>(Combos))!, c => c.GetProperty("id").GetGuid() == comboId);
    }

    [Fact]
    public async Task Report_ShowsSales_Savings_AndCost_ForTheCeoOnly()
    {
        var id = Guid.NewGuid().ToString("N")[..8];
        var ceo = await ClientAsync($"cceo-{id}", "Ceo@123456", AdminRole.Ceo);
        var admin = await ClientAsync();

        Guid branch;
        await using (var scope = factory.Services.CreateAsyncScope())
        {
            var db = scope.ServiceProvider.GetRequiredService<IApplicationDbContext>();
            var location = new Location { Name = $"Combo branch {id}", Address = "x", WorkingHours = "", ContactPhone = "", IsActive = true };
            db.Locations.Add(location);
            await db.SaveChangesAsync();
            branch = location.Id;
        }

        var (cat, coffee) = await NewItemAsync(Unique("Coffee"), 5m);
        var (_, cake) = await NewItemAsync(Unique("Cake"), 5m);
        var beans = (await (await ceo.PostAsJsonAsync("/api/v1/admin/ingredients", new { name = $"Beans {id}", unit = "g" }))
            .Content.ReadFromJsonAsync<JsonElement>()).GetProperty("id").GetGuid();
        (await ceo.PutAsJsonAsync($"/api/v1/admin/ingredients/{beans}/cost", new { costPerUnit = 2m })).EnsureSuccessStatusCode();
        (await ceo.PutAsJsonAsync($"/api/v1/admin/ingredients/recipes/{coffee}",
            new { lines = new[] { new { ingredientId = beans, variantId = (Guid?)null, quantity = 10m } } })).EnsureSuccessStatusCode();
        (await ceo.PutAsJsonAsync($"/api/v1/admin/ingredients/recipes/{cake}",
            new { lines = new[] { new { ingredientId = beans, variantId = (Guid?)null, quantity = 5m } } })).EnsureSuccessStatusCode();
        var combo = (await (await admin.PostAsJsonAsync(Combos, Save(cat, Unique("Combo"), 8m, (coffee, 1), (cake, 1))))
            .Content.ReadFromJsonAsync<JsonElement>()).GetProperty("id").GetGuid();

        var guest = await GuestAsync();
        var placed = await guest.PostAsJsonAsync("/api/v1/orders", new
        {
            locationId = branch, paymentMethod = "Cash", items = new[] { new { menuItemId = combo, quantity = 2 } },
        });
        Assert.Equal(HttpStatusCode.Created, placed.StatusCode);
        var orderId = (await placed.Content.ReadFromJsonAsync<ApiHelpers.OrderResult>(ApiHelpers.JsonOpts))!.Id;
        (await ceo.PostAsJsonAsync($"/api/v1/admin/orders/{orderId}/payment", new { paymentStatus = "Paid", paymentMethod = "Cash" })).EnsureSuccessStatusCode();
        (await ceo.PostAsJsonAsync($"/api/v1/admin/orders/{orderId}/accept", new { etaMinutes = 5 })).EnsureSuccessStatusCode();

        var year = 2200 + Random.Shared.Next(0, 700);
        var month = $"{year}-{Random.Shared.Next(2, 13):00}";
        await using (var scope = factory.Services.CreateAsyncScope())
        {
            var db = scope.ServiceProvider.GetRequiredService<IApplicationDbContext>();
            var order = await db.Orders.FirstAsync(o => o.Id == orderId);
            order.Status = OrderStatus.Completed;
            order.CompletedAt = new DateTime(year, int.Parse(month[5..]), 15, 6, 0, 0, DateTimeKind.Utc);
            await db.SaveChangesAsync();
        }

        var report = await ceo.GetFromJsonAsync<JsonElement>($"{Combos}/report?month={month}&locationId={branch}");
        var row = report.GetProperty("items").EnumerateArray().Single();
        Assert.Equal(combo, row.GetProperty("comboId").GetGuid());
        Assert.Equal(2, row.GetProperty("sold").GetInt32());
        Assert.Equal(16m, row.GetProperty("revenue").GetDecimal());
        Assert.Equal(20m, row.GetProperty("regularValue").GetDecimal());
        Assert.Equal(4m, row.GetProperty("saved").GetDecimal());
        Assert.Equal(60m, row.GetProperty("cost").GetDecimal());   // 2 combos × (10 g + 5 g) × 2 per g
        Assert.Equal(-44m, row.GetProperty("profit").GetDecimal());
        var totals = report.GetProperty("totals");
        Assert.Equal(2, totals.GetProperty("sold").GetInt32());
        Assert.Equal(100m, totals.GetProperty("shareOfSales").GetDecimal());
        Assert.Equal(1, totals.GetProperty("ordersWithCombo").GetInt32());

        // Money is for the CEO only.
        Assert.Equal(HttpStatusCode.Forbidden, (await admin.GetAsync($"{Combos}/report?month={month}")).StatusCode);
        Assert.Equal(HttpStatusCode.UnprocessableEntity, (await ceo.GetAsync($"{Combos}/report?month=nope")).StatusCode);
    }
}
