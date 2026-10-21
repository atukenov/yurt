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

/// <summary>Paying one order in parts with different methods (own_pos, Phase 6).</summary>
[Collection("Integration")]
public class SplitPaymentTests(YurtWebAppFactory factory)
{
    private static readonly Guid LocationId = Guid.Parse("11111111-0000-0000-0000-000000000001");

    [Fact]
    public async Task SplitPayment_MustAddUp_AndEachPartLandsUnderItsOwnMethodInTheZReport()
    {
        var admin = factory.CreateClient();
        admin.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer",
            await ApiHelpers.CreateAdminTokenAsync(factory.Services, admin));

        var existing = await admin.GetStringAsync($"/api/v1/admin/shifts/current?locationId={LocationId}");
        if (!string.IsNullOrWhiteSpace(existing))
            (await admin.PostAsJsonAsync($"/api/v1/admin/shifts/{JsonDocument.Parse(existing).RootElement.GetProperty("id").GetGuid()}/close", new { countedCash = 0m }))
                .EnsureSuccessStatusCode();
        var opened = await admin.PostAsJsonAsync("/api/v1/admin/shifts/open", new { locationId = LocationId, openingCash = 50m });
        var shiftId = (await opened.Content.ReadFromJsonAsync<JsonElement>()).GetProperty("id").GetGuid();

        Guid itemId;
        await using (var scope = factory.Services.CreateAsyncScope())
        {
            var db = scope.ServiceProvider.GetRequiredService<ApplicationDbContext>();
            var item = new MenuItem { CategoryId = (await db.MenuCategories.FirstAsync()).Id, Name = $"Split item {Guid.NewGuid():N}"[..20], Description = "t", Price = 10m, IsAvailable = true };
            db.MenuItems.Add(item);
            await db.SaveChangesAsync();
            itemId = item.Id;
            await scope.ServiceProvider.GetRequiredService<Yurt.Application.Common.Interfaces.IMenuCacheService>().InvalidateAllMenuAsync();
        }
        var customer = factory.CreateClient();
        var (token, _) = await ApiHelpers.CreateCustomerAsync(customer, "+77002990302");
        ApiHelpers.Authorize(customer, token);
        var placed = await customer.PostAsJsonAsync("/api/v1/orders", new
        {
            locationId = LocationId, paymentMethod = "Cash",
            items = new[] { new { menuItemId = itemId, quantity = 2, toppings = Array.Empty<object>() } },
        });
        var order = await placed.Content.ReadFromJsonAsync<ApiHelpers.OrderResult>(ApiHelpers.JsonOpts);
        var url = $"/api/v1/admin/orders/{order!.Id}/split-payment";

        // Parts that do not add up to the 20 to pay, an unknown method and an empty list are all refused.
        Assert.Equal(HttpStatusCode.BadRequest, (await admin.PutAsJsonAsync(url, new { lines = new[] { new { method = "Cash", amount = 5m }, new { method = "Card", amount = 5m } } })).StatusCode);
        Assert.Equal(HttpStatusCode.BadRequest, (await admin.PutAsJsonAsync(url, new { lines = new[] { new { method = "Gold", amount = 20m } } })).StatusCode);
        Assert.Equal(HttpStatusCode.BadRequest, (await admin.PutAsJsonAsync(url, new { lines = Array.Empty<object>() })).StatusCode);

        var saved = await admin.PutAsJsonAsync(url, new { lines = new[] { new { method = "Cash", amount = 5m }, new { method = "Card", amount = 15m } } });
        saved.EnsureSuccessStatusCode();
        var body = await saved.Content.ReadFromJsonAsync<JsonElement>();
        Assert.Equal(20m, body.GetProperty("toPay").GetDecimal());
        Assert.Equal(2, body.GetProperty("lines").GetArrayLength());

        // Saving again replaces the parts instead of adding to them.
        (await admin.PutAsJsonAsync(url, new { lines = new[] { new { method = "Cash", amount = 5m }, new { method = "Card", amount = 15m } } })).EnsureSuccessStatusCode();
        Assert.Equal(2, (await admin.GetFromJsonAsync<JsonElement>(url)).GetProperty("lines").GetArrayLength());

        // Paying marked the order paid, so it can be accepted and finished.
        (await admin.PostAsJsonAsync($"/api/v1/admin/orders/{order.Id}/accept", new { etaMinutes = 5 })).EnsureSuccessStatusCode();
        foreach (var status in new[] { "Preparing", "Ready", "Completed" })
            (await admin.PostAsJsonAsync($"/api/v1/admin/orders/{order.Id}/status", new { status })).EnsureSuccessStatusCode();

        var z = await (await admin.PostAsJsonAsync($"/api/v1/admin/shifts/{shiftId}/close", new { countedCash = 55m })).Content.ReadFromJsonAsync<JsonElement>();
        Assert.Equal(5m, z.GetProperty("cashSales").GetDecimal());
        Assert.Equal(55m, z.GetProperty("expectedCash").GetDecimal());
        var byMethod = z.GetProperty("byMethod").EnumerateArray().ToDictionary(m => m.GetProperty("method").GetString()!, m => m.GetProperty("amount").GetDecimal());
        Assert.Equal(15m, byMethod["Card"]);
        Assert.Equal(5m, byMethod["Cash"]);
    }
}
