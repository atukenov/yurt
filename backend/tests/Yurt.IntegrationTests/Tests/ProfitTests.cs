using System.Net;
using System.Net.Http.Json;
using System.Text.Json;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Yurt.Application.Common.Interfaces;
using Yurt.Domain.Entities;
using Yurt.Domain.Enums;
using Yurt.IntegrationTests.Helpers;

namespace Yurt.IntegrationTests.Tests;

/// <summary>
/// Profit reports: the ingredient cost is saved on each sale when the order is accepted, older sales are priced at today's
/// costs, the month's profit takes off write-offs, wages and the costs the CEO types in, and only the CEO sees any of it.
/// </summary>
[Collection("Integration")]
public class ProfitTests(YurtWebAppFactory factory)
{
    private async Task<HttpClient> ClientAsync(string username, string password, AdminRole role)
    {
        var client = factory.CreateClient();
        ApiHelpers.Authorize(client, await ApiHelpers.CreateAdminTokenAsync(factory.Services, client, username, password, role: role));
        return client;
    }

    private async Task<Guid> NewLocationAsync()
    {
        await using var scope = factory.Services.CreateAsyncScope();
        var db = scope.ServiceProvider.GetRequiredService<IApplicationDbContext>();
        var location = new Location
        {
            Name = $"Profit branch {Guid.NewGuid():N}"[..24], Address = "x", WorkingHours = "", ContactPhone = "", IsActive = true,
        };
        db.Locations.Add(location);
        await db.SaveChangesAsync();
        return location.Id;
    }

    private async Task<Guid> NewItemAsync(string name, decimal price)
    {
        await using var scope = factory.Services.CreateAsyncScope();
        var db = scope.ServiceProvider.GetRequiredService<IApplicationDbContext>();
        var category = await db.MenuCategories.FirstAsync();
        var item = new MenuItem { CategoryId = category.Id, Name = name, Description = "test", Price = price, IsAvailable = true };
        db.MenuItems.Add(item);
        await db.SaveChangesAsync();
        await scope.ServiceProvider.GetRequiredService<IMenuCacheService>().InvalidateAllMenuAsync();
        return item.Id;
    }

    /// <summary>A month far away so other tests' orders never land in it.</summary>
    private static (string Month, DateTime Inside) FarMonth()
    {
        var year = 2200 + Random.Shared.Next(0, 700);
        var month = Random.Shared.Next(2, 13);
        return ($"{year}-{month:00}", new DateTime(year, month, 15, 6, 0, 0, DateTimeKind.Utc));
    }

