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

/// <summary>Cashier shifts, cash in/out and the Z report (own_pos, Phase 6).</summary>
[Collection("Integration")]
public class ShiftTests(YurtWebAppFactory factory)
{
    private static readonly Guid LocationId = Guid.Parse("11111111-0000-0000-0000-000000000001");

    [Fact]
    public async Task Shift_CountsCompletedOrders_AndShowsTheCashDifference()
    {
        var admin = factory.CreateClient();
        admin.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer",
            await ApiHelpers.CreateAdminTokenAsync(factory.Services, admin));

        // Clean slate: another test may have left a shift open at the shared branch.
        var existing = await CurrentAsync(admin);
        if (existing is { } open)
            (await admin.PostAsJsonAsync($"/api/v1/admin/shifts/{open.GetProperty("id").GetGuid()}/close", new { countedCash = 0m }))
                .EnsureSuccessStatusCode();

        Assert.Equal(HttpStatusCode.BadRequest, (await admin.PostAsJsonAsync("/api/v1/admin/shifts/open", new { locationId = LocationId, openingCash = -1m })).StatusCode);
        var opened = await admin.PostAsJsonAsync("/api/v1/admin/shifts/open", new { locationId = LocationId, openingCash = 100m });
        Assert.Equal(HttpStatusCode.Created, opened.StatusCode);
        var shiftId = (await opened.Content.ReadFromJsonAsync<JsonElement>()).GetProperty("id").GetGuid();
        Assert.Equal(HttpStatusCode.Conflict, (await admin.PostAsJsonAsync("/api/v1/admin/shifts/open", new { locationId = LocationId, openingCash = 5m })).StatusCode);

        Assert.Equal(HttpStatusCode.BadRequest, (await admin.PostAsJsonAsync($"/api/v1/admin/shifts/{shiftId}/cash", new { amount = 10m, isOut = true, reason = "" })).StatusCode);
        (await admin.PostAsJsonAsync($"/api/v1/admin/shifts/{shiftId}/cash", new { amount = 30m, isOut = false, reason = "Change float" })).EnsureSuccessStatusCode();
        (await admin.PostAsJsonAsync($"/api/v1/admin/shifts/{shiftId}/cash", new { amount = 10m, isOut = true, reason = "Milk delivery" })).EnsureSuccessStatusCode();

        // One cash order, completed inside the shift.
        Guid itemId;
        await using (var scope = factory.Services.CreateAsyncScope())
        {
            var db = scope.ServiceProvider.GetRequiredService<ApplicationDbContext>();
            var item = new MenuItem { CategoryId = (await db.MenuCategories.FirstAsync()).Id, Name = $"Shift item {Guid.NewGuid():N}"[..20], Description = "t", Price = 7m, IsAvailable = true };
            db.MenuItems.Add(item);
            await db.SaveChangesAsync();
            itemId = item.Id;
            await scope.ServiceProvider.GetRequiredService<Yurt.Application.Common.Interfaces.IMenuCacheService>().InvalidateAllMenuAsync();
        }
        var customer = factory.CreateClient();
        var (token, _) = await ApiHelpers.CreateCustomerAsync(customer, "+77002990301");
        ApiHelpers.Authorize(customer, token);
        var placed = await customer.PostAsJsonAsync("/api/v1/orders", new
        {
            locationId = LocationId, paymentMethod = "Cash",
            items = new[] { new { menuItemId = itemId, quantity = 2, toppings = Array.Empty<object>() } },
        });
        Assert.Equal(HttpStatusCode.Created, placed.StatusCode);
        var order = await placed.Content.ReadFromJsonAsync<ApiHelpers.OrderResult>(ApiHelpers.JsonOpts);
        (await admin.PostAsJsonAsync($"/api/v1/admin/orders/{order!.Id}/payment", new { paymentStatus = "Paid", paymentMethod = "Cash" }))
            .EnsureSuccessStatusCode();
        (await admin.PostAsJsonAsync($"/api/v1/admin/orders/{order.Id}/accept", new { etaMinutes = 5 })).EnsureSuccessStatusCode();
        foreach (var status in new[] { "Preparing", "Ready", "Completed" })
            (await admin.PostAsJsonAsync($"/api/v1/admin/orders/{order.Id}/status", new { status })).EnsureSuccessStatusCode();

        var x = await admin.GetFromJsonAsync<JsonElement>($"/api/v1/admin/shifts/{shiftId}/report");
        Assert.Equal(1, x.GetProperty("ordersCompleted").GetInt32());
        Assert.Equal(14m, x.GetProperty("cashSales").GetDecimal());
        Assert.Equal(134m, x.GetProperty("expectedCash").GetDecimal()); // 100 + 14 + 30 - 10
        Assert.False(x.TryGetProperty("difference", out var d) && d.ValueKind != JsonValueKind.Null); // not counted yet

        var z = await admin.PostAsJsonAsync($"/api/v1/admin/shifts/{shiftId}/close", new { countedCash = 130m, note = "short by 4" });
        z.EnsureSuccessStatusCode();
        var report = await z.Content.ReadFromJsonAsync<JsonElement>();
        Assert.Equal(-4m, report.GetProperty("difference").GetDecimal());
        Assert.Equal(2, report.GetProperty("movements").GetArrayLength());
        Assert.Equal("Cash", report.GetProperty("byMethod")[0].GetProperty("method").GetString());

        Assert.Equal(HttpStatusCode.Conflict, (await admin.PostAsJsonAsync($"/api/v1/admin/shifts/{shiftId}/close", new { countedCash = 1m })).StatusCode);
        Assert.Equal(HttpStatusCode.Conflict, (await admin.PostAsJsonAsync($"/api/v1/admin/shifts/{shiftId}/cash", new { amount = 1m, isOut = false, reason = "late" })).StatusCode);
        Assert.Null(await CurrentAsync(admin));
    }

    /// <summary>No open shift comes back as an empty body.</summary>
    private async Task<JsonElement?> CurrentAsync(HttpClient admin)
    {
        var body = await (await admin.GetAsync($"/api/v1/admin/shifts/current?locationId={LocationId}")).Content.ReadAsStringAsync();
        return string.IsNullOrWhiteSpace(body) ? null : JsonDocument.Parse(body).RootElement.Clone();
    }
}
