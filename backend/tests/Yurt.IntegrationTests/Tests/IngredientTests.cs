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

    [Fact]
    public async Task ToppingUse_LimitsOrders_AcceptLowersIngredient_AndRunningOutHidesTheTopping()
    {
        var itemId = await NewItemAsync($"Tea test {Guid.NewGuid():N}"[..24]);
        Guid toppingId;
        await using (var scope = factory.Services.CreateAsyncScope())
        {
            var db = scope.ServiceProvider.GetRequiredService<ApplicationDbContext>();
            var categoryId = (await db.MenuItems.FirstAsync(m => m.Id == itemId)).CategoryId;
            var topping = new MenuTopping { Name = $"Oat {Guid.NewGuid():N}"[..12], Price = 1m, IsAvailable = true };
            db.MenuToppings.Add(topping);
            await db.SaveChangesAsync();
            db.MenuToppingCategories.Add(new MenuToppingCategory { ToppingId = topping.Id, CategoryId = categoryId });
            await db.SaveChangesAsync();
            toppingId = topping.Id;
            await scope.ServiceProvider.GetRequiredService<IMenuCacheService>().InvalidateAllMenuAsync();
        }
        var admin = factory.CreateClient();
        admin.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer",
            await ApiHelpers.CreateAdminTokenAsync(factory.Services, admin));
        var customer = factory.CreateClient();
        var (token, _) = await ApiHelpers.CreateCustomerAsync(customer, "+77002990202");
        ApiHelpers.Authorize(customer, token);

        var oat = (await (await admin.PostAsJsonAsync("/api/v1/admin/ingredients", new { name = $"Oat milk {Guid.NewGuid():N}"[..20], unit = "ml" }))
            .Content.ReadFromJsonAsync<JsonElement>()).GetProperty("id").GetGuid();
        Assert.Equal(HttpStatusCode.BadRequest, (await admin.PutAsJsonAsync($"/api/v1/admin/ingredients/toppings/{toppingId}",
            new { lines = new[] { new { ingredientId = oat, quantity = 0m } } })).StatusCode);
        (await admin.PutAsJsonAsync($"/api/v1/admin/ingredients/toppings/{toppingId}",
            new { lines = new[] { new { ingredientId = oat, quantity = 200m } } })).EnsureSuccessStatusCode();
        Assert.Single((await admin.GetFromJsonAsync<List<JsonElement>>($"/api/v1/admin/ingredients/toppings/{toppingId}"))!);
        (await admin.PutAsJsonAsync($"/api/v1/admin/ingredients/{oat}/stock/{LocationId}", new { quantity = 300m, lowStockAt = (decimal?)null }))
            .EnsureSuccessStatusCode();

        Task<HttpResponseMessage> Order(int quantity) => customer.PostAsJsonAsync("/api/v1/orders", new
        {
            locationId = LocationId, paymentMethod = "Cash",
            items = new[]
            {
                new { menuItemId = itemId, quantity, toppings = new[] { new { toppingId, toppingName = "Oat", price = 1m } } },
            },
        });
        async Task<bool> ToppingOnMenuAsync()
        {
            var json = await customer.GetFromJsonAsync<JsonElement>($"/api/v1/menu?locationId={LocationId}");
            var items = json.ValueKind == JsonValueKind.Array ? json : json.GetProperty("items");
            var item = items.EnumerateArray().First(i => i.GetProperty("id").GetGuid() == itemId);
            return item.TryGetProperty("availableToppings", out var tops) && tops.ValueKind == JsonValueKind.Array
                && tops.EnumerateArray().Any(t => t.GetProperty("id").GetGuid() == toppingId);
        }

        Assert.True(await ToppingOnMenuAsync());
        Assert.Equal(HttpStatusCode.BadRequest, (await Order(2)).StatusCode); // 400 ml needed, 300 left
        var placed = await Order(1);
        Assert.Equal(HttpStatusCode.Created, placed.StatusCode);
        var order = await placed.Content.ReadFromJsonAsync<ApiHelpers.OrderResult>(ApiHelpers.JsonOpts);
        (await admin.PostAsJsonAsync($"/api/v1/admin/orders/{order!.Id}/payment", new { paymentStatus = "Paid", paymentMethod = "Cash" }))
            .EnsureSuccessStatusCode();
        (await admin.PostAsJsonAsync($"/api/v1/admin/orders/{order.Id}/accept", new { etaMinutes = 5 })).EnsureSuccessStatusCode();

        var row = (await admin.GetFromJsonAsync<List<JsonElement>>($"/api/v1/admin/ingredients?locationId={LocationId}"))!
            .Single(r => r.GetProperty("id").GetGuid() == oat);
        Assert.Equal(100m, row.GetProperty("quantity").GetDecimal());
        Assert.False(await ToppingOnMenuAsync()); // 100 ml left, one topping needs 200

        (await admin.PutAsJsonAsync($"/api/v1/admin/ingredients/{oat}/stock/{LocationId}", new { quantity = 1000m, lowStockAt = (decimal?)null }))
            .EnsureSuccessStatusCode();
        Assert.True(await ToppingOnMenuAsync());

        // No longer counted: unlimited again.
        (await admin.PutAsJsonAsync($"/api/v1/admin/ingredients/{oat}/stock/{LocationId}", new { quantity = (decimal?)null, lowStockAt = (decimal?)null }))
            .EnsureSuccessStatusCode();
        Assert.Equal(HttpStatusCode.Created, (await Order(50)).StatusCode);
    }

    [Fact]
    public async Task Alerts_ListWhatIsLowOrOut_AndDropItWhenRestockedOrNotCounted()
    {
        var admin = factory.CreateClient();
        admin.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer",
            await ApiHelpers.CreateAdminTokenAsync(factory.Services, admin));
        var name = $"Sugar {Guid.NewGuid():N}"[..18];
        var sugar = (await (await admin.PostAsJsonAsync("/api/v1/admin/ingredients", new { name, unit = "g" }))
            .Content.ReadFromJsonAsync<JsonElement>()).GetProperty("id").GetGuid();

        async Task<JsonElement?> AlertAsync()
        {
            var json = await admin.GetFromJsonAsync<JsonElement>($"/api/v1/admin/stock/alerts?locationId={LocationId}");
            foreach (var a in json.GetProperty("items").EnumerateArray())
                if (a.GetProperty("name").GetString() == name) return a;
            return null;
        }
        Task Set(decimal? quantity, decimal? low) => admin.PutAsJsonAsync($"/api/v1/admin/ingredients/{sugar}/stock/{LocationId}", new { quantity, lowStockAt = low });

        Assert.Null(await AlertAsync()); // not counted
        await Set(500m, 100m);
        Assert.Null(await AlertAsync()); // plenty left
        await Set(80m, 100m);
        var low = await AlertAsync();
        Assert.NotNull(low);
        Assert.False(low!.Value.GetProperty("isOut").GetBoolean());
        Assert.Equal("ingredient", low.Value.GetProperty("kind").GetString());
        await Set(0m, 100m);
        Assert.True((await AlertAsync())!.Value.GetProperty("isOut").GetBoolean());

        var all = await admin.GetFromJsonAsync<JsonElement>("/api/v1/admin/stock/alerts");
        Assert.True(all.GetProperty("out").GetInt32() >= 1);

        await Set(null, null);
        Assert.Null(await AlertAsync());
    }
}