    [Fact]
    public async Task AcceptedOrder_KeepsItsCost_AndTheReportsAddItUp()
    {
        var id = Guid.NewGuid().ToString("N")[..8];
        var ceo = await ClientAsync($"pceo-{id}", "Ceo@123456", AdminRole.Ceo);
        var location = await NewLocationAsync();
        var latteName = $"Profit latte {id}";
        var latte = await NewItemAsync(latteName, 1000m);

        var created = await ceo.PostAsJsonAsync("/api/v1/admin/ingredients", new { name = $"Beans {id}", unit = "g" });
        var beans = (await created.Content.ReadFromJsonAsync<JsonElement>()).GetProperty("id").GetGuid();
        (await ceo.PutAsJsonAsync($"/api/v1/admin/ingredients/{beans}/cost", new { costPerUnit = 10m })).EnsureSuccessStatusCode();
        (await ceo.PutAsJsonAsync($"/api/v1/admin/ingredients/recipes/{latte}",
            new { lines = new[] { new { ingredientId = beans, variantId = (Guid?)null, quantity = 18m } } })).EnsureSuccessStatusCode();

        // A guest orders 2 lattes; accepting the order saves 18 g × 10 = 180 per latte.
        var guest = factory.CreateClient();
        var (customerToken, _) = await ApiHelpers.CreateCustomerAsync(guest, $"+7701{Random.Shared.Next(1000000, 9999999)}");
        ApiHelpers.Authorize(guest, customerToken);
        var placed = await guest.PostAsJsonAsync("/api/v1/orders", new
        {
            locationId = location, paymentMethod = "Cash", items = new[] { new { menuItemId = latte, quantity = 2 } },
        });
        placed.EnsureSuccessStatusCode();
        var orderId = (await placed.Content.ReadFromJsonAsync<ApiHelpers.OrderResult>(ApiHelpers.JsonOpts))!.Id;
        (await ceo.PostAsJsonAsync($"/api/v1/admin/orders/{orderId}/payment", new { paymentStatus = "Paid", paymentMethod = "Cash" }))
            .EnsureSuccessStatusCode();
        (await ceo.PostAsJsonAsync($"/api/v1/admin/orders/{orderId}/accept", new { etaMinutes = 5 })).EnsureSuccessStatusCode();

        // Beans get dearer afterwards: the accepted order keeps its cost.
        (await ceo.PutAsJsonAsync($"/api/v1/admin/ingredients/{beans}/cost", new { costPerUnit = 20m })).EnsureSuccessStatusCode();

        var (month, inside) = FarMonth();
        await using (var scope = factory.Services.CreateAsyncScope())
        {
            var db = scope.ServiceProvider.GetRequiredService<IApplicationDbContext>();
            var order = await db.Orders.Include(o => o.Items).FirstAsync(o => o.Id == orderId);
            Assert.Equal(180m, order.Items.Single().UnitCost);
            order.Status = OrderStatus.Completed;
            order.CompletedAt = inside;

            // An older sale from before costs were saved (no UnitCost), with a 50 % discount: priced at today's 360.
            var old = new Order
            {
                CustomerUserId = order.CustomerUserId, LocationId = location, Status = OrderStatus.Completed,
                PaymentStatus = PaymentStatus.Paid, Subtotal = 1000m, DiscountAmount = 500m, Total = 500m,
                CreatedAt = inside, CompletedAt = inside,
            };
            db.Orders.Add(old);
            db.OrderItems.Add(new OrderItem { Order = old, MenuItemId = latte, MenuItemName = latteName, Quantity = 1, UnitPrice = 1000m, LineTotal = 1000m });
            await db.SaveChangesAsync();
        }

        var items = await ceo.GetFromJsonAsync<JsonElement>($"/api/v1/admin/profit/items?month={month}&locationId={location}");
        var row = items.GetProperty("items").EnumerateArray().Single(i => i.GetProperty("menuItemId").GetGuid() == latte);
        Assert.Equal(3, row.GetProperty("sold").GetInt32());
        Assert.Equal(2500m, row.GetProperty("revenue").GetDecimal());          // 2000 + 1000 at half price
        Assert.Equal(720m, row.GetProperty("cost").GetDecimal());             // 2 × 180 saved + 1 × 360 today
        Assert.Equal(1780m, row.GetProperty("profit").GetDecimal());
        Assert.Equal(1, row.GetProperty("estimatedCount").GetInt32());
        Assert.Equal(28.8m, row.GetProperty("foodCostPercent").GetDecimal());

        // Rent for the whole cafe and utilities for this branch.
        (await ceo.PostAsJsonAsync("/api/v1/admin/profit/expenses",
            new { month, locationId = (Guid?)null, category = "rent", name = "Rent", amount = 300m })).EnsureSuccessStatusCode();
        (await ceo.PostAsJsonAsync("/api/v1/admin/profit/expenses",
            new { month, locationId = location, category = "utilities", name = "Power", amount = 100m })).EnsureSuccessStatusCode();
        Assert.Equal(HttpStatusCode.UnprocessableEntity, (await ceo.PostAsJsonAsync("/api/v1/admin/profit/expenses",
            new { month, locationId = (Guid?)null, category = "yacht", name = "Boat", amount = 1m })).StatusCode);

        var branch = await ceo.GetFromJsonAsync<JsonElement>($"/api/v1/admin/profit/month?month={month}&locationId={location}");
        Assert.Equal(2, branch.GetProperty("orders").GetInt32());
        Assert.Equal(3000m, branch.GetProperty("sales").GetDecimal());
        Assert.Equal(500m, branch.GetProperty("discounts").GetDecimal());
        Assert.Equal(2500m, branch.GetProperty("netSales").GetDecimal());
        Assert.Equal(720m, branch.GetProperty("ingredientCost").GetDecimal());
        Assert.Equal(1, branch.GetProperty("estimatedLines").GetInt32());
        Assert.False(branch.TryGetProperty("payroll", out var branchPay) && branchPay.ValueKind == JsonValueKind.Number); // wages are not split by branch
        Assert.Equal(100m, branch.GetProperty("expensesTotal").GetDecimal());        // whole-cafe rent left out
        Assert.Equal(2500m - 720m - 100m, branch.GetProperty("profit").GetDecimal());
        Assert.Equal(0m, branch.GetProperty("previous").GetProperty("netSales").GetDecimal());

        var all = await ceo.GetFromJsonAsync<JsonElement>($"/api/v1/admin/profit/month?month={month}");
        Assert.Equal(400m, all.GetProperty("expensesTotal").GetDecimal());
        var wages = all.TryGetProperty("payroll", out var pay) && pay.ValueKind == JsonValueKind.Number ? pay.GetDecimal() : 0m;
        Assert.Equal(
            all.GetProperty("grossProfit").GetDecimal() - all.GetProperty("writeOffs").GetDecimal()
                - all.GetProperty("stocktakeLoss").GetDecimal() - wages - 400m,
            all.GetProperty("profit").GetDecimal());

        // The Excel files download.
        var xlsx = await ceo.GetAsync($"/api/v1/admin/profit/month/export?month={month}&lang=ru");
        xlsx.EnsureSuccessStatusCode();
        Assert.Equal("application/vnd.openxmlformats-officedocument.spreadsheetml.sheet", xlsx.Content.Headers.ContentType?.MediaType);
        (await ceo.GetAsync($"/api/v1/admin/profit/items/export?month={month}&lang=kk")).EnsureSuccessStatusCode();

        // Next month starts empty and can copy this month's costs once.
        var next = DateTime.ParseExact(month + "-01", "yyyy-MM-dd", null).AddMonths(1).ToString("yyyy-MM");
        var copied = await ceo.PostAsJsonAsync("/api/v1/admin/profit/expenses/copy-previous", new { month = next });
        copied.EnsureSuccessStatusCode();
        Assert.Equal(2, (await copied.Content.ReadFromJsonAsync<List<JsonElement>>())!.Count);
        Assert.Equal(HttpStatusCode.Conflict,
            (await ceo.PostAsJsonAsync("/api/v1/admin/profit/expenses/copy-previous", new { month = next })).StatusCode);
    }

