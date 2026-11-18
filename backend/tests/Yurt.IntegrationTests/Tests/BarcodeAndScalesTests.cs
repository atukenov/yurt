using System.Net;
using System.Net.Http.Json;
using System.Text.Json;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Yurt.Domain.Entities;
using Yurt.Domain.Enums;
using Yurt.Infrastructure.Persistence;
using Yurt.IntegrationTests.Helpers;

namespace Yurt.IntegrationTests.Tests;

/// <summary>Barcodes on dishes and ingredients, and dishes sold by weight at the till.</summary>
[Collection("Integration")]
public class BarcodeAndScalesTests(YurtWebAppFactory factory)
{
    private static readonly Guid LocationId = Guid.Parse("11111111-0000-0000-0000-000000000001");
    private const string Items = "/api/v1/admin/menu/items";
    private const string Ingredients = "/api/v1/admin/ingredients";

    private async Task<HttpClient> ClientAsync(string username = "testadmin", string password = "TestAdmin@123", AdminRole role = AdminRole.Admin)
    {
        var client = factory.CreateClient();
        ApiHelpers.Authorize(client, await ApiHelpers.CreateAdminTokenAsync(factory.Services, client, username, password, role: role));
        return client;
    }

    private static string Unique(string p) => $"{p} {Guid.NewGuid():N}"[..16];
    private static string Code() => Random.Shared.NextInt64(1_000_000_000_000, 9_999_999_999_999).ToString();

    private async Task<Guid> CategoryAsync()
    {
        await using var scope = factory.Services.CreateAsyncScope();
        return (await scope.ServiceProvider.GetRequiredService<ApplicationDbContext>().MenuCategories.FirstAsync()).Id;
    }

    private async Task<HttpResponseMessage> SaveItemAsync(HttpClient admin, Guid category, string name, decimal price,
        string? barcode, bool byWeight, object[]? variants = null, Guid? updateId = null)
    {
        var body = new { categoryId = category, name, price, isAvailable = true, description = "t", barcode, soldByWeight = byWeight, variants };
        return updateId is { } id ? await admin.PutAsJsonAsync($"{Items}/{id}", body) : await admin.PostAsJsonAsync(Items, body);
    }

    [Fact]
    public async Task DishBarcode_IsSaved_ShownToTheTill_AndMustBeUniqueAndClean()
    {
        var admin = await ClientAsync();
        var cat = await CategoryAsync();
        var code = Code();

        var made = await SaveItemAsync(admin, cat, Unique("Scan"), 5m, $" {code} ", false);
        Assert.Equal(HttpStatusCode.Created, made.StatusCode);
        var item = await made.Content.ReadFromJsonAsync<JsonElement>();
        Assert.Equal(code, item.GetProperty("barcode").GetString()); // trimmed
        var id = item.GetProperty("id").GetGuid();

        // The till reads it from the public menu (and keeps it for offline use).
        var shown = await admin.GetFromJsonAsync<JsonElement>($"/api/v1/menu/items/{id}?lang=en");
        Assert.Equal(code, shown.GetProperty("barcode").GetString());

        // Another dish can't take the same code; the dish itself can keep it when saved again.
        Assert.Equal(HttpStatusCode.Conflict, (await SaveItemAsync(admin, cat, Unique("Dup"), 5m, code, false)).StatusCode);
        Assert.Equal(HttpStatusCode.OK, (await SaveItemAsync(admin, cat, Unique("Scan"), 6m, code, false, updateId: id)).StatusCode);

        // A scanner's stray control characters or an over-long code are refused.
        Assert.Equal(HttpStatusCode.BadRequest, (await SaveItemAsync(admin, cat, Unique("Bad"), 5m, "12 34\t", false)).StatusCode);
        Assert.Equal(HttpStatusCode.BadRequest, (await SaveItemAsync(admin, cat, Unique("Long"), 5m, new string('1', 65), false)).StatusCode);

        // Clearing the field removes the code, and the code is free again.
        var cleared = await SaveItemAsync(admin, cat, Unique("Scan"), 6m, "  ", false, updateId: id);
        Assert.Equal(HttpStatusCode.OK, cleared.StatusCode);
        var clearedItem = await cleared.Content.ReadFromJsonAsync<JsonElement>();
        Assert.True(!clearedItem.TryGetProperty("barcode", out var gone) || gone.ValueKind == JsonValueKind.Null);
        Assert.Equal(HttpStatusCode.Created, (await SaveItemAsync(admin, cat, Unique("Again"), 5m, code, false)).StatusCode);
    }

    [Fact]
    public async Task IngredientBarcode_FindsOneIngredient_AndMustBeUnique()
    {
        var admin = await ClientAsync();
        var code = Code();
        var made = await admin.PostAsJsonAsync(Ingredients, new { name = Unique("Flour"), unit = "g", barcode = code });
        Assert.Equal(HttpStatusCode.Created, made.StatusCode);
        Assert.Equal(HttpStatusCode.Conflict,
            (await admin.PostAsJsonAsync(Ingredients, new { name = Unique("Rice"), unit = "g", barcode = code })).StatusCode);
        Assert.Equal(HttpStatusCode.BadRequest,
            (await admin.PostAsJsonAsync(Ingredients, new { name = Unique("Salt"), unit = "g", barcode = "a b" })).StatusCode);

        var list = await admin.GetFromJsonAsync<List<JsonElement>>($"{Ingredients}?locationId={LocationId}");
        Assert.Single(list!, r => r.TryGetProperty("barcode", out var b) && b.ValueKind == JsonValueKind.String && b.GetString() == code);
    }

