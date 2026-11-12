using System.Net;
using System.Net.Http.Headers;
using System.Net.Http.Json;
using System.Text.Json;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Yurt.Domain.Entities;
using Yurt.Domain.Enums;
using Yurt.Infrastructure.Persistence;
using Yurt.IntegrationTests.Helpers;

namespace Yurt.IntegrationTests.Tests;

/// <summary>Sales rung up at the counter for walk-in guests (Yurt POS).</summary>
[Collection("Integration")]
public class CounterSaleTests(YurtWebAppFactory factory)
{
    private static readonly Guid LocationId = Guid.Parse("11111111-0000-0000-0000-000000000001");
    private const string Url = "/api/v1/admin/counter-sales";

    private async Task<HttpClient> AdminAsync()
    {
        var admin = factory.CreateClient();
        admin.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer",
            await ApiHelpers.CreateAdminTokenAsync(factory.Services, admin));
        return admin;
    }

    /// <summary>A fresh menu item (price 10) with one add-on (price 3) in its category, and another add-on in no category.</summary>
    private async Task<(Guid Item, Guid Topping, Guid OtherTopping)> SeedMenuAsync()
    {
        await using var scope = factory.Services.CreateAsyncScope();
        var db = scope.ServiceProvider.GetRequiredService<ApplicationDbContext>();
        var category = await db.MenuCategories.FirstAsync();
        var item = new MenuItem { CategoryId = category.Id, Name = $"Counter {Guid.NewGuid():N}"[..20], Description = "t", Price = 10m, IsAvailable = true };
        var topping = new MenuTopping { Name = "Syrup", Price = 3m };
        var other = new MenuTopping { Name = "Other", Price = 99m };
        db.MenuItems.Add(item);
        db.MenuToppings.AddRange(topping, other);
        db.MenuToppingCategories.Add(new MenuToppingCategory { ToppingId = topping.Id, CategoryId = category.Id });
        await db.SaveChangesAsync();
        return (item.Id, topping.Id, other.Id);
    }

    [Fact]
    public async Task CounterSale_IsPaidAndTakenOn_WithMenuPrices_AndResendingGivesTheSameSale()
    {
        var admin = await AdminAsync();
        var (item, topping, _) = await SeedMenuAsync();
        var key = Guid.NewGuid().ToString();
        var body = new
        {
            locationId = LocationId, paymentMethod = "Card", idempotencyKey = key, etaMinutes = 4,
            items = new[] { new { menuItemId = item, quantity = 2, toppingIds = new[] { topping } } },
        };

        var first = await admin.PostAsJsonAsync(Url, body);
        Assert.Equal(HttpStatusCode.Created, first.StatusCode);
        var sale = await first.Content.ReadFromJsonAsync<JsonElement>();
        var id = sale.GetProperty("id").GetGuid();
        Assert.Equal(26m, sale.GetProperty("total").GetDecimal()); // (10 + 3) x 2, prices from the menu
        Assert.Equal("Accepted", sale.GetProperty("status").GetString());
        Assert.Equal("Paid", sale.GetProperty("paymentStatus").GetString());
        Assert.False(sale.TryGetProperty("customerUserId", out var guest) && guest.ValueKind != JsonValueKind.Null); // no guest account

        // The till lost the answer and sends the same sale again: the first one comes back, nothing new is made.
        var again = await admin.PostAsJsonAsync(Url, body);
        Assert.Equal(HttpStatusCode.OK, again.StatusCode);
        Assert.Equal(id, (await again.Content.ReadFromJsonAsync<JsonElement>()).GetProperty("id").GetGuid());

        await using var scope = factory.Services.CreateAsyncScope();
        var db = scope.ServiceProvider.GetRequiredService<ApplicationDbContext>();
        var order = await db.Orders.Include(o => o.Items).SingleAsync(o => o.IdempotencyKey == key);
        Assert.Equal(OrderSource.Counter, order.Source);
        Assert.NotNull(order.SoldByAdminId);
        Assert.Equal(4, order.EtaMinutes);
        Assert.Single(order.Items);

        // Staff move it on like any other order, and it shows in the admin list.
        foreach (var status in new[] { "Preparing", "Ready", "Completed" })
            (await admin.PostAsJsonAsync($"/api/v1/admin/orders/{id}/status", new { status })).EnsureSuccessStatusCode();
        var list = await admin.GetFromJsonAsync<JsonElement>($"/api/v1/admin/orders?locationId={LocationId}");
        Assert.Contains(list.GetProperty("items").EnumerateArray(), o => o.GetProperty("id").GetGuid() == id);
    }

    [Fact]
    public async Task CounterSale_KeepsHereOrToGo_AndDefaultsToTakeAway()
    {
        var admin = await AdminAsync();
        var (item, _, _) = await SeedMenuAsync();
        async Task<JsonElement> Sell(object? type)
        {
            var body = new Dictionary<string, object?>
            {
                ["locationId"] = LocationId, ["paymentMethod"] = "Cash", ["idempotencyKey"] = Guid.NewGuid().ToString(),
                ["items"] = new[] { new { menuItemId = item, quantity = 1 } },
            };
            if (type is not null) body["orderType"] = type;
            var res = await admin.PostAsJsonAsync(Url, body);
            Assert.Equal(HttpStatusCode.Created, res.StatusCode);
            return await res.Content.ReadFromJsonAsync<JsonElement>();
        }

        Assert.Equal("TakeAway", (await Sell(null)).GetProperty("orderType").GetString());
        Assert.Equal("DineIn", (await Sell("DineIn")).GetProperty("orderType").GetString());
    }

    [Fact]
    public async Task PrintLabels_WithNoLabelPrinter_QueuesNothing_AndWithOneQueuesAJob()
    {
        var admin = await AdminAsync();
        var (item, _, _) = await SeedMenuAsync();
        var sale = await (await admin.PostAsJsonAsync(Url, new
        {
            locationId = LocationId, paymentMethod = "Cash", idempotencyKey = Guid.NewGuid().ToString(),
            items = new[] { new { menuItemId = item, quantity = 2 } },
        })).Content.ReadFromJsonAsync<JsonElement>();
        var id = sale.GetProperty("id").GetGuid();

        var none = await admin.PostAsync($"/api/v1/admin/orders/{id}/print/labels", null);
        none.EnsureSuccessStatusCode();
        Assert.Equal(0, (await none.Content.ReadFromJsonAsync<JsonElement>()).GetProperty("printers").GetInt32());

        await using (var scope = factory.Services.CreateAsyncScope())
        {
            var db = scope.ServiceProvider.GetRequiredService<ApplicationDbContext>();
            db.Printers.Add(new Printer { LocationId = LocationId, Name = "Cups", Kind = Printer.LabelKind, Host = "10.255.255.1", ViaAgent = true });
            await db.SaveChangesAsync();
        }
        var one = await admin.PostAsync($"/api/v1/admin/orders/{id}/print/labels", null);
        one.EnsureSuccessStatusCode();
        Assert.Equal(1, (await one.Content.ReadFromJsonAsync<JsonElement>()).GetProperty("printers").GetInt32());
    }

    [Fact]
    public async Task OfflineSale_KeepsItsTime_IsHandedOver_AndIsTakenEvenIfTheItemIsNowStopped()
    {
        var admin = await AdminAsync();
        var (item, _, _) = await SeedMenuAsync();
        var soldAt = DateTime.UtcNow.AddHours(-3);

        // The item went on the stop-list after the till sold it offline.
        await using (var scope = factory.Services.CreateAsyncScope())
        {
            var db = scope.ServiceProvider.GetRequiredService<ApplicationDbContext>();
            await db.MenuItems.Where(m => m.Id == item).ExecuteUpdateAsync(u => u.SetProperty(m => m.IsAvailable, false));
        }

        var live = await admin.PostAsJsonAsync(Url, new
        {
            locationId = LocationId, paymentMethod = "Cash", idempotencyKey = Guid.NewGuid().ToString(),
            items = new[] { new { menuItemId = item, quantity = 1 } },
        });
        Assert.Equal(HttpStatusCode.BadRequest, live.StatusCode); // a live sale respects the stop-list

        var offline = await admin.PostAsJsonAsync(Url, new
        {
            locationId = LocationId, paymentMethod = "Cash", idempotencyKey = Guid.NewGuid().ToString(),
            soldAt, handedOver = true,
            items = new[] { new { menuItemId = item, quantity = 1 } },
        });
        Assert.Equal(HttpStatusCode.Created, offline.StatusCode);
        var sale = await offline.Content.ReadFromJsonAsync<JsonElement>();
        Assert.Equal("Completed", sale.GetProperty("status").GetString());
        Assert.True(Math.Abs((sale.GetProperty("createdAt").GetDateTime().ToUniversalTime() - soldAt).TotalSeconds) < 1);
        Assert.True(Math.Abs((sale.GetProperty("completedAt").GetDateTime().ToUniversalTime() - soldAt).TotalSeconds) < 1);

        // No receipt, ticket or drawer job from the server: the till printed it itself while offline.
        await using (var scope = factory.Services.CreateAsyncScope())
        {
            var db = scope.ServiceProvider.GetRequiredService<ApplicationDbContext>();
            var orderId = sale.GetProperty("id").GetGuid();
            Assert.False(await db.PrintJobs.AnyAsync(j => j.OrderId == orderId));
        }
    }

    [Fact]
    public async Task CounterSale_RefusesBadInput()
    {
        var admin = await AdminAsync();
        var (item, _, otherTopping) = await SeedMenuAsync();
        object Sale(object items, string method = "Cash", string? key = "k", DateTime? soldAt = null)
            => new { locationId = LocationId, paymentMethod = method, idempotencyKey = key ?? "", soldAt, items };
        var one = new[] { new { menuItemId = item, quantity = 1 } };

        Assert.Equal(HttpStatusCode.BadRequest, (await admin.PostAsJsonAsync(Url, Sale(one, key: null))).StatusCode);
        Assert.Equal(HttpStatusCode.BadRequest, (await admin.PostAsJsonAsync(Url, Sale(Array.Empty<object>(), key: Guid.NewGuid().ToString()))).StatusCode);
        Assert.Equal(HttpStatusCode.BadRequest, (await admin.PostAsJsonAsync(Url, Sale(one, "Other", Guid.NewGuid().ToString()))).StatusCode);
        Assert.Equal(HttpStatusCode.BadRequest, (await admin.PostAsJsonAsync(Url, Sale(
            new[] { new { menuItemId = item, quantity = 0 } }, key: Guid.NewGuid().ToString()))).StatusCode);
        Assert.Equal(HttpStatusCode.BadRequest, (await admin.PostAsJsonAsync(Url, Sale(
            new[] { new { menuItemId = Guid.NewGuid(), quantity = 1 } }, key: Guid.NewGuid().ToString()))).StatusCode);
        // An add-on that does not belong to the item's category.
        Assert.Equal(HttpStatusCode.BadRequest, (await admin.PostAsJsonAsync(Url, Sale(
            new[] { new { menuItemId = item, quantity = 1, toppingIds = new[] { otherTopping } } }, key: Guid.NewGuid().ToString()))).StatusCode);
        // Too old, or from the future.
        Assert.Equal(HttpStatusCode.BadRequest, (await admin.PostAsJsonAsync(Url, Sale(one, key: Guid.NewGuid().ToString(), soldAt: DateTime.UtcNow.AddDays(-8)))).StatusCode);
        Assert.Equal(HttpStatusCode.BadRequest, (await admin.PostAsJsonAsync(Url, Sale(one, key: Guid.NewGuid().ToString(), soldAt: DateTime.UtcNow.AddHours(1)))).StatusCode);

        // Guests can't use it.
        var customer = factory.CreateClient();
        var (token, _) = await ApiHelpers.CreateCustomerAsync(customer, "+77002990411");
        ApiHelpers.Authorize(customer, token);
        Assert.Equal(HttpStatusCode.Forbidden, (await customer.PostAsJsonAsync(Url, Sale(one, key: Guid.NewGuid().ToString()))).StatusCode);
    }
}