    [Fact]
    public async Task OnlyTheCeo_SeesProfitAndFoodCost()
    {
        var id = Guid.NewGuid().ToString("N")[..8];
        var admin = await ClientAsync($"padm-{id}", "Admin@12345", AdminRole.Admin);
        var worker = await ClientAsync($"pwrk-{id}", "Worker@12345", AdminRole.Worker);
        var op = await ClientAsync($"pop-{id}", "OperatorPass@12345", AdminRole.SuperAdmin);
        var ceo = await ClientAsync($"pceo2-{id}", "Ceo@123456", AdminRole.Ceo);
        var (month, _) = FarMonth();

        string[] moneyUrls =
        [
            $"/api/v1/admin/profit/items?month={month}", $"/api/v1/admin/profit/month?month={month}",
            $"/api/v1/admin/profit/menu-analysis?month={month}",
            $"/api/v1/admin/profit/expenses?month={month}", "/api/v1/admin/stock/food-cost", "/api/v1/admin/stock/write-offs",
        ];
        foreach (var url in moneyUrls)
        {
            Assert.Equal(HttpStatusCode.Forbidden, (await admin.GetAsync(url)).StatusCode);
            Assert.Equal(HttpStatusCode.Forbidden, (await worker.GetAsync(url)).StatusCode);
            Assert.Equal(HttpStatusCode.OK, (await ceo.GetAsync(url)).StatusCode);
        }

        // The month's profit includes wages, so like payroll the platform operator does not see it.
        Assert.Equal(HttpStatusCode.Forbidden, (await op.GetAsync($"/api/v1/admin/profit/month?month={month}")).StatusCode);
        Assert.Equal(HttpStatusCode.UnprocessableEntity, (await ceo.GetAsync("/api/v1/admin/profit/month?month=2026-13")).StatusCode);
    }
}
