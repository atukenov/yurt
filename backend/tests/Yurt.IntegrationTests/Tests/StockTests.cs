using System.Net;
using System.Net.Http.Headers;
using System.Net.Http.Json;
using System.Text.Json;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Yurt.Application.Common.Interfaces;
using Yurt.Domain;
using Yurt.Domain.Entities;
using Yurt.Domain.Enums;
using Yurt.Infrastructure.Persistence;
using Yurt.IntegrationTests.Helpers;

namespace Yurt.IntegrationTests.Tests;

/// <summary>Per-branch stock counts and the automatic stop-list (own_pos, Phase 6).</summary>
[Collection("Integration")]
public class StockTests(YurtWebAppFactory factory)
{
    private static readonly Guid LocationId = Guid.Parse("11111111-0000-0000-0000-000000000001");

    private static object License(bool enabled)
        => new { enabled, validUntil = (DateTime?)null, source = "Manual", notes = (string?)null };

    private async Task<Guid> NewItemAsync(string name)
    {
        await using var scope = factory.Services.CreateAsyncScope();
        var db = scope.ServiceProvider.GetRequiredService<ApplicationDbContext>();
        var category = await db.MenuCategories.FirstAsync();
        var item = new MenuItem { CategoryId = category.Id, Name = name, Description = "test", Price = 5m, IsAvailable = true };
        db.MenuItems.Add(item);
        await db.SaveChangesAsync();
        await scope.ServiceProvider.GetRequiredService<IMenuCacheService>().InvalidateAllMenuAsync(); // inserted behind the app's back
        return item.Id;
    }

    private static Task<HttpResponseMessage> OrderAsync(HttpClient customer, Guid itemId, int quantity)
        => customer.PostAsJsonAsync("/api/v1/orders", new
        {
            locationId = LocationId, paymentMethod = "Cash",
            items = new[] { new { menuItemId = itemId, quantity, toppings = Array.Empty<object>() } },
        });

    private static async Task<bool> OnMenuAsync(HttpClient client, Guid itemId)
    {
        var json = await client.GetFromJsonAsync<JsonElement>($"/api/v1/menu?locationId={LocationId}");
        var items = json.ValueKind == JsonValueKind.Array ? json : json.GetProperty("items");
        return items.EnumerateArray().Any(i => i.GetProperty("id").GetGuid() == itemId);
    }

    [Fact]
    public async Task CountedItem_LimitsOrders_AcceptLowersIt_AndSoldOutHidesIt()
    {
        var itemId = await NewItemAsync($"Stock cake {Guid.NewGuid():N}"[..24]);
        var admin = factory.CreateClient();
        admin.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer",
            await ApiHelpers.CreateAdminTokenAsync(factory.Services, admin));
        var customer = factory.CreateClient();
        var (token, _) = await ApiHelpers.CreateCustomerAsync(customer, "+77002990101");
        ApiHelpers.Authorize(customer, token);

        Assert.True(await OnMenuAsync(customer, itemId)); // not counted yet: unlimited
        var set = await admin.PutAsJsonAsync($"/api/v1/admin/stock/{itemId}/{LocationId}", new { quantity = 2, lowStockAt = 1 });
        Assert.Equal(HttpStatusCode.OK, set.StatusCode);

        var tooMany = await OrderAsync(customer, itemId, 3);
        Assert.Equal(HttpStatusCode.BadRequest, tooMany.StatusCode);
        Assert.Contains("Only 2 left", await tooMany.Content.ReadAsStringAsync());

        var placed = await OrderAsync(customer, itemId, 2);
        Assert.Equal(HttpStatusCode.Created, placed.StatusCode);
        var order = await placed.Content.ReadFromJsonAsync<ApiHelpers.OrderResult>(ApiHelpers.JsonOpts);
        (await admin.PostAsJsonAsync($"/api/v1/admin/orders/{order!.Id}/payment", new { paymentStatus = "Paid", paymentMethod = "Cash" }))
            .EnsureSuccessStatusCode();
        (await admin.PostAsJsonAsync($"/api/v1/admin/orders/{order.Id}/accept", new { etaMinutes = 5 })).EnsureSuccessStatusCode();

        var list = await admin.GetFromJsonAsync<List<JsonElement>>($"/api/v1/admin/stock?locationId={LocationId}");
        var row = list!.Single(r => r.GetProperty("menuItemId").GetGuid() == itemId);
        Assert.Equal(0, row.GetProperty("quantity").GetInt32());
        Assert.True(row.GetProperty("isSoldOut").GetBoolean());

        Assert.False(await OnMenuAsync(customer, itemId)); // the automatic stop-list
        var soldOut = await OrderAsync(customer, itemId, 1);
        Assert.Equal(HttpStatusCode.BadRequest, soldOut.StatusCode);
        Assert.Contains("sold out", await soldOut.Content.ReadAsStringAsync());

        // Refilling puts it back on the menu; clearing the count makes it unlimited again.
        await admin.PutAsJsonAsync($"/api/v1/admin/stock/{itemId}/{LocationId}", new { quantity = 5, lowStockAt = (int?)null });
        Assert.True(await OnMenuAsync(customer, itemId));
        await admin.PutAsJsonAsync($"/api/v1/admin/stock/{itemId}/{LocationId}", new { quantity = (int?)null, lowStockAt = (int?)null });
        Assert.Equal(HttpStatusCode.Created, (await OrderAsync(customer, itemId, 9)).StatusCode);
    }

    [Fact]
    public async Task OwnPosOff_StockIsNotEnforced_AndThePageIsBlocked()
    {
        var itemId = await NewItemAsync($"Stock gate {Guid.NewGuid():N}"[..24]);
        var admin = factory.CreateClient();
        admin.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer",
            await ApiHelpers.CreateAdminTokenAsync(factory.Services, admin));
        var super = factory.CreateClient();
        super.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer",
            await ApiHelpers.CreateAdminTokenAsync(factory.Services, super,
                username: "platform-owner", password: "PlatformOwner@123", role: AdminRole.SuperAdmin));
        var customer = factory.CreateClient();
        var (token, _) = await ApiHelpers.CreateCustomerAsync(customer, "+77002990102");
        ApiHelpers.Authorize(customer, token);

        await admin.PutAsJsonAsync($"/api/v1/admin/stock/{itemId}/{LocationId}", new { quantity = 0, lowStockAt = (int?)null });
        Assert.False(await OnMenuAsync(customer, itemId));

        try
        {
            await super.PutAsJsonAsync($"/api/v1/platform/modules/{ModuleKeys.OwnPos}", License(false));

            var blocked = await admin.GetAsync($"/api/v1/admin/stock?locationId={LocationId}");
            Assert.Equal(HttpStatusCode.Forbidden, blocked.StatusCode);
            Assert.True(await OnMenuAsync(customer, itemId)); // nothing is stuck at zero
            Assert.Equal(HttpStatusCode.Created, (await OrderAsync(customer, itemId, 1)).StatusCode);
        }
        finally
        {
            await super.PutAsJsonAsync($"/api/v1/platform/modules/{ModuleKeys.OwnPos}", License(true));
        }
    }
}
