using System.Net;
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

/// <summary>Ingredients, recipes and the automatic stop-list they drive (own_pos, Phase 6).</summary>
[Collection("Integration")]
public class IngredientTests(YurtWebAppFactory factory)
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
    public async Task Recipe_LimitsOrders_AcceptUsesIngredients_AndRunningOutHidesTheItem()
    {
        var itemId = await NewItemAsync($"Latte test {Guid.NewGuid():N}"[..24]);
        var admin = factory.CreateClient();
        admin.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer",
            await ApiHelpers.CreateAdminTokenAsync(factory.Services, admin));
        var customer = factory.CreateClient();
        var (token, _) = await ApiHelpers.CreateCustomerAsync(customer, "+77002990201");
        ApiHelpers.Authorize(customer, token);

        var created = await admin.PostAsJsonAsync("/api/v1/admin/ingredients", new { name = $"Milk {Guid.NewGuid():N}"[..20], unit = "ml" });
        Assert.Equal(HttpStatusCode.Created, created.StatusCode);
        var milk = (await created.Content.ReadFromJsonAsync<JsonElement>()).GetProperty("id").GetGuid();

        (await admin.PutAsJsonAsync($"/api/v1/admin/ingredients/recipes/{itemId}",
            new { lines = new[] { new { ingredientId = milk, variantId = (Guid?)null, quantity = 200m } } }))
            .EnsureSuccessStatusCode();
        (await admin.PutAsJsonAsync($"/api/v1/admin/ingredients/{milk}/stock/{LocationId}", new { quantity = 500m, lowStockAt = 200m }))
            .EnsureSuccessStatusCode();

        Assert.True(await OnMenuAsync(customer, itemId));
        var tooMany = await OrderAsync(customer, itemId, 3); // needs 600 ml, 500 left
        Assert.Equal(HttpStatusCode.BadRequest, tooMany.StatusCode);
        Assert.Contains("not available in that amount", await tooMany.Content.ReadAsStringAsync());

        var placed = await OrderAsync(customer, itemId, 2);
        Assert.Equal(HttpStatusCode.Created, placed.StatusCode);
        var order = await placed.Content.ReadFromJsonAsync<ApiHelpers.OrderResult>(ApiHelpers.JsonOpts);
        (await admin.PostAsJsonAsync($"/api/v1/admin/orders/{order!.Id}/payment", new { paymentStatus = "Paid", paymentMethod = "Cash" }))
            .EnsureSuccessStatusCode();
        (await admin.PostAsJsonAsync($"/api/v1/admin/orders/{order.Id}/accept", new { etaMinutes = 5 })).EnsureSuccessStatusCode();

        var list = await admin.GetFromJsonAsync<List<JsonElement>>($"/api/v1/admin/ingredients?locationId={LocationId}");
        var row = list!.Single(r => r.GetProperty("id").GetGuid() == milk);
        Assert.Equal(100m, row.GetProperty("quantity").GetDecimal());
        Assert.True(row.GetProperty("isLow").GetBoolean());
        Assert.Equal(1, row.GetProperty("usedInItems").GetInt32());

        Assert.False(await OnMenuAsync(customer, itemId)); // 100 ml left, one portion needs 200
        (await admin.PutAsJsonAsync($"/api/v1/admin/ingredients/{milk}/stock/{LocationId}", new { quantity = 1000m, lowStockAt = (decimal?)null }))
            .EnsureSuccessStatusCode();
        Assert.True(await OnMenuAsync(customer, itemId));

        // Not counting the ingredient any more makes the item unlimited again.
        (await admin.PutAsJsonAsync($"/api/v1/admin/ingredients/{milk}/stock/{LocationId}", new { quantity = (decimal?)null, lowStockAt = (decimal?)null }))
            .EnsureSuccessStatusCode();
        Assert.Equal(HttpStatusCode.Created, (await OrderAsync(customer, itemId, 50)).StatusCode);
    }

    [Fact]
    public async Task Recipes_AreValidated_AndDeletingAnIngredientRemovesItsLines()
    {
        var itemId = await NewItemAsync($"Recipe check {Guid.NewGuid():N}"[..24]);
        var otherItem = await NewItemAsync($"Other item {Guid.NewGuid():N}"[..24]);
        var admin = factory.CreateClient();
        admin.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer",
            await ApiHelpers.CreateAdminTokenAsync(factory.Services, admin));
        var variantOfOther = await ApiHelpers.AddVariantAsync(factory.Services, otherItem, "Large", 6m);

        var name = $"Beans {Guid.NewGuid():N}"[..20];
        var beans = (await (await admin.PostAsJsonAsync("/api/v1/admin/ingredients", new { name, unit = "g" }))
            .Content.ReadFromJsonAsync<JsonElement>()).GetProperty("id").GetGuid();
        Assert.Equal(HttpStatusCode.Conflict, (await admin.PostAsJsonAsync("/api/v1/admin/ingredients", new { name, unit = "g" })).StatusCode);

        Task<HttpResponseMessage> Save(object lines) => admin.PutAsJsonAsync($"/api/v1/admin/ingredients/recipes/{itemId}", new { lines });
        Assert.Equal(HttpStatusCode.BadRequest, (await Save(new[] { new { ingredientId = beans, variantId = (Guid?)null, quantity = 0m } })).StatusCode);
        Assert.Equal(HttpStatusCode.BadRequest, (await Save(new[] { new { ingredientId = Guid.NewGuid(), variantId = (Guid?)null, quantity = 5m } })).StatusCode);
        Assert.Equal(HttpStatusCode.BadRequest, (await Save(new[] { new { ingredientId = beans, variantId = (Guid?)variantOfOther, quantity = 5m } })).StatusCode);

        Assert.Equal(HttpStatusCode.OK, (await Save(new[] { new { ingredientId = beans, variantId = (Guid?)null, quantity = 18m } })).StatusCode);
        var recipe = await admin.GetFromJsonAsync<List<JsonElement>>($"/api/v1/admin/ingredients/recipes/{itemId}");
        Assert.Single(recipe!);

        Assert.Equal(HttpStatusCode.OK, (await admin.DeleteAsync($"/api/v1/admin/ingredients/{beans}")).StatusCode);
        Assert.Empty((await admin.GetFromJsonAsync<List<JsonElement>>($"/api/v1/admin/ingredients/recipes/{itemId}"))!);
    }
}
