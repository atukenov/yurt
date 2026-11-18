using System.Net;
using System.Net.Http.Headers;
using System.Net.Http.Json;
using System.Text.Json;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Yurt.Domain;
using Yurt.Domain.Entities;
using Yurt.Domain.Enums;
using Yurt.Infrastructure.Persistence;
using Yurt.IntegrationTests.Helpers;

namespace Yurt.IntegrationTests.Tests;

/// <summary>Orders a guest makes on the self-order kiosk (module kiosk): unpaid until the cashier takes the payment.</summary>
[Collection("Integration")]
public class KioskOrderTests(YurtWebAppFactory factory)
{
    private static readonly Guid LocationId = Guid.Parse("11111111-0000-0000-0000-000000000001");
    private const string Url = "/api/v1/admin/kiosk-orders";

    private static object License(bool enabled)
        => new { enabled, validUntil = (DateTime?)null, source = "Manual", notes = (string?)null };

    private async Task<HttpClient> AdminAsync()
    {
        var admin = factory.CreateClient();
        admin.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer",
            await ApiHelpers.CreateAdminTokenAsync(factory.Services, admin));
        return admin;
    }

    private static string Unique(string prefix) => $"{prefix}{Guid.NewGuid():N}"[..(prefix.Length + 6)];

    /// <summary>
    /// A dish (price 10) in a category of its own, with one add-on (price 3) in an option group. The category is new so the
    /// add-on and its rule do not leak into other tests' dishes (they share one database).
    /// </summary>
    private async Task<(Guid Item, Guid Option)> DishAsync(bool required = true)
    {
        await using var scope = factory.Services.CreateAsyncScope();
        var db = scope.ServiceProvider.GetRequiredService<ApplicationDbContext>();
        var category = new MenuCategory { Name = Unique("KCat") };
        var item = new MenuItem { Category = category, Name = $"Kiosk {Guid.NewGuid():N}"[..20], Description = "t", Price = 10m, IsAvailable = true };
        db.MenuCategories.Add(category);
        var group = Unique("kside");
        var option = new MenuTopping { Name = "Oat", Price = 3m, Group = group };
        db.MenuItems.Add(item);
        db.MenuToppings.Add(option);
        db.MenuToppingCategories.Add(new MenuToppingCategory { Topping = option, Category = category });
        if (required) db.MenuOptionGroups.Add(new MenuOptionGroup { Key = group, Title = "side", Required = true, MinSelect = 1, MaxSelect = 1 });
        await db.SaveChangesAsync();
        await scope.ServiceProvider.GetRequiredService<Yurt.Application.Common.Interfaces.IMenuCacheService>().InvalidateAllMenuAsync();
        return (item.Id, option.Id);
    }

    private static object Body(Guid item, string key, Guid[]? options = null, string? type = null, int quantity = 1)
    {
        var body = new Dictionary<string, object?>
        {
            ["locationId"] = LocationId, ["idempotencyKey"] = key,
            ["items"] = new[] { new { menuItemId = item, quantity, toppingIds = options ?? [] } },
        };
        if (type is not null) body["orderType"] = type;
        return body;
    }

    [Fact]
    public async Task KioskOrder_WaitsUnpaid_WithMenuPrices_AndResendingGivesTheSameOrder()
    {
        var admin = await AdminAsync();
        var (item, option) = await DishAsync();
        var key = Guid.NewGuid().ToString();

        var first = await admin.PostAsJsonAsync(Url, Body(item, key, [option], "DineIn", quantity: 2));
        Assert.Equal(HttpStatusCode.Created, first.StatusCode);
        var order = await first.Content.ReadFromJsonAsync<JsonElement>();
        var id = order.GetProperty("id").GetGuid();
        Assert.Equal(26m, order.GetProperty("total").GetDecimal()); // (10 + 3) x 2, from the menu
        Assert.Equal("Created", order.GetProperty("status").GetString());
        Assert.Equal("Unpaid", order.GetProperty("paymentStatus").GetString());
        Assert.Equal("Kiosk", order.GetProperty("source").GetString());
        Assert.Equal("DineIn", order.GetProperty("orderType").GetString());
        Assert.False(order.TryGetProperty("customerUserId", out var guest) && guest.ValueKind != JsonValueKind.Null);

        var again = await admin.PostAsJsonAsync(Url, Body(item, key, [option], "DineIn", quantity: 2));
        Assert.Equal(HttpStatusCode.OK, again.StatusCode);
        Assert.Equal(id, (await again.Content.ReadFromJsonAsync<JsonElement>()).GetProperty("id").GetGuid());

        await using (var scope = factory.Services.CreateAsyncScope())
        {
            var db = scope.ServiceProvider.GetRequiredService<ApplicationDbContext>();
            Assert.Equal(1, await db.Orders.CountAsync(o => o.IdempotencyKey == key));
        }

        // It is not taken on until paid, like an unpaid cash order from the app.
        var early = await admin.PostAsJsonAsync($"/api/v1/admin/orders/{id}/accept", new { etaMinutes = 5 });
        Assert.Equal(HttpStatusCode.UnprocessableEntity, early.StatusCode);

        // The cashier takes the money, then accepts it: from here it is an ordinary paid order.
        (await admin.PostAsJsonAsync($"/api/v1/admin/orders/{id}/payment", new { paymentStatus = "Paid" })).EnsureSuccessStatusCode();
        var accepted = await admin.PostAsJsonAsync($"/api/v1/admin/orders/{id}/accept", new { etaMinutes = 5 });
        accepted.EnsureSuccessStatusCode();
        Assert.Equal("Accepted", (await accepted.Content.ReadFromJsonAsync<JsonElement>()).GetProperty("status").GetString());
    }

    [Fact]
    public async Task KioskOrder_DefaultsToTakeAway_AndTheCashierCanOpenIt()
    {
        var admin = await AdminAsync();
        var (item, _) = await DishAsync(required: false);
        var res = await admin.PostAsJsonAsync(Url, Body(item, Guid.NewGuid().ToString()));
        Assert.True(res.StatusCode == HttpStatusCode.Created, await res.Content.ReadAsStringAsync());
        var order = await res.Content.ReadFromJsonAsync<JsonElement>();
        Assert.Equal("TakeAway", order.GetProperty("orderType").GetString());

        // The cashier's order screen finds it like any other order.
        var seen = await admin.GetFromJsonAsync<JsonElement>($"/api/v1/admin/orders/{order.GetProperty("id").GetGuid()}");
        Assert.Equal("Kiosk", seen.GetProperty("source").GetString());
    }

    [Fact]
    public async Task KioskOrder_FollowsTheOptionRules_TheStopList_AndNeedsAnItem()
    {
        var admin = await AdminAsync();
        var (item, option) = await DishAsync();

        // The required choice was skipped.
        var missing = await admin.PostAsJsonAsync(Url, Body(item, Guid.NewGuid().ToString()));
        Assert.Equal(HttpStatusCode.BadRequest, missing.StatusCode);
        Assert.Contains("choose", await missing.Content.ReadAsStringAsync(), StringComparison.OrdinalIgnoreCase);

        // No items, a missing key, a silly quantity.
        Assert.Equal(HttpStatusCode.BadRequest, (await admin.PostAsJsonAsync(Url,
            new { locationId = LocationId, idempotencyKey = Guid.NewGuid().ToString(), items = Array.Empty<object>() })).StatusCode);
        Assert.Equal(HttpStatusCode.BadRequest, (await admin.PostAsJsonAsync(Url, Body(item, "", [option]))).StatusCode);
        Assert.Equal(HttpStatusCode.BadRequest, (await admin.PostAsJsonAsync(Url, Body(item, Guid.NewGuid().ToString(), [option], quantity: 0))).StatusCode);

        // An item on the stop-list cannot be ordered.
        await using (var scope = factory.Services.CreateAsyncScope())
        {
            var db = scope.ServiceProvider.GetRequiredService<ApplicationDbContext>();
            (await db.MenuItems.SingleAsync(m => m.Id == item)).IsAvailable = false;
            await db.SaveChangesAsync();
        }
        var stopped = await admin.PostAsJsonAsync(Url, Body(item, Guid.NewGuid().ToString(), [option]));
        Assert.Equal(HttpStatusCode.BadRequest, stopped.StatusCode);
        Assert.Contains("stop-list", await stopped.Content.ReadAsStringAsync());
    }

    [Fact]
    public async Task KioskOrder_NeedsStaffSignIn_AndAnOpenBranch()
    {
        var (item, option) = await DishAsync();
        var body = Body(item, Guid.NewGuid().ToString(), [option]);

        // Nobody signed in, or a guest account: refused.
        Assert.Equal(HttpStatusCode.Unauthorized, (await factory.CreateClient().PostAsJsonAsync(Url, body)).StatusCode);
        var customer = factory.CreateClient();
        var (token, _) = await ApiHelpers.CreateCustomerAsync(customer, "+77002990731");
        ApiHelpers.Authorize(customer, token);
        Assert.Equal(HttpStatusCode.Forbidden, (await customer.PostAsJsonAsync(Url, body)).StatusCode);

        var admin = await AdminAsync();
        var elsewhere = await admin.PostAsJsonAsync(Url, new
        {
            locationId = Guid.NewGuid(), idempotencyKey = Guid.NewGuid().ToString(),
            items = new[] { new { menuItemId = item, quantity = 1, toppingIds = new[] { option } } },
        });
        Assert.Equal(HttpStatusCode.BadRequest, elsewhere.StatusCode);
    }

    [Fact]
    public async Task KioskOrder_SwitchedOff_IsRefused()
    {
        var super = factory.CreateClient();
        super.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer",
            await ApiHelpers.CreateAdminTokenAsync(factory.Services, super,
                username: "platform-owner", password: "PlatformOwner@123", role: AdminRole.SuperAdmin));
        var admin = await AdminAsync();
        var (item, option) = await DishAsync();
        try
        {
            await super.PutAsJsonAsync($"/api/v1/platform/modules/{ModuleKeys.Kiosk}", License(false));
            var res = await admin.PostAsJsonAsync(Url, Body(item, Guid.NewGuid().ToString(), [option]));
            Assert.Equal(HttpStatusCode.Forbidden, res.StatusCode);
            Assert.Equal(ModuleKeys.Kiosk, (await res.Content.ReadFromJsonAsync<JsonElement>()).GetProperty("module").GetString());
        }
        finally
        {
            await super.PutAsJsonAsync($"/api/v1/platform/modules/{ModuleKeys.Kiosk}", License(true));
        }
    }
}
