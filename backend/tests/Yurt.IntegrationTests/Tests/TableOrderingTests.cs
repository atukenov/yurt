using System.Net;
using System.Net.Http.Headers;
using System.Net.Http.Json;
using System.Text.Json;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Yurt.Domain;
using Yurt.Domain.Entities;
using Yurt.Domain.Enums;
using Yurt.Infrastructure.Persistence;
using Yurt.IntegrationTests.Helpers;

namespace Yurt.IntegrationTests.Tests;

/// <summary>Tables with QR codes: a guest who scans one orders to that table (table_ordering).</summary>
[Collection("Integration")]
public class TableOrderingTests(YurtWebAppFactory factory)
{
    private static readonly Guid LocationId = Guid.Parse("11111111-0000-0000-0000-000000000001");

    private static object License(bool enabled)
        => new { enabled, validUntil = (DateTime?)null, source = "Manual", notes = (string?)null };

    private async Task<HttpClient> AdminAsync()
    {
        var admin = factory.CreateClient();
        admin.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer",
            await ApiHelpers.CreateAdminTokenAsync(factory.Services, admin));
        return admin;
    }

    private async Task<Guid> MenuItemAsync()
    {
        await using var scope = factory.Services.CreateAsyncScope();
        var db = scope.ServiceProvider.GetRequiredService<ApplicationDbContext>();
        var item = new MenuItem { CategoryId = (await db.MenuCategories.FirstAsync()).Id, Name = $"Table item {Guid.NewGuid():N}"[..20], Description = "t", Price = 4m, IsAvailable = true };
        db.MenuItems.Add(item);
        await db.SaveChangesAsync();
        await scope.ServiceProvider.GetRequiredService<Yurt.Application.Common.Interfaces.IMenuCacheService>().InvalidateAllMenuAsync();
        return item.Id;
    }

    private static bool HasTable(JsonElement order)
        => order.TryGetProperty("tableLabel", out var t) && t.ValueKind != JsonValueKind.Null;

    private static string Unique(string prefix) => $"{prefix}{Guid.NewGuid():N}"[..(prefix.Length + 6)];

    [Fact]
    public async Task Tables_GetQrCodes_AndAScannedCodeSendsTheOrderToThatTable()
    {
        var admin = await AdminAsync();
        string a = Unique("A"), b = Unique("B");

        Assert.Equal(HttpStatusCode.BadRequest, (await admin.PostAsJsonAsync("/api/v1/admin/tables", new { locationId = LocationId, labels = new[] { " " } })).StatusCode);
        Assert.Equal(HttpStatusCode.BadRequest, (await admin.PostAsJsonAsync("/api/v1/admin/tables", new { locationId = LocationId, labels = new[] { a, a.ToLowerInvariant() } })).StatusCode);
        Assert.Equal(HttpStatusCode.NotFound, (await admin.PostAsJsonAsync("/api/v1/admin/tables", new { locationId = Guid.NewGuid(), labels = new[] { a } })).StatusCode);

        var created = await admin.PostAsJsonAsync("/api/v1/admin/tables", new { locationId = LocationId, labels = new[] { a, b } });
        Assert.Equal(HttpStatusCode.Created, created.StatusCode);
        var tables = (await created.Content.ReadFromJsonAsync<JsonElement>()).EnumerateArray().ToList();
        Assert.Equal(2, tables.Count);
        var tableA = tables[0];
        var code = tableA.GetProperty("code").GetString()!;
        Assert.Equal(10, code.Length);
        Assert.Equal($"https://app.test.invalid/t/{code}", tableA.GetProperty("link").GetString());
        Assert.StartsWith("data:image/svg+xml;base64,", tableA.GetProperty("qrSvg").GetString());
        Assert.Equal(HttpStatusCode.Conflict, (await admin.PostAsJsonAsync("/api/v1/admin/tables", new { locationId = LocationId, labels = new[] { b.ToUpperInvariant() } })).StatusCode);

        var listed = await admin.GetFromJsonAsync<JsonElement>($"/api/v1/admin/tables?locationId={LocationId}");
        Assert.Contains(listed.EnumerateArray(), t => t.GetProperty("label").GetString() == b);

        // Anyone can look a code up (the guest may not be signed in yet); lower case works too.
        var anonymous = factory.CreateClient();
        var lookup = await anonymous.GetFromJsonAsync<JsonElement>($"/api/v1/tables/{code.ToLowerInvariant()}");
        Assert.Equal(a, lookup.GetProperty("label").GetString());
        Assert.Equal(LocationId, lookup.GetProperty("locationId").GetGuid());
        Assert.Equal(HttpStatusCode.NotFound, (await anonymous.GetAsync("/api/v1/tables/NOSUCHCODE")).StatusCode);

        var itemId = await MenuItemAsync();
        var customer = factory.CreateClient();
        var (token, _) = await ApiHelpers.CreateCustomerAsync(customer, "+77002990701");
        ApiHelpers.Authorize(customer, token);
        Task<HttpResponseMessage> Place(string? tableCode, Guid? locationId = null) => customer.PostAsJsonAsync("/api/v1/orders", new
        {
            locationId = locationId ?? LocationId, paymentMethod = "Cash", tableCode,
            items = new[] { new { menuItemId = itemId, quantity = 1, toppings = Array.Empty<object>() } },
        });

        // A table order is brought over as soon as it is made: a pickup time sent along is dropped.
        var withTime = await customer.PostAsJsonAsync("/api/v1/orders", new
        {
            locationId = LocationId, paymentMethod = "Cash", tableCode = code, pickupAt = DateTime.UtcNow.AddHours(1),
            items = new[] { new { menuItemId = itemId, quantity = 1, toppings = Array.Empty<object>() } },
        });
        Assert.Equal(HttpStatusCode.Created, withTime.StatusCode);
        var timed = await withTime.Content.ReadFromJsonAsync<JsonElement>();
        Assert.True(HasTable(timed));
        Assert.False(timed.TryGetProperty("pickupAt", out var at) && at.ValueKind != JsonValueKind.Null);

        var placed = await Place(code);
        Assert.Equal(HttpStatusCode.Created, placed.StatusCode);
        var order = await placed.Content.ReadFromJsonAsync<JsonElement>();
        Assert.Equal(a, order.GetProperty("tableLabel").GetString());
        var orderId = order.GetProperty("id").GetGuid();

        // A pickup order has no table, and a made-up code is refused instead of losing the table quietly.
        var pickup = await (await Place(null)).Content.ReadFromJsonAsync<JsonElement>();
        Assert.False(HasTable(pickup));
        Assert.Equal(HttpStatusCode.BadRequest, (await Place("NOSUCHCODE")).StatusCode);

        // Renaming keeps the old orders as they were; a new code makes the old sticker stop working.
        var idA = tableA.GetProperty("id").GetGuid();
        (await admin.PutAsJsonAsync($"/api/v1/admin/tables/{idA}", new { label = Unique("R") })).EnsureSuccessStatusCode();
        Assert.Equal(HttpStatusCode.Conflict, (await admin.PutAsJsonAsync($"/api/v1/admin/tables/{idA}", new { label = b })).StatusCode);
        var renewed = await (await admin.PostAsync($"/api/v1/admin/tables/{idA}/new-code", null)).Content.ReadFromJsonAsync<JsonElement>();
        Assert.NotEqual(code, renewed.GetProperty("code").GetString());
        Assert.Equal(HttpStatusCode.NotFound, (await anonymous.GetAsync($"/api/v1/tables/{code}")).StatusCode);
        Assert.Equal(HttpStatusCode.BadRequest, (await Place(code)).StatusCode);

        var mine = await customer.GetFromJsonAsync<JsonElement>($"/api/v1/orders/{orderId}");
        Assert.Equal(a, mine.GetProperty("tableLabel").GetString());

        // Deleting a table keeps its orders and their label.
        Assert.Equal(HttpStatusCode.OK, (await admin.DeleteAsync($"/api/v1/admin/tables/{idA}")).StatusCode);
        Assert.Equal(HttpStatusCode.NotFound, (await admin.DeleteAsync($"/api/v1/admin/tables/{idA}")).StatusCode);
        mine = await customer.GetFromJsonAsync<JsonElement>($"/api/v1/orders/{orderId}");
        Assert.Equal(a, mine.GetProperty("tableLabel").GetString());
        await using (var scope = factory.Services.CreateAsyncScope())
        {
            var db = scope.ServiceProvider.GetRequiredService<ApplicationDbContext>();
            Assert.Null((await db.Orders.SingleAsync(o => o.Id == orderId)).DiningTableId);
        }
    }

    [Fact]
    public async Task TableOrdering_SwitchedOff_HidesTables_AndOrdersArePickupOrders()
    {
        var super = factory.CreateClient();
        super.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer",
            await ApiHelpers.CreateAdminTokenAsync(factory.Services, super,
                username: "platform-owner", password: "PlatformOwner@123", role: AdminRole.SuperAdmin));
        var admin = await AdminAsync();
        var label = Unique("Off");
        var created = await (await admin.PostAsJsonAsync("/api/v1/admin/tables", new { locationId = LocationId, labels = new[] { label } }))
            .Content.ReadFromJsonAsync<JsonElement>();
        var code = created[0].GetProperty("code").GetString()!;

        var itemId = await MenuItemAsync();
        var customer = factory.CreateClient();
        var (token, _) = await ApiHelpers.CreateCustomerAsync(customer, "+77002990702");
        ApiHelpers.Authorize(customer, token);

        try
        {
            await super.PutAsJsonAsync($"/api/v1/platform/modules/{ModuleKeys.TableOrdering}", License(false));

            var blocked = await factory.CreateClient().GetAsync($"/api/v1/tables/{code}");
            Assert.Equal(HttpStatusCode.Forbidden, blocked.StatusCode);
            Assert.Equal(ModuleKeys.TableOrdering, (await blocked.Content.ReadFromJsonAsync<JsonElement>()).GetProperty("module").GetString());
            Assert.Equal(HttpStatusCode.Forbidden, (await admin.GetAsync($"/api/v1/admin/tables?locationId={LocationId}")).StatusCode);

            var placed = await customer.PostAsJsonAsync("/api/v1/orders", new
            {
                locationId = LocationId, paymentMethod = "Cash", tableCode = code,
                items = new[] { new { menuItemId = itemId, quantity = 1, toppings = Array.Empty<object>() } },
            });
            Assert.Equal(HttpStatusCode.Created, placed.StatusCode);
            Assert.False(HasTable(await placed.Content.ReadFromJsonAsync<JsonElement>()));
        }
        finally
        {
            await super.PutAsJsonAsync($"/api/v1/platform/modules/{ModuleKeys.TableOrdering}", License(true));
        }
    }
}
