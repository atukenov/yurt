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

/// <summary>Kitchen and Bar each mark their own part of an order ready (own_pos, Phase 6).</summary>
[Collection("Integration")]
public class StationReadyTests(YurtWebAppFactory factory)
{
    private static readonly Guid LocationId = Guid.Parse("11111111-0000-0000-0000-000000000001");

    [Fact]
    public async Task OrderMovesToReady_OnlyWhenEveryStationWithItemsInItIsReady()
    {
        var admin = factory.CreateClient();
        admin.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer",
            await ApiHelpers.CreateAdminTokenAsync(factory.Services, admin));

        var kitchen = await CreateStationAsync(admin, "Kitchen");
        var bar = await CreateStationAsync(admin, "Bar");
        var spare = await CreateStationAsync(admin, "Spare");
        var food = await CreateItemAsync(kitchen);
        var drink = await CreateItemAsync(bar);
        var unrouted = await CreateItemAsync(null);

        try
        {
            var order = await PlaceAcceptedOrderAsync(admin, "+77002990401", food, drink);
            string Url(Guid station) => $"/api/v1/admin/orders/{order}/stations/{station}/ready";

            // A station with nothing in this order cannot mark it, and an unknown station is not found.
            Assert.Equal(HttpStatusCode.BadRequest, (await admin.PostAsync(Url(spare), null)).StatusCode);
            Assert.Equal(HttpStatusCode.NotFound, (await admin.PostAsync(Url(Guid.NewGuid()), null)).StatusCode);

            // The bar is done first: the order waits for the kitchen. The bar can take it back.
            var body = await ReadAsync(await admin.PostAsync(Url(bar), null));
            Assert.Equal("Accepted", body.GetProperty("status").GetString());
            Assert.Equal([bar], ReadyIds(body));
            body = await ReadAsync(await admin.DeleteAsync(Url(bar)));
            Assert.Empty(ReadyIds(body));

            (await admin.PostAsJsonAsync($"/api/v1/admin/orders/{order}/status", new { status = "Preparing" })).EnsureSuccessStatusCode();
            await ReadAsync(await admin.PostAsync(Url(bar), null));
            await ReadAsync(await admin.PostAsync(Url(bar), null)); // Pressing twice changes nothing.
            body = await ReadAsync(await admin.PostAsync(Url(kitchen), null));
            Assert.Equal("Ready", body.GetProperty("status").GetString());
            Assert.Equal(2, ReadyIds(body).Count);

            // Once the order is Ready, parts can no longer be changed; it is handed over as before.
            Assert.Equal(HttpStatusCode.Conflict, (await admin.DeleteAsync(Url(bar))).StatusCode);
            (await admin.PostAsJsonAsync($"/api/v1/admin/orders/{order}/status", new { status = "Completed" })).EnsureSuccessStatusCode();

            // An item with no station has no owner, so the order never moves on its own; staff press Ready as before.
            var mixed = await PlaceAcceptedOrderAsync(admin, "+77002990402", food, unrouted);
            body = await ReadAsync(await admin.PostAsync($"/api/v1/admin/orders/{mixed}/stations/{kitchen}/ready", null));
            Assert.Equal("Accepted", body.GetProperty("status").GetString());
            Assert.Equal([kitchen], ReadyIds(body));
        }
        finally
        {
            foreach (var s in new[] { kitchen, bar, spare }) await admin.DeleteAsync($"/api/v1/admin/stations/{s}");
        }
    }

    private static async Task<Guid> CreateStationAsync(HttpClient admin, string name)
    {
        var res = await admin.PostAsJsonAsync("/api/v1/admin/stations", new { name = $"{name} {Guid.NewGuid():N}"[..20] });
        Assert.Equal(HttpStatusCode.Created, res.StatusCode);
        return (await res.Content.ReadFromJsonAsync<JsonElement>()).GetProperty("id").GetGuid();
    }

    private async Task<Guid> CreateItemAsync(Guid? stationId)
    {
        await using var scope = factory.Services.CreateAsyncScope();
        var db = scope.ServiceProvider.GetRequiredService<ApplicationDbContext>();
        var category = new MenuCategory { Name = $"Ready cat {Guid.NewGuid():N}"[..18], StationId = stationId };
        db.MenuCategories.Add(category);
        await db.SaveChangesAsync();
        var item = new MenuItem { CategoryId = category.Id, Name = $"Ready item {Guid.NewGuid():N}"[..20], Description = "t", Price = 4m, IsAvailable = true };
        db.MenuItems.Add(item);
        await db.SaveChangesAsync();
        await scope.ServiceProvider.GetRequiredService<Yurt.Application.Common.Interfaces.IMenuCacheService>().InvalidateAllMenuAsync();
        return item.Id;
    }

    private async Task<Guid> PlaceAcceptedOrderAsync(HttpClient admin, string phone, params Guid[] items)
    {
        var customer = factory.CreateClient();
        var (token, _) = await ApiHelpers.CreateCustomerAsync(customer, phone);
        ApiHelpers.Authorize(customer, token);
        var placed = await customer.PostAsJsonAsync("/api/v1/orders", new
        {
            locationId = LocationId, paymentMethod = "Cash",
            items = items.Select(i => new { menuItemId = i, quantity = 1, toppings = Array.Empty<object>() }).ToArray(),
        });
        placed.EnsureSuccessStatusCode();
        var order = (await placed.Content.ReadFromJsonAsync<ApiHelpers.OrderResult>(ApiHelpers.JsonOpts))!.Id;
        (await admin.PostAsJsonAsync($"/api/v1/admin/orders/{order}/payment", new { paymentStatus = "Paid" })).EnsureSuccessStatusCode();
        (await admin.PostAsJsonAsync($"/api/v1/admin/orders/{order}/accept", new { etaMinutes = 5 })).EnsureSuccessStatusCode();
        return order;
    }

    private static async Task<JsonElement> ReadAsync(HttpResponseMessage res)
    {
        res.EnsureSuccessStatusCode();
        return await res.Content.ReadFromJsonAsync<JsonElement>();
    }

    private static List<Guid> ReadyIds(JsonElement order)
        => order.GetProperty("readyStationIds").EnumerateArray().Select(e => e.GetGuid()).ToList();
}
