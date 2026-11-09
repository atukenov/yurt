using System.Net;
using System.Net.Http.Headers;
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
/// The stock pack (own_pos): deliveries with cost, food cost per dish, write-offs with a reason, moves between
/// branches by staff, stocktakes and the warning before an order takes stock below zero.
/// </summary>
[Collection("Integration")]
public class StockPackTests(YurtWebAppFactory factory)
{
    private static readonly Guid Downtown = Guid.Parse("11111111-0000-0000-0000-000000000001");
    private static readonly Guid Midtown = Guid.Parse("11111111-0000-0000-0000-000000000002");

    private async Task<HttpClient> AdminAsync()
    {
        var admin = factory.CreateClient();
        admin.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer",
            await ApiHelpers.CreateAdminTokenAsync(factory.Services, admin));
        return admin;
    }

    private async Task<HttpClient> WorkerAsync()
    {
        var worker = factory.CreateClient();
        worker.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer",
            await ApiHelpers.CreateAdminTokenAsync(factory.Services, worker, "stockbarista", "Barista@123", role: AdminRole.Worker));
        return worker;
    }

    private async Task<Guid> NewItemAsync(string name, decimal price)
    {
        await using var scope = factory.Services.CreateAsyncScope();
        var db = scope.ServiceProvider.GetRequiredService<ApplicationDbContext>();
        var category = await db.MenuCategories.FirstAsync();
        var item = new MenuItem { CategoryId = category.Id, Name = name, Description = "test", Price = price, IsAvailable = true };
        db.MenuItems.Add(item);
        await db.SaveChangesAsync();
        await scope.ServiceProvider.GetRequiredService<IMenuCacheService>().InvalidateAllMenuAsync();
        return item.Id;
    }

    private static async Task<Guid> NewIngredientAsync(HttpClient admin, string prefix, string unit)
    {
        var created = await admin.PostAsJsonAsync("/api/v1/admin/ingredients", new { name = $"{prefix} {Guid.NewGuid():N}"[..20], unit });
        return (await created.Content.ReadFromJsonAsync<JsonElement>()).GetProperty("id").GetGuid();
    }

    private static async Task<decimal?> LeftAsync(HttpClient client, Guid ingredientId, Guid locationId)
    {
        var list = await client.GetFromJsonAsync<List<JsonElement>>($"/api/v1/admin/ingredients?locationId={locationId}");
        var row = list!.Single(r => r.GetProperty("id").GetGuid() == ingredientId);
        return row.TryGetProperty("quantity", out var q) && q.ValueKind == JsonValueKind.Number ? q.GetDecimal() : null;
    }

    private static async Task<List<JsonElement>> HistoryAsync(HttpClient client, Guid locationId, Guid itemId)
    {
        var all = await client.GetFromJsonAsync<List<JsonElement>>($"/api/v1/admin/stock/history?locationId={locationId}&take=500");
        return all!.Where(r => r.GetProperty("itemId").GetGuid() == itemId).ToList();
    }

    [Fact]
    public async Task Restock_WithCost_KeepsARunningAverage_AndFoodCostUsesIt()
    {
        var admin = await AdminAsync();
        var beans = await NewIngredientAsync(admin, "Beans", "g");
        var latte = await NewItemAsync($"Cost latte {Guid.NewGuid():N}"[..24], 1000m);
        (await admin.PutAsJsonAsync($"/api/v1/admin/ingredients/recipes/{latte}",
            new { lines = new[] { new { ingredientId = beans, variantId = (Guid?)null, quantity = 18m } } })).EnsureSuccessStatusCode();

        // 1000 g for 8000 = 8 per g; then 1000 g for 12000 on top of 1000 g at 8 = 10 per g on average.
        var first = await admin.PostAsJsonAsync("/api/v1/admin/stock/restock",
            new { locationId = Downtown, ingredientId = beans, quantity = 1000m, totalCost = 8000m, note = "Invoice 12" });
        first.EnsureSuccessStatusCode();
        Assert.Equal(8m, (await first.Content.ReadFromJsonAsync<JsonElement>()).GetProperty("costPerUnit").GetDecimal());
        var second = await admin.PostAsJsonAsync("/api/v1/admin/stock/restock",
            new { locationId = Downtown, ingredientId = beans, quantity = 1000m, totalCost = 12000m });
        var body = await second.Content.ReadFromJsonAsync<JsonElement>();
        Assert.Equal(10m, body.GetProperty("costPerUnit").GetDecimal());
        Assert.Equal(2000m, body.GetProperty("left").GetDecimal());

        var history = await HistoryAsync(admin, Downtown, beans);
        Assert.Equal(2, history.Count);
        Assert.All(history, r => Assert.Equal("restock", r.GetProperty("type").GetString()));
        Assert.Equal(12000m, history[0].GetProperty("costValue").GetDecimal());
        Assert.Equal("Invoice 12", history[1].GetProperty("note").GetString());

        var food = await admin.GetFromJsonAsync<List<JsonElement>>("/api/v1/admin/stock/food-cost");
        var row = food!.Single(f => f.GetProperty("menuItemId").GetGuid() == latte);
        Assert.Equal(180m, row.GetProperty("cost").GetDecimal());          // 18 g x 10
        Assert.Equal(18m, row.GetProperty("foodCostPercent").GetDecimal()); // 180 of 1000
        Assert.Equal(820m, row.GetProperty("margin").GetDecimal());
        Assert.Empty(row.GetProperty("missingCost").EnumerateArray());

        // The cost can also be set by hand, and invalid restocks are refused.
        (await admin.PutAsJsonAsync($"/api/v1/admin/ingredients/{beans}/cost", new { costPerUnit = 9.5m })).EnsureSuccessStatusCode();
        Assert.Equal(HttpStatusCode.BadRequest, (await admin.PostAsJsonAsync("/api/v1/admin/stock/restock",
            new { locationId = Downtown, ingredientId = beans, quantity = 0m, totalCost = 1m })).StatusCode);
        Assert.Equal(HttpStatusCode.BadRequest, (await admin.PostAsJsonAsync("/api/v1/admin/stock/restock",
            new { locationId = Downtown, ingredientId = beans, quantity = 5m, totalCost = -1m })).StatusCode);
    }

    [Fact]
    public async Task WriteOff_NeedsAReason_NeverGoesBelowZero_AndIsTotalledByReason()
    {
        var admin = await AdminAsync();
        var worker = await WorkerAsync();
        var milk = await NewIngredientAsync(admin, "Milk", "ml");
        (await admin.PutAsJsonAsync($"/api/v1/admin/ingredients/{milk}/cost", new { costPerUnit = 0.5m })).EnsureSuccessStatusCode();
        (await admin.PutAsJsonAsync($"/api/v1/admin/ingredients/{milk}/stock/{Downtown}", new { quantity = 1000m, lowStockAt = (decimal?)null }))
            .EnsureSuccessStatusCode();

        object WriteOff(decimal quantity, string reason) => new { locationId = Downtown, kind = "ingredient", itemId = milk, quantity, reason, note = "" };
        Assert.Equal(HttpStatusCode.BadRequest, (await worker.PostAsJsonAsync("/api/v1/admin/stock/write-off", WriteOff(100m, "because"))).StatusCode);
        var tooMuch = await worker.PostAsJsonAsync("/api/v1/admin/stock/write-off", WriteOff(1500m, "spilled"));
        Assert.Equal(HttpStatusCode.Conflict, tooMuch.StatusCode);
        Assert.Contains("Only 1000 ml", await tooMuch.Content.ReadAsStringAsync());
        Assert.Equal(1000m, await LeftAsync(admin, milk, Downtown));

        // A barista (not an admin) can write off.
        (await worker.PostAsJsonAsync("/api/v1/admin/stock/write-off", WriteOff(200m, "spilled"))).EnsureSuccessStatusCode();
        (await worker.PostAsJsonAsync("/api/v1/admin/stock/write-off", WriteOff(300m, "staff"))).EnsureSuccessStatusCode();
        Assert.Equal(500m, await LeftAsync(admin, milk, Downtown));

        var history = await HistoryAsync(admin, Downtown, milk);
        var spilled = history.Single(r => r.TryGetProperty("reason", out var why) && why.GetString() == "spilled");
        Assert.Equal("writeoff", spilled.GetProperty("type").GetString());
        Assert.Equal("stockbarista", spilled.GetProperty("changedBy").GetString());
        Assert.Equal(-200m, spilled.GetProperty("change").GetDecimal());
        Assert.Equal(100m, spilled.GetProperty("costValue").GetDecimal());

        var summary = await admin.GetFromJsonAsync<JsonElement>($"/api/v1/admin/stock/write-offs?locationId={Downtown}");
        var reasons = summary.GetProperty("byReason").EnumerateArray().ToDictionary(r => r.GetProperty("reason").GetString()!);
        Assert.True(reasons["staff"].GetProperty("cost").GetDecimal() >= 150m);
        Assert.True(reasons["spilled"].GetProperty("cost").GetDecimal() >= 100m);

        // Money figures are for cafe admins only.
        Assert.Equal(HttpStatusCode.Forbidden, (await worker.GetAsync($"/api/v1/admin/stock/write-offs?locationId={Downtown}")).StatusCode);
        Assert.Equal(HttpStatusCode.Forbidden, (await worker.GetAsync("/api/v1/admin/stock/food-cost")).StatusCode);
    }

    [Fact]
    public async Task Barista_MovesStockBetweenBranches_BothSidesLogged()
    {
        var admin = await AdminAsync();
        var worker = await WorkerAsync();
        var syrup = await NewIngredientAsync(admin, "Syrup", "ml");
        (await admin.PutAsJsonAsync($"/api/v1/admin/ingredients/{syrup}/stock/{Downtown}", new { quantity = 700m, lowStockAt = (decimal?)null }))
            .EnsureSuccessStatusCode();

        object Move(Guid from, Guid to, decimal quantity) => new { fromLocationId = from, toLocationId = to, kind = "ingredient", itemId = syrup, quantity, note = "for the weekend" };
        Assert.Equal(HttpStatusCode.BadRequest, (await worker.PostAsJsonAsync("/api/v1/admin/stock/transfer", Move(Downtown, Downtown, 10m))).StatusCode);
        Assert.Equal(HttpStatusCode.Conflict, (await worker.PostAsJsonAsync("/api/v1/admin/stock/transfer", Move(Downtown, Midtown, 701m))).StatusCode);
        // Midtown does not count syrup, so it has nothing to send.
        Assert.Equal(HttpStatusCode.BadRequest, (await worker.PostAsJsonAsync("/api/v1/admin/stock/transfer", Move(Midtown, Downtown, 1m))).StatusCode);

        var moved = await worker.PostAsJsonAsync("/api/v1/admin/stock/transfer", Move(Downtown, Midtown, 250m));
        moved.EnsureSuccessStatusCode();
        Assert.Equal(450m, (await moved.Content.ReadFromJsonAsync<JsonElement>()).GetProperty("left").GetDecimal());
        Assert.Equal(450m, await LeftAsync(admin, syrup, Downtown));
        Assert.Equal(250m, await LeftAsync(admin, syrup, Midtown)); // started counting there

        var sent = (await HistoryAsync(admin, Downtown, syrup)).Single(r => r.GetProperty("type").GetString() == "transfer_out");
        Assert.Equal("Yurt Midtown", sent.GetProperty("otherLocationName").GetString());
        Assert.Equal(-250m, sent.GetProperty("change").GetDecimal());
        var received = (await HistoryAsync(admin, Midtown, syrup)).Single();
        Assert.Equal("transfer_in", received.GetProperty("type").GetString());
        Assert.Equal("Yurt Downtown", received.GetProperty("otherLocationName").GetString());
        Assert.Equal(250m, received.GetProperty("newQuantity").GetDecimal());
        Assert.Equal("stockbarista", received.GetProperty("changedBy").GetString());
    }

    [Fact]
    public async Task Stocktake_ShowsDifferenceFromExpected_AndItsCost()
    {
        var admin = await AdminAsync();
        var cups = await NewIngredientAsync(admin, "Cups", "pcs");
        var lids = await NewIngredientAsync(admin, "Lids", "pcs");
        var bun = await NewItemAsync($"Count bun {Guid.NewGuid():N}"[..24], 500m);
        (await admin.PutAsJsonAsync($"/api/v1/admin/ingredients/{cups}/cost", new { costPerUnit = 40m })).EnsureSuccessStatusCode();
        (await admin.PutAsJsonAsync($"/api/v1/admin/ingredients/{cups}/stock/{Downtown}", new { quantity = 100m, lowStockAt = (decimal?)null })).EnsureSuccessStatusCode();
        (await admin.PutAsJsonAsync($"/api/v1/admin/ingredients/{lids}/stock/{Downtown}", new { quantity = 50m, lowStockAt = (decimal?)null })).EnsureSuccessStatusCode();
        (await admin.PutAsJsonAsync($"/api/v1/admin/stock/{bun}/{Downtown}", new { quantity = 10, lowStockAt = (int?)null })).EnsureSuccessStatusCode();

        var notCounted = await NewIngredientAsync(admin, "Straws", "pcs");
        Assert.Equal(HttpStatusCode.BadRequest, (await admin.PostAsJsonAsync("/api/v1/admin/stock/stocktake",
            new { locationId = Downtown, lines = new[] { new { kind = "ingredient", itemId = notCounted, counted = 5m } } })).StatusCode);
        Assert.Equal(HttpStatusCode.BadRequest, (await admin.PostAsJsonAsync("/api/v1/admin/stock/stocktake",
            new { locationId = Downtown, lines = new[] { new { kind = "dish", itemId = bun, counted = 2.5m } } })).StatusCode);

        var resp = await admin.PostAsJsonAsync("/api/v1/admin/stock/stocktake", new
        {
            locationId = Downtown, note = "Monday count",
            lines = new object[]
            {
                new { kind = "ingredient", itemId = cups, counted = 92m },
                new { kind = "ingredient", itemId = lids, counted = 50m },
                new { kind = "dish", itemId = bun, counted = 11m },
            },
        });
        resp.EnsureSuccessStatusCode();
        var result = await resp.Content.ReadFromJsonAsync<JsonElement>();
        var lines = result.GetProperty("lines").EnumerateArray().ToDictionary(l => l.GetProperty("itemId").GetGuid());
        Assert.Equal(100m, lines[cups].GetProperty("expected").GetDecimal());
        Assert.Equal(-8m, lines[cups].GetProperty("difference").GetDecimal());
        Assert.Equal(-320m, lines[cups].GetProperty("costDifference").GetDecimal());
        Assert.Equal(0m, lines[lids].GetProperty("difference").GetDecimal());
        Assert.Equal(1m, lines[bun].GetProperty("difference").GetDecimal());
        Assert.Equal(320m, result.GetProperty("missingCost").GetDecimal());

        Assert.Equal(92m, await LeftAsync(admin, cups, Downtown));
        var logged = (await HistoryAsync(admin, Downtown, cups)).First();
        Assert.Equal("stocktake", logged.GetProperty("type").GetString());
        Assert.Equal("Monday count", logged.GetProperty("note").GetString());
        Assert.Empty(await HistoryAsync(admin, Downtown, lids).ContinueWith(t => t.Result.Where(r => r.GetProperty("type").GetString() == "stocktake").ToList()));
    }

    [Fact]
    public async Task OrderCheck_WarnsBeforeStockGoesBelowZero_AndAcceptingAnywayLogsTheShortfall()
    {
        var admin = await AdminAsync();
        var milk = await NewIngredientAsync(admin, "Oat", "ml");
        var latte = await NewItemAsync($"Short latte {Guid.NewGuid():N}"[..24], 900m);
        (await admin.PutAsJsonAsync($"/api/v1/admin/ingredients/recipes/{latte}",
            new { lines = new[] { new { ingredientId = milk, variantId = (Guid?)null, quantity = 200m } } })).EnsureSuccessStatusCode();
        (await admin.PutAsJsonAsync($"/api/v1/admin/ingredients/{milk}/stock/{Downtown}", new { quantity = 500m, lowStockAt = (decimal?)null }))
            .EnsureSuccessStatusCode();

        var customer = factory.CreateClient();
        var (token, _) = await ApiHelpers.CreateCustomerAsync(customer, "+77002990311");
        ApiHelpers.Authorize(customer, token);
        var placed = await customer.PostAsJsonAsync("/api/v1/orders", new
        {
            locationId = Downtown, paymentMethod = "Cash",
            items = new[] { new { menuItemId = latte, quantity = 2, toppings = Array.Empty<object>() } },
        });
        Assert.Equal(HttpStatusCode.Created, placed.StatusCode);
        var order = await placed.Content.ReadFromJsonAsync<ApiHelpers.OrderResult>(ApiHelpers.JsonOpts);

        Assert.Empty((await admin.GetFromJsonAsync<List<JsonElement>>($"/api/v1/admin/stock/order-check/{order!.Id}"))!);

        // Someone spills milk while the order waits: now 2 x 200 ml is more than the 300 ml left.
        (await admin.PostAsJsonAsync("/api/v1/admin/stock/write-off",
            new { locationId = Downtown, kind = "ingredient", itemId = milk, quantity = 200m, reason = "spilled" })).EnsureSuccessStatusCode();
        var shorts = await admin.GetFromJsonAsync<List<JsonElement>>($"/api/v1/admin/stock/order-check/{order.Id}");
        var shortRow = Assert.Single(shorts!);
        Assert.Equal(400m, shortRow.GetProperty("need").GetDecimal());
        Assert.Equal(300m, shortRow.GetProperty("left").GetDecimal());

        (await admin.PostAsJsonAsync($"/api/v1/admin/orders/{order.Id}/payment", new { paymentStatus = "Paid", paymentMethod = "Cash" }))
            .EnsureSuccessStatusCode();
        (await admin.PostAsJsonAsync($"/api/v1/admin/orders/{order.Id}/accept", new { etaMinutes = 5 })).EnsureSuccessStatusCode();

        Assert.Equal(0m, await LeftAsync(admin, milk, Downtown));
        var shortfall = (await HistoryAsync(admin, Downtown, milk)).Single(r => r.GetProperty("type").GetString() == "shortfall");
        Assert.Equal(300m, shortfall.GetProperty("oldQuantity").GetDecimal());
        Assert.Contains("400 needed", shortfall.GetProperty("note").GetString());
    }
}
