using System.Net;
using System.Net.Http.Headers;
using System.Net.Http.Json;
using System.Text.Json;
using Yurt.Domain.Enums;
using Yurt.IntegrationTests.Helpers;

namespace Yurt.IntegrationTests.Tests;

/// <summary>
/// Purchasing (own_pos): suppliers and what they sell, delivery invoices that go on the shelf in one go, cancelling
/// a posted invoice, paid marks and what the cafe owes, and the "what to order" list.
/// </summary>
[Collection("Integration")]
public class PurchasingTests(YurtWebAppFactory factory)
{
    private static readonly Guid Downtown = Guid.Parse("11111111-0000-0000-0000-000000000001");
    private const string Api = "/api/v1/admin/purchasing";

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
            await ApiHelpers.CreateAdminTokenAsync(factory.Services, worker, "buyerbarista", "Barista@123", role: AdminRole.Worker));
        return worker;
    }

    private static string Unique(string prefix) => $"{prefix} {Guid.NewGuid():N}"[..20];

    private static async Task<Guid> NewIngredientAsync(HttpClient admin, string prefix, string unit)
    {
        var created = await admin.PostAsJsonAsync("/api/v1/admin/ingredients", new { name = Unique(prefix), unit });
        created.EnsureSuccessStatusCode();
        return (await created.Content.ReadFromJsonAsync<JsonElement>()).GetProperty("id").GetGuid();
    }

    private static async Task<JsonElement> NewSupplierAsync(HttpClient admin, string terms = "cash", int? payInDays = null, object[]? products = null)
    {
        var created = await admin.PostAsJsonAsync($"{Api}/suppliers", new
        {
            name = Unique("Supplier"), contactName = "Aida", phone = "+7 701 000 00 00", bin = "123456789012",
            paymentTerms = terms, payInDays, note = (string?)null, isActive = (bool?)null, products = products ?? [],
        });
        Assert.Equal(HttpStatusCode.Created, created.StatusCode);
        return await created.Content.ReadFromJsonAsync<JsonElement>();
    }

    private static async Task<decimal?> LeftAsync(HttpClient client, Guid ingredientId)
    {
        var list = await client.GetFromJsonAsync<List<JsonElement>>($"/api/v1/admin/ingredients?locationId={Downtown}");
        var row = list!.Single(r => r.GetProperty("id").GetGuid() == ingredientId);
        return row.TryGetProperty("quantity", out var q) && q.ValueKind == JsonValueKind.Number ? q.GetDecimal() : null;
    }

    private static async Task<decimal?> CostAsync(HttpClient client, Guid ingredientId)
    {
        var list = await client.GetFromJsonAsync<List<JsonElement>>($"/api/v1/admin/ingredients?locationId={Downtown}");
        var row = list!.Single(r => r.GetProperty("id").GetGuid() == ingredientId);
        return row.TryGetProperty("costPerUnit", out var c) && c.ValueKind == JsonValueKind.Number ? c.GetDecimal() : null;
    }

    private static object Invoice(Guid supplierId, string status, params object[] lines)
        => new { supplierId, locationId = Downtown, status, number = "N-17", date = (DateTime?)null, note = (string?)null, photoUrl = (string?)null, lines };

    [Fact]
    public async Task Suppliers_AdminsManage_StaffOnlyRead()
    {
        var admin = await AdminAsync();
        var worker = await WorkerAsync();
        var milk = await NewIngredientAsync(admin, "Milk", "ml");
        var supplier = await NewSupplierAsync(admin, "later", 14,
            [new { ingredientId = milk, packSize = 12000m, packName = "box 12 x 1 L", packPrice = 6240m }]);
        var id = supplier.GetProperty("id").GetGuid();
        Assert.Equal("later", supplier.GetProperty("paymentTerms").GetString());
        Assert.Equal(14, supplier.GetProperty("payInDays").GetInt32());
        var product = supplier.GetProperty("products").EnumerateArray().Single();
        Assert.Equal(12000m, product.GetProperty("packSize").GetDecimal());

        // Same name twice, bad terms, an ingredient twice: refused.
        Assert.Equal(HttpStatusCode.Conflict, (await admin.PostAsJsonAsync($"{Api}/suppliers",
            new { name = supplier.GetProperty("name").GetString(), paymentTerms = "cash" })).StatusCode);
        Assert.Equal(HttpStatusCode.BadRequest, (await admin.PostAsJsonAsync($"{Api}/suppliers",
            new { name = Unique("S"), paymentTerms = "barter" })).StatusCode);
        Assert.Equal(HttpStatusCode.BadRequest, (await admin.PostAsJsonAsync($"{Api}/suppliers", new
        {
            name = Unique("S"), products = new[] { new { ingredientId = milk, packSize = 1m }, new { ingredientId = milk, packSize = 2m } },
        })).StatusCode);

        // Staff can see the list (to pick a supplier on a delivery) but not change it or see what is owed.
        var seen = await worker.GetFromJsonAsync<List<JsonElement>>($"{Api}/suppliers");
        var mine = seen!.Single(s => s.GetProperty("id").GetGuid() == id);
        Assert.False(mine.TryGetProperty("owed", out var owedByStaff) && owedByStaff.ValueKind == JsonValueKind.Number);
        Assert.Equal(HttpStatusCode.Forbidden, (await worker.PostAsJsonAsync($"{Api}/suppliers", new { name = Unique("S") })).StatusCode);
        Assert.Equal(HttpStatusCode.Forbidden, (await worker.DeleteAsync($"{Api}/suppliers/{id}")).StatusCode);

        // Updating replaces what it sells.
        var updated = await admin.PutAsJsonAsync($"{Api}/suppliers/{id}", new
        {
            name = supplier.GetProperty("name").GetString(), paymentTerms = "transfer", payInDays = 30, products = Array.Empty<object>(),
        });
        updated.EnsureSuccessStatusCode();
        var body = await updated.Content.ReadFromJsonAsync<JsonElement>();
        Assert.Empty(body.GetProperty("products").EnumerateArray());
        Assert.False(body.TryGetProperty("payInDays", out var days) && days.ValueKind == JsonValueKind.Number); // only for "pay later"

        // Without invoices it is deleted for good.
        var deleted = await admin.DeleteAsync($"{Api}/suppliers/{id}");
        Assert.True(await deleted.Content.ReadFromJsonAsync<bool>());
    }

    [Fact]
    public async Task PostingAnInvoice_PutsEveryLineOnTheShelf_WithCostAndHistory()
    {
        var admin = await AdminAsync();
        var worker = await WorkerAsync();
        var beans = await NewIngredientAsync(admin, "Beans", "g");
        var cups = await NewIngredientAsync(admin, "Cups", "pcs");
        (await admin.PutAsJsonAsync($"/api/v1/admin/ingredients/{beans}/stock/{Downtown}", new { quantity = 1000m, lowStockAt = (decimal?)null })).EnsureSuccessStatusCode();
        (await admin.PutAsJsonAsync($"/api/v1/admin/ingredients/{beans}/cost", new { costPerUnit = 8m })).EnsureSuccessStatusCode();
        var supplier = (await NewSupplierAsync(admin, "cash")).GetProperty("id").GetGuid();

        // A barista types in the delivery and posts it.
        var created = await worker.PostAsJsonAsync($"{Api}/purchases", Invoice(supplier, "draft",
            new { ingredientId = beans, quantity = 1000m, total = (decimal?)12000m },
            new { ingredientId = cups, quantity = 500m, total = (decimal?)null }));
        Assert.Equal(HttpStatusCode.Created, created.StatusCode);
        var invoice = await created.Content.ReadFromJsonAsync<JsonElement>();
        var id = invoice.GetProperty("id").GetGuid();
        Assert.Equal(12000m, invoice.GetProperty("total").GetDecimal());
        Assert.Equal(1000m, await LeftAsync(admin, beans)); // nothing on the shelf before posting

        var posted = await worker.PostAsync($"{Api}/purchases/{id}/post", null);
        posted.EnsureSuccessStatusCode();
        var result = await posted.Content.ReadFromJsonAsync<JsonElement>();
        Assert.Equal("posted", result.GetProperty("purchase").GetProperty("status").GetString());
        Assert.True(result.GetProperty("purchase").GetProperty("isPaid").GetBoolean()); // cash supplier
        Assert.Equal(2000m, await LeftAsync(admin, beans));
        Assert.Equal(500m, await LeftAsync(admin, cups)); // started counting
        Assert.Equal(10m, await CostAsync(admin, beans)); // 1000 g at 8 + 1000 g at 12

        var history = await admin.GetFromJsonAsync<List<JsonElement>>($"/api/v1/admin/stock/history?locationId={Downtown}&take=500");
        var rows = history!.Where(r => r.TryGetProperty("purchaseId", out var p) && p.ValueKind == JsonValueKind.String && p.GetGuid() == id).ToList();
        Assert.Equal(2, rows.Count);
        Assert.All(rows, r => Assert.Equal("restock", r.GetProperty("type").GetString()));
        Assert.All(rows, r => Assert.Contains("N-17", r.GetProperty("note").GetString()));

        // Posted invoices cannot be changed, deleted or posted again.
        Assert.Equal(HttpStatusCode.Conflict, (await admin.PutAsJsonAsync($"{Api}/purchases/{id}", Invoice(supplier, "draft",
            new { ingredientId = beans, quantity = 1m, total = (decimal?)null }))).StatusCode);
        Assert.Equal(HttpStatusCode.Conflict, (await admin.DeleteAsync($"{Api}/purchases/{id}")).StatusCode);
        Assert.Equal(HttpStatusCode.Conflict, (await admin.PostAsync($"{Api}/purchases/{id}/post", null)).StatusCode);

        // Bad invoices are refused.
        Assert.Equal(HttpStatusCode.BadRequest, (await admin.PostAsJsonAsync($"{Api}/purchases", Invoice(supplier, "draft",
            new { ingredientId = beans, quantity = 0m, total = (decimal?)null }))).StatusCode);
        Assert.Equal(HttpStatusCode.BadRequest, (await admin.PostAsJsonAsync($"{Api}/purchases", Invoice(supplier, "draft",
            new { ingredientId = beans, quantity = 1m, total = (decimal?)null }, new { ingredientId = beans, quantity = 2m, total = (decimal?)null }))).StatusCode);
        Assert.Equal(HttpStatusCode.BadRequest, (await admin.PostAsJsonAsync($"{Api}/purchases", Invoice(supplier, "posted",
            new { ingredientId = beans, quantity = 1m, total = (decimal?)null }))).StatusCode);
    }

    [Fact]
    public async Task CancellingAnInvoice_TakesItBack_UnlessItWasUsed()
    {
        var admin = await AdminAsync();
        var worker = await WorkerAsync();
        var milk = await NewIngredientAsync(admin, "Milk", "ml");
        (await admin.PutAsJsonAsync($"/api/v1/admin/ingredients/{milk}/stock/{Downtown}", new { quantity = 1000m, lowStockAt = (decimal?)null })).EnsureSuccessStatusCode();
        (await admin.PutAsJsonAsync($"/api/v1/admin/ingredients/{milk}/cost", new { costPerUnit = 0.5m })).EnsureSuccessStatusCode();
        var supplier = (await NewSupplierAsync(admin, "transfer")).GetProperty("id").GetGuid();

        async Task<Guid> PostedAsync(decimal quantity, decimal total)
        {
            var created = await admin.PostAsJsonAsync($"{Api}/purchases", Invoice(supplier, "draft", new { ingredientId = milk, quantity, total = (decimal?)total }));
            var id = (await created.Content.ReadFromJsonAsync<JsonElement>()).GetProperty("id").GetGuid();
            (await admin.PostAsync($"{Api}/purchases/{id}/post", null)).EnsureSuccessStatusCode();
            return id;
        }

        var wrong = await PostedAsync(1000m, 1000m); // 1 per ml: average becomes 0.75
        Assert.Equal(0.75m, await CostAsync(admin, milk));
        Assert.Equal(HttpStatusCode.Forbidden, (await worker.PostAsync($"{Api}/purchases/{wrong}/cancel", null)).StatusCode);
        var cancelled = await admin.PostAsync($"{Api}/purchases/{wrong}/cancel", null);
        cancelled.EnsureSuccessStatusCode();
        Assert.Equal("cancelled", (await cancelled.Content.ReadFromJsonAsync<JsonElement>()).GetProperty("status").GetString());
        Assert.Equal(1000m, await LeftAsync(admin, milk));
        Assert.Equal(0.5m, await CostAsync(admin, milk));
        Assert.Equal(HttpStatusCode.Conflict, (await admin.PostAsync($"{Api}/purchases/{wrong}/cancel", null)).StatusCode);

        // Once most of it is used, cancelling would go below zero: refused with what is left.
        var used = await PostedAsync(500m, 250m);
        (await worker.PostAsJsonAsync("/api/v1/admin/stock/write-off",
            new { locationId = Downtown, kind = "ingredient", itemId = milk, quantity = 1200m, reason = "spilled", note = "" })).EnsureSuccessStatusCode();
        var refused = await admin.PostAsync($"{Api}/purchases/{used}/cancel", null);
        Assert.Equal(HttpStatusCode.Conflict, refused.StatusCode);
        Assert.Contains("Only 300", await refused.Content.ReadAsStringAsync());
        Assert.Equal(300m, await LeftAsync(admin, milk));
    }

    [Fact]
    public async Task PayLaterInvoices_GetADueDate_AndShowAsOwed_UntilPaid()
    {
        var admin = await AdminAsync();
        var worker = await WorkerAsync();
        var flour = await NewIngredientAsync(admin, "Flour", "g");
        var supplier = (await NewSupplierAsync(admin, "later", 7)).GetProperty("id").GetGuid();
        var created = await admin.PostAsJsonAsync($"{Api}/purchases", new
        {
            supplierId = supplier, locationId = Downtown, status = "draft", number = "F-1", date = DateTime.UtcNow.Date.AddDays(-10),
            lines = new[] { new { ingredientId = flour, quantity = 5000m, total = (decimal?)4500m } },
        });
        var id = (await created.Content.ReadFromJsonAsync<JsonElement>()).GetProperty("id").GetGuid();
        var posted = (await (await admin.PostAsync($"{Api}/purchases/{id}/post", null)).Content.ReadFromJsonAsync<JsonElement>()).GetProperty("purchase");
        Assert.False(posted.GetProperty("isPaid").GetBoolean());
        Assert.True(posted.GetProperty("isOverdue").GetBoolean()); // delivered 10 days ago, pay in 7

        var owed = (await admin.GetFromJsonAsync<List<JsonElement>>($"{Api}/suppliers"))!.Single(s => s.GetProperty("id").GetGuid() == supplier);
        Assert.Equal(4500m, owed.GetProperty("owed").GetDecimal());
        Assert.Equal(4500m, owed.GetProperty("overdue").GetDecimal());
        var unpaid = await admin.GetFromJsonAsync<List<JsonElement>>($"{Api}/purchases?supplierId={supplier}&unpaid=true");
        Assert.Single(unpaid!);

        Assert.Equal(HttpStatusCode.Forbidden, (await worker.PutAsJsonAsync($"{Api}/purchases/{id}/paid", new { paid = true })).StatusCode);
        (await admin.PutAsJsonAsync($"{Api}/purchases/{id}/paid", new { paid = true })).EnsureSuccessStatusCode();
        owed = (await admin.GetFromJsonAsync<List<JsonElement>>($"{Api}/suppliers"))!.Single(s => s.GetProperty("id").GetGuid() == supplier);
        Assert.Equal(0m, owed.GetProperty("owed").GetDecimal());

        // A supplier with invoices is only switched off, and a new invoice cannot use it.
        Assert.False(await (await admin.DeleteAsync($"{Api}/suppliers/{supplier}")).Content.ReadFromJsonAsync<bool>());
        Assert.Equal(HttpStatusCode.BadRequest, (await admin.PostAsJsonAsync($"{Api}/purchases", Invoice(supplier, "draft",
            new { ingredientId = flour, quantity = 1m, total = (decimal?)null }))).StatusCode);
    }

    [Fact]
    public async Task WhatToOrder_SuggestsWholePacks_FromTheCheapestSupplier_LessWhatIsOrdered()
    {
        var admin = await AdminAsync();
        var oat = await NewIngredientAsync(admin, "Oat milk", "ml");
        var lids = await NewIngredientAsync(admin, "Lids", "pcs");
        var plenty = await NewIngredientAsync(admin, "Sugar", "g");
        (await admin.PutAsJsonAsync($"/api/v1/admin/ingredients/{oat}/stock/{Downtown}", new { quantity = 1500m, lowStockAt = (decimal?)2000m })).EnsureSuccessStatusCode();
        (await admin.PutAsJsonAsync($"/api/v1/admin/ingredients/{lids}/stock/{Downtown}", new { quantity = 10m, lowStockAt = (decimal?)50m })).EnsureSuccessStatusCode();
        (await admin.PutAsJsonAsync($"/api/v1/admin/ingredients/{plenty}/stock/{Downtown}", new { quantity = 9000m, lowStockAt = (decimal?)1000m })).EnsureSuccessStatusCode();
        (await admin.PutAsJsonAsync($"{Api}/order-up-to/{oat}/{Downtown}", new { orderUpTo = 10000m })).EnsureSuccessStatusCode();

        // Two suppliers sell oat milk; the cheaper one per ml wins. Lids have no supplier yet.
        var dear = (await NewSupplierAsync(admin, products: [new { ingredientId = oat, packSize = 1000m, packName = "1 L", packPrice = 1500m }])).GetProperty("id").GetGuid();
        var cheap = (await NewSupplierAsync(admin, products: [new { ingredientId = oat, packSize = 6000m, packName = "6 x 1 L", packPrice = 7200m }])).GetProperty("id").GetGuid();

        var groups = await admin.GetFromJsonAsync<List<JsonElement>>($"{Api}/to-order?locationId={Downtown}");
        var line = groups!.SelectMany(g => g.GetProperty("lines").EnumerateArray().Select(l => (Group: g, Line: l)))
            .Single(x => x.Line.GetProperty("ingredientId").GetGuid() == oat);
        Assert.Equal(cheap, line.Group.GetProperty("supplierId").GetGuid());
        Assert.Equal(2m, line.Line.GetProperty("packs").GetDecimal());        // need 8500 ml -> 2 packs of 6000
        Assert.Equal(12000m, line.Line.GetProperty("suggested").GetDecimal());
        Assert.Equal(14400m, line.Line.GetProperty("estimatedCost").GetDecimal());
        var lidLine = groups!.SelectMany(g => g.GetProperty("lines").EnumerateArray().Select(l => (Group: g, Line: l)))
            .Single(x => x.Line.GetProperty("ingredientId").GetGuid() == lids);
        Assert.False(lidLine.Group.TryGetProperty("supplierId", out var sup) && sup.ValueKind == JsonValueKind.String);
        Assert.Equal(90m, lidLine.Line.GetProperty("suggested").GetDecimal()); // up to twice the warn-at number
        Assert.DoesNotContain(groups!.SelectMany(g => g.GetProperty("lines").EnumerateArray()), l => l.GetProperty("ingredientId").GetGuid() == plenty);

        // Turning the suggestion into an order: the list then counts it as already ordered.
        var order = await admin.PostAsJsonAsync($"{Api}/purchases", Invoice(cheap, "ordered", new { ingredientId = oat, quantity = 12000m, total = (decimal?)14400m }));
        Assert.Equal(HttpStatusCode.Created, order.StatusCode);
        var orderId = (await order.Content.ReadFromJsonAsync<JsonElement>()).GetProperty("id").GetGuid();
        groups = await admin.GetFromJsonAsync<List<JsonElement>>($"{Api}/to-order?locationId={Downtown}");
        var after = groups!.SelectMany(g => g.GetProperty("lines").EnumerateArray()).Single(l => l.GetProperty("ingredientId").GetGuid() == oat);
        Assert.Equal(12000m, after.GetProperty("alreadyOrdered").GetDecimal());
        Assert.Equal(0m, after.GetProperty("suggested").GetDecimal());

        // The goods arrive: the order becomes the invoice and is posted.
        (await admin.PutAsJsonAsync($"{Api}/purchases/{orderId}", Invoice(cheap, "draft", new { ingredientId = oat, quantity = 12000m, total = (decimal?)14000m }))).EnsureSuccessStatusCode();
        (await admin.PostAsync($"{Api}/purchases/{orderId}/post", null)).EnsureSuccessStatusCode();
        Assert.Equal(13500m, await LeftAsync(admin, oat));
        groups = await admin.GetFromJsonAsync<List<JsonElement>>($"{Api}/to-order?locationId={Downtown}");
        Assert.DoesNotContain(groups!.SelectMany(g => g.GetProperty("lines").EnumerateArray()), l => l.GetProperty("ingredientId").GetGuid() == oat);
        Assert.NotEqual(dear, cheap);
    }
}
