using System.Net.Http.Headers;
using System.Net.Http.Json;
using System.Text.Json;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Yurt.Application.Common.Interfaces;
using Yurt.Domain.Entities;
using Yurt.Infrastructure.Persistence;
using Yurt.IntegrationTests.Helpers;

namespace Yurt.IntegrationTests.Tests;

/// <summary>The restocking history on the Stock page: who changed which count and when (own_pos, Phase 6).</summary>
[Collection("Integration")]
public class StockHistoryTests(YurtWebAppFactory factory)
{
    private static readonly Guid LocationId = Guid.Parse("11111111-0000-0000-0000-000000000001");

    private async Task<Guid> NewItemAsync(string name)
    {
        await using var scope = factory.Services.CreateAsyncScope();
        var db = scope.ServiceProvider.GetRequiredService<ApplicationDbContext>();
        var category = await db.MenuCategories.FirstAsync();
        var item = new MenuItem { CategoryId = category.Id, Name = name, Description = "test", Price = 5m, IsAvailable = true };
        db.MenuItems.Add(item);
        await db.SaveChangesAsync();
        await scope.ServiceProvider.GetRequiredService<IMenuCacheService>().InvalidateAllMenuAsync();
        return item.Id;
    }

    private static async Task<List<JsonElement>> HistoryOfAsync(HttpClient admin, Guid itemId)
    {
        var all = await admin.GetFromJsonAsync<List<JsonElement>>($"/api/v1/admin/stock/history?locationId={LocationId}&take=500");
        return all!.Where(r => r.GetProperty("itemId").GetGuid() == itemId).ToList();
    }

    /// <summary>The API leaves out null fields.</summary>
    private static bool IsNull(JsonElement row, string name)
        => !row.TryGetProperty(name, out var v) || v.ValueKind == JsonValueKind.Null;

    [Fact]
    public async Task HandChanges_AreLogged_WithWhoAndHowMuch_NewestFirst()
    {
        var itemId = await NewItemAsync($"History bun {Guid.NewGuid():N}"[..24]);
        var admin = factory.CreateClient();
        admin.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer",
            await ApiHelpers.CreateAdminTokenAsync(factory.Services, admin));

        (await admin.PutAsJsonAsync($"/api/v1/admin/stock/{itemId}/{LocationId}", new { quantity = 10, lowStockAt = (int?)null })).EnsureSuccessStatusCode();
        (await admin.PutAsJsonAsync($"/api/v1/admin/stock/{itemId}/{LocationId}", new { quantity = 7, lowStockAt = (int?)null })).EnsureSuccessStatusCode();
        // Only the warn-at number changes: not a count change, so no new row.
        (await admin.PutAsJsonAsync($"/api/v1/admin/stock/{itemId}/{LocationId}", new { quantity = 7, lowStockAt = 2 })).EnsureSuccessStatusCode();
        (await admin.PutAsJsonAsync($"/api/v1/admin/stock/{itemId}/{LocationId}", new { quantity = (int?)null, lowStockAt = (int?)null })).EnsureSuccessStatusCode();

        var rows = await HistoryOfAsync(admin, itemId);
        Assert.Equal(3, rows.Count);
        Assert.All(rows, r => Assert.Equal("testadmin", r.GetProperty("changedBy").GetString()));
        Assert.All(rows, r => Assert.Equal("dish", r.GetProperty("kind").GetString()));

        // Newest first: stopped counting, then 10 -> 7, then first count.
        Assert.Equal(7m, rows[0].GetProperty("oldQuantity").GetDecimal());
        Assert.True(IsNull(rows[0], "newQuantity"));
        Assert.Equal(-3m, rows[1].GetProperty("change").GetDecimal());
        Assert.True(IsNull(rows[2], "oldQuantity"));
        Assert.Equal(10m, rows[2].GetProperty("newQuantity").GetDecimal());
    }

    [Fact]
    public async Task IngredientRestock_IsLogged_AndKeepsItsNameAfterDelete()
    {
        var admin = factory.CreateClient();
        admin.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer",
            await ApiHelpers.CreateAdminTokenAsync(factory.Services, admin));
        var name = $"Beans {Guid.NewGuid():N}"[..20];
        var created = await admin.PostAsJsonAsync("/api/v1/admin/ingredients", new { name, unit = "g" });
        var beans = (await created.Content.ReadFromJsonAsync<JsonElement>()).GetProperty("id").GetGuid();

        (await admin.PutAsJsonAsync($"/api/v1/admin/ingredients/{beans}/stock/{LocationId}", new { quantity = 250.5m, lowStockAt = (decimal?)null })).EnsureSuccessStatusCode();
        (await admin.PutAsJsonAsync($"/api/v1/admin/ingredients/{beans}/stock/{LocationId}", new { quantity = 1250.5m, lowStockAt = (decimal?)null })).EnsureSuccessStatusCode();
        (await admin.DeleteAsync($"/api/v1/admin/ingredients/{beans}")).EnsureSuccessStatusCode();

        var rows = await HistoryOfAsync(admin, beans);
        Assert.Equal(2, rows.Count);
        Assert.Equal("ingredient", rows[0].GetProperty("kind").GetString());
        Assert.Equal(name, rows[0].GetProperty("itemName").GetString());
        Assert.Equal("g", rows[0].GetProperty("unit").GetString());
        Assert.Equal(1000m, rows[0].GetProperty("change").GetDecimal());
    }
}