    [Fact]
    public async Task WeighedDish_IsPricedByWeight_TakesStockAndCostByWeight_AndOnlyAtTheTill()
    {
        var ceo = await ClientAsync($"wceo-{Guid.NewGuid():N}"[..14], "Ceo@123456", AdminRole.Ceo);
        var cat = await CategoryAsync();

        // A dish sold by weight can't have sizes.
        Assert.Equal(HttpStatusCode.BadRequest, (await SaveItemAsync(ceo, cat, Unique("Sized"), 100m, null, true,
            variants: [new { label = "L", price = 1m, sortOrder = 0, isDefault = true }])).StatusCode);

        // Cheese: 100 per kg. One kg uses 1000 g of cheese stock at 2 per g.
        var made = await SaveItemAsync(ceo, cat, Unique("Cheese"), 100m, null, true);
        Assert.Equal(HttpStatusCode.Created, made.StatusCode);
        var cheese = (await made.Content.ReadFromJsonAsync<JsonElement>()).GetProperty("id").GetGuid();
        var stockItem = (await (await ceo.PostAsJsonAsync(Ingredients, new { name = Unique("Wheel"), unit = "g" }))
            .Content.ReadFromJsonAsync<JsonElement>()).GetProperty("id").GetGuid();
        (await ceo.PutAsJsonAsync($"{Ingredients}/{stockItem}/cost", new { costPerUnit = 2m })).EnsureSuccessStatusCode();
        (await ceo.PutAsJsonAsync($"{Ingredients}/{stockItem}/stock/{LocationId}", new { quantity = 5000m, lowStockAt = (decimal?)null })).EnsureSuccessStatusCode();
        (await ceo.PutAsJsonAsync($"{Ingredients}/recipes/{cheese}",
            new { lines = new[] { new { ingredientId = stockItem, variantId = (Guid?)null, quantity = 1000m } } })).EnsureSuccessStatusCode();

        Task<HttpResponseMessage> Sell(object line) => ceo.PostAsJsonAsync("/api/v1/admin/counter-sales", new
        {
            locationId = LocationId, paymentMethod = "Cash", idempotencyKey = Guid.NewGuid().ToString(), handedOver = true,
            items = new[] { line },
        });

        // Needs a weight; the weight is for weighed dishes only; one line per weighing.
        Assert.Equal(HttpStatusCode.BadRequest, (await Sell(new { menuItemId = cheese, quantity = 1 })).StatusCode);
        Assert.Equal(HttpStatusCode.BadRequest, (await Sell(new { menuItemId = cheese, quantity = 1, weightGrams = 0 })).StatusCode);
        Assert.Equal(HttpStatusCode.BadRequest, (await Sell(new { menuItemId = cheese, quantity = 2, weightGrams = 300 })).StatusCode);

        // 350 g of a 100/kg dish = 35.
        var sold = await Sell(new { menuItemId = cheese, quantity = 1, weightGrams = 350 });
        Assert.Equal(HttpStatusCode.Created, sold.StatusCode);
        var sale = await sold.Content.ReadFromJsonAsync<JsonElement>();
        Assert.Equal(35m, sale.GetProperty("total").GetDecimal());
        var line = sale.GetProperty("items")[0];
        Assert.Equal(350, line.GetProperty("weightGrams").GetInt32());
        Assert.Equal(35m, line.GetProperty("lineTotal").GetDecimal());

        // A plain dish takes no weight.
        var (_, plain) = await NewItemAsync(Unique("Plain"), 5m);
        Assert.Equal(HttpStatusCode.BadRequest, (await Sell(new { menuItemId = plain, quantity = 1, weightGrams = 200 })).StatusCode);

        // Ingredient count went down by 0.35 kg of the recipe (350 g), the cost saved is 0.35 × 1000 × 2 = 700.
        var counted = (await ceo.GetFromJsonAsync<List<JsonElement>>($"{Ingredients}?locationId={LocationId}"))!
            .Single(r => r.GetProperty("id").GetGuid() == stockItem);
        Assert.Equal(4650m, counted.GetProperty("quantity").GetDecimal());
        await using var scope = factory.Services.CreateAsyncScope();
        var db = scope.ServiceProvider.GetRequiredService<ApplicationDbContext>();
        var saved = await db.OrderItems.SingleAsync(i => i.Id == line.GetProperty("id").GetGuid());
        Assert.Equal(350, saved.WeightGrams);
        Assert.Equal(700m, saved.UnitCost);

        // The app can't order it: it needs the scale.
        var guest = factory.CreateClient();
        var (token, _) = await ApiHelpers.CreateCustomerAsync(guest, $"+7701{Random.Shared.Next(1000000, 9999999)}");
        ApiHelpers.Authorize(guest, token);
        var online = await guest.PostAsJsonAsync("/api/v1/orders", new
        {
            locationId = LocationId, paymentMethod = "Cash", items = new[] { new { menuItemId = cheese, quantity = 1 } },
        });
        Assert.Equal(HttpStatusCode.BadRequest, online.StatusCode);
        Assert.Contains("sold by weight", await online.Content.ReadAsStringAsync());
    }

    private async Task<(Guid Category, Guid Item)> NewItemAsync(string name, decimal price)
    {
        await using var scope = factory.Services.CreateAsyncScope();
        var db = scope.ServiceProvider.GetRequiredService<ApplicationDbContext>();
        var category = await db.MenuCategories.FirstAsync();
        var item = new MenuItem { CategoryId = category.Id, Name = name, Description = "t", Price = price, IsAvailable = true };
        db.MenuItems.Add(item);
        await db.SaveChangesAsync();
        return (category.Id, item.Id);
    }
}
