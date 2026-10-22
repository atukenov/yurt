using System.Net;
using System.Net.Http.Headers;
using System.Net.Http.Json;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Yurt.Application.Common;
using Yurt.Application.Common.Interfaces;
using Yurt.Domain.Entities;
using Yurt.Domain.Enums;
using Yurt.Infrastructure.Persistence;
using Yurt.IntegrationTests.Helpers;

namespace Yurt.IntegrationTests.Tests;

/// <summary>
/// Phase 5 data-isolation check: with two cafes in one database, one cafe's staff can neither read, change nor link to
/// the other cafe's rows, including the Phase 6 tables (stock, recipes, stations, printers, print jobs, shifts).
/// </summary>
[Collection("Integration")]
public class CafeDataIsolationTests(YurtWebAppFactory factory)
{
    private sealed record OtherCafe(
        Guid TenantId, Guid Location, Guid Category, Guid Item, Guid Topping, Guid Ingredient, Guid Station,
        Guid Printer, Guid PrintJob, Guid Shift, Guid DiscountCode, Guid Promotion, Guid Customer, Guid Order,
        Guid Report, Guid Worker, string Name);

    private ApplicationDbContext DbFor(Guid tenantId, out IServiceScope scope)
    {
        scope = factory.Services.CreateScope();
        scope.ServiceProvider.GetRequiredService<ITenantContext>().Set(tenantId, string.Empty);
        return scope.ServiceProvider.GetRequiredService<ApplicationDbContext>();
    }

    /// <summary>A second cafe with one row in each table an admin screen can touch.</summary>
    private async Task<OtherCafe> SeedOtherCafeAsync()
    {
        var slug = $"iso-{Guid.NewGuid():N}"[..12];
        var name = $"Other {Guid.NewGuid():N}"[..20];
        using (var scope = factory.Services.CreateScope())
        {
            var db = scope.ServiceProvider.GetRequiredService<ApplicationDbContext>();
            db.Tenants.Add(new Tenant { Slug = slug, Name = slug });
            await db.SaveChangesAsync();
        }
        factory.Services.GetRequiredService<ITenantDirectory>().Invalidate();
        Guid tenantId;
        using (var scope = factory.Services.CreateScope())
            tenantId = (await scope.ServiceProvider.GetRequiredService<ApplicationDbContext>().Tenants.SingleAsync(t => t.Slug == slug)).Id;

        using var dbB = DbFor(tenantId, out var scopeB);
        using var _ = scopeB;
        var location = new Location { Name = name, Address = "x", WorkingHours = "x", ContactPhone = "x" };
        var category = new MenuCategory { Name = name };
        var item = new MenuItem { Category = category, Name = name, Price = 100 };
        var topping = new MenuTopping { Name = name, Price = 10 };
        var ingredient = new Ingredient { Name = name, Unit = "g" };
        var station = new Station { Name = name };
        var printer = new Printer { Location = location, Name = name, Host = "10.0.0.9" };
        var job = new PrintJob { Printer = printer, Data = [1] };
        var shift = new Shift { Location = location, OpenedBy = "b", OpeningCash = 0 };
        var code = new DiscountCode { Code = $"ISO{Guid.NewGuid():N}"[..12].ToUpperInvariant(), Title = name, DiscountType = DiscountType.Percentage, DiscountValue = 10 };
        var promo = new Promotion { Title = name, Description = "d" };
        var customer = new CustomerUser { MobileNumber = $"+7{Random.Shared.NextInt64(7000000000, 7999999999)}", PinHash = "x", FirstName = "B", LastName = "B" };
        var order = new Order { CustomerUser = customer, Location = location, Subtotal = 100, Total = 100 };
        var report = new UserReport { Customer = customer, Text = name };
        var worker = new AdminUser { Username = $"iso-{Guid.NewGuid():N}"[..16], PasswordHash = "x", Role = AdminRole.Admin, IsActive = true };
        dbB.AddRange(location, category, item, topping, ingredient, station, printer, job, shift, code, promo, customer, order, report, worker);
        dbB.IngredientStocks.Add(new IngredientStock { Ingredient = ingredient, Location = location, Quantity = 5 });
        dbB.RecipeLines.Add(new RecipeLine { MenuItem = item, Ingredient = ingredient, Quantity = 1 });
        dbB.MenuItemStocks.Add(new MenuItemStock { MenuItem = item, Location = location, Quantity = 3 });
        await dbB.SaveChangesAsync();

        return new OtherCafe(tenantId, location.Id, category.Id, item.Id, topping.Id, ingredient.Id, station.Id, printer.Id,
            job.Id, shift.Id, code.Id, promo.Id, customer.Id, order.Id, report.Id, worker.Id, name);
    }

    private async Task<HttpClient> AdminOfFirstCafeAsync()
    {
        var client = factory.CreateClient();
        client.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer",
            await ApiHelpers.CreateAdminTokenAsync(factory.Services, client, username: $"iso-adm-{Guid.NewGuid():N}"[..20]));
        return client;
    }

    [Fact]
    public async Task AdminOfOneCafe_CannotReadOrChange_AnotherCafesRows_ByTheirIds()
    {
        var b = await SeedOtherCafeAsync();
        var admin = await AdminOfFirstCafeAsync();
        var item = new
        {
            categoryId = b.Category, name = "Hacked", price = 1m, isAvailable = false, description = "x"
        };

        var calls = new (HttpMethod Method, string Url, object? Body)[]
        {
            (HttpMethod.Get, $"/api/v1/admin/locations/{b.Location}", null),
            (HttpMethod.Put, $"/api/v1/admin/locations/{b.Location}", new { name = "Hacked", address = "x", workingHours = "x", contactPhone = "x", isActive = false }),
            (HttpMethod.Delete, $"/api/v1/admin/locations/{b.Location}", null),
            (HttpMethod.Put, $"/api/v1/admin/menu/categories/{b.Category}", new { name = "Hacked", sortOrder = 0 }),
            (HttpMethod.Delete, $"/api/v1/admin/menu/categories/{b.Category}", null),
            (HttpMethod.Get, $"/api/v1/admin/menu/items/{b.Item}", null),
            (HttpMethod.Put, $"/api/v1/admin/menu/items/{b.Item}", item),
            (HttpMethod.Delete, $"/api/v1/admin/menu/items/{b.Item}", null),
            (HttpMethod.Put, $"/api/v1/admin/menu/toppings/{b.Topping}", new { name = "Hacked", price = 1m, isAvailable = false }),
            (HttpMethod.Delete, $"/api/v1/admin/menu/toppings/{b.Topping}", null),
            (HttpMethod.Put, $"/api/v1/admin/ingredients/{b.Ingredient}", new { name = "Hacked", unit = "g" }),
            (HttpMethod.Delete, $"/api/v1/admin/ingredients/{b.Ingredient}", null),
            (HttpMethod.Put, $"/api/v1/admin/ingredients/{b.Ingredient}/stock/{b.Location}", new { quantity = 999m }),
            (HttpMethod.Get, $"/api/v1/admin/ingredients/recipes/{b.Item}", null),
            (HttpMethod.Put, $"/api/v1/admin/ingredients/recipes/{b.Item}", new { lines = Array.Empty<object>() }),
            (HttpMethod.Get, $"/api/v1/admin/ingredients/toppings/{b.Topping}", null),
            (HttpMethod.Put, $"/api/v1/admin/stock/{b.Item}/{b.Location}", new { quantity = 999 }),
            (HttpMethod.Delete, $"/api/v1/admin/stations/{b.Station}", null),
            (HttpMethod.Put, $"/api/v1/admin/stations/categories/{b.Category}", new { stationId = (Guid?)null }),
            (HttpMethod.Put, $"/api/v1/admin/printers/{b.Printer}", new { locationId = b.Location, name = "Hacked", kind = "Receipt", host = "10.0.0.1", port = 9100, lineWidth = 48, codeTable = 17, language = "ru" }),
            (HttpMethod.Delete, $"/api/v1/admin/printers/{b.Printer}", null),
            (HttpMethod.Post, $"/api/v1/admin/printers/{b.Printer}/test", null),
            (HttpMethod.Post, $"/api/v1/admin/printers/agents/{b.Location}", null),
            (HttpMethod.Post, $"/api/v1/admin/cash-drawer/{b.Location}/open", null),
            (HttpMethod.Post, $"/api/v1/admin/shifts/{b.Shift}/cash", new { amount = 5m, isOut = true, reason = "x" }),
            (HttpMethod.Post, $"/api/v1/admin/shifts/{b.Shift}/close", new { countedCash = 0m }),
            (HttpMethod.Get, $"/api/v1/admin/shifts/{b.Shift}/report", null),
            (HttpMethod.Put, $"/api/v1/admin/discount-codes/{b.DiscountCode}", new { title = "Hacked", discountType = "Percentage", discountValue = 99m, isActive = false }),
            (HttpMethod.Delete, $"/api/v1/admin/discount-codes/{b.DiscountCode}", null),
            (HttpMethod.Put, $"/api/v1/admin/promotions/{b.Promotion}", new { title = "Hacked", description = "x", isActive = false }),
            (HttpMethod.Delete, $"/api/v1/admin/promotions/{b.Promotion}", null),
            (HttpMethod.Get, $"/api/v1/admin/customers/{b.Customer}", null),
            (HttpMethod.Patch, $"/api/v1/admin/customers/{b.Customer}/active", new { isActive = false }),
            (HttpMethod.Get, $"/api/v1/admin/orders/{b.Order}", null),
            (HttpMethod.Post, $"/api/v1/admin/orders/{b.Order}/accept", new { etaMinutes = 5 }),
            (HttpMethod.Post, $"/api/v1/admin/orders/{b.Order}/decline", new { reason = "x" }),
            (HttpMethod.Post, $"/api/v1/admin/orders/{b.Order}/status", new { status = "Completed" }),
            (HttpMethod.Post, $"/api/v1/admin/orders/{b.Order}/print/receipt", null),
            (HttpMethod.Get, $"/api/v1/admin/fiscal/orders/{b.Order}", null),
            (HttpMethod.Post, $"/api/v1/admin/reports/{b.Report}/resolve", null),
            (HttpMethod.Put, $"/api/v1/admin/workers/{b.Worker}", new { username = "hacked", isActive = false }),
            (HttpMethod.Patch, $"/api/v1/admin/workers/{b.Worker}/active", new { isActive = false }),
            (HttpMethod.Post, $"/api/v1/admin/workers/{b.Worker}/reset-password", new { newPassword = "Hacked@12345" }),
        };

        var leaks = new List<string>();
        foreach (var (method, url, body) in calls)
        {
            using var request = new HttpRequestMessage(method, url) { Content = body is null ? null : JsonContent.Create(body) };
            var response = await admin.SendAsync(request);
            if (response.IsSuccessStatusCode || response.StatusCode == HttpStatusCode.InternalServerError)
                leaks.Add($"{method} {url} -> {(int)response.StatusCode}");
        }
        Assert.True(leaks.Count == 0, "Reached another cafe's row: " + string.Join("; ", leaks));

        // The loyalty card answers "no card" rather than 404, exactly as for an id that exists nowhere.
        Assert.Equal(await admin.GetStringAsync($"/api/v1/admin/customers/{Guid.NewGuid()}/loyalty"),
            await admin.GetStringAsync($"/api/v1/admin/customers/{b.Customer}/loyalty"));

        // Nothing of the other cafe changed.
        using var db = DbFor(b.TenantId, out var scope);
        using var _ = scope;
        Assert.Equal(b.Name, (await db.Locations.SingleAsync(l => l.Id == b.Location)).Name);
        Assert.Equal(b.Name, (await db.MenuCategories.SingleAsync(c => c.Id == b.Category)).Name);
        var stored = await db.MenuItems.SingleAsync(i => i.Id == b.Item);
        Assert.Equal((b.Name, 100m, b.Category), (stored.Name, stored.Price, stored.CategoryId));
        Assert.Equal(b.Name, (await db.MenuToppings.SingleAsync(t => t.Id == b.Topping)).Name);
        Assert.Equal(b.Name, (await db.Ingredients.SingleAsync(i => i.Id == b.Ingredient)).Name);
        Assert.Equal(5m, (await db.IngredientStocks.SingleAsync(s => s.IngredientId == b.Ingredient)).Quantity);
        Assert.Equal(1, await db.RecipeLines.CountAsync(r => r.MenuItemId == b.Item));
        Assert.Equal(3, (await db.MenuItemStocks.SingleAsync(s => s.MenuItemId == b.Item)).Quantity);
        Assert.True(await db.Stations.AnyAsync(s => s.Id == b.Station));
        Assert.Equal(b.Name, (await db.Printers.SingleAsync(p => p.Id == b.Printer)).Name);
        Assert.Equal(0, await db.PrintJobs.CountAsync(j => j.PrinterId == b.Printer && j.Id != b.PrintJob));
        Assert.False(await db.PrintAgents.AnyAsync(a => a.LocationId == b.Location));
        Assert.Null((await db.Shifts.SingleAsync(s => s.Id == b.Shift)).ClosedAt);
        Assert.False(await db.CashMovements.AnyAsync(m => m.ShiftId == b.Shift));
        Assert.True((await db.DiscountCodes.SingleAsync(d => d.Id == b.DiscountCode)).IsActive);
        Assert.Equal(b.Name, (await db.Promotions.SingleAsync(p => p.Id == b.Promotion)).Title);
        Assert.True((await db.CustomerUsers.SingleAsync(c => c.Id == b.Customer)).IsActive);
        Assert.Equal(OrderStatus.Created, (await db.Orders.SingleAsync(o => o.Id == b.Order)).Status);
        Assert.False((await db.UserReports.SingleAsync(r => r.Id == b.Report)).IsResolved);
        var worker = await db.AdminUsers.SingleAsync(u => u.Id == b.Worker);
        Assert.Equal(("x", true), (worker.PasswordHash, worker.IsActive));
    }

    [Fact]
    public async Task AdminOfOneCafe_CannotLinkItsRows_ToAnotherCafesRows()
    {
        var b = await SeedOtherCafeAsync();
        var admin = await AdminOfFirstCafeAsync();

        Guid ownItem, ownCategory, ownLocation;
        using (var db = DbFor(Tenant.DefaultId, out var scope))
        {
            using var _ = scope;
            var category = new MenuCategory { Name = $"Own {Guid.NewGuid():N}"[..20] };
            var item = new MenuItem { Category = category, Name = category.Name, Price = 50 };
            db.AddRange(category, item);
            await db.SaveChangesAsync();
            (ownItem, ownCategory) = (item.Id, category.Id);
            ownLocation = (await db.Locations.FirstAsync()).Id;
        }

        // Move own item into the other cafe's category.
        var move = await admin.PutAsJsonAsync($"/api/v1/admin/menu/items/{ownItem}",
            new { categoryId = b.Category, name = "Own", price = 50m, isAvailable = true, description = "" });
        Assert.False(move.IsSuccessStatusCode);
        // Route own category to the other cafe's station; a printer at the other cafe's branch; a recipe with its ingredient.
        Assert.False((await admin.PutAsJsonAsync($"/api/v1/admin/stations/categories/{ownCategory}", new { stationId = b.Station })).IsSuccessStatusCode);
        Assert.False((await admin.PostAsJsonAsync("/api/v1/admin/printers", new
        {
            locationId = b.Location, name = "Sneaky", kind = "Receipt", host = "10.0.0.2", port = 9100, lineWidth = 48, codeTable = 17, language = "ru"
        })).IsSuccessStatusCode);
        Assert.False((await admin.PutAsJsonAsync($"/api/v1/admin/ingredients/recipes/{ownItem}",
            new { lines = new[] { new { ingredientId = b.Ingredient, quantity = 1m } } })).IsSuccessStatusCode);
        Assert.False((await admin.PutAsJsonAsync($"/api/v1/admin/ingredients/{b.Ingredient}/stock/{ownLocation}", new { quantity = 1m })).IsSuccessStatusCode);

        using var check = DbFor(Tenant.DefaultId, out var checkScope);
        using var __ = checkScope;
        Assert.Equal(ownCategory, (await check.MenuItems.SingleAsync(i => i.Id == ownItem)).CategoryId);
        Assert.Null((await check.MenuCategories.SingleAsync(c => c.Id == ownCategory)).StationId);
        Assert.False(await check.Printers.IgnoreQueryFilters().AnyAsync(p => p.LocationId == b.Location && p.Name == "Sneaky"));
        Assert.False(await check.RecipeLines.IgnoreQueryFilters().AnyAsync(r => r.MenuItemId == ownItem));
    }

    [Fact]
    public async Task Database_RefusesALink_ToAnotherCafesRow_EvenWhenAServiceForgetsToCheck()
    {
        var b = await SeedOtherCafeAsync();

        // Not loaded: the id came straight from a request.
        using (var db = DbFor(Tenant.DefaultId, out var scope))
        {
            using var _ = scope;
            db.MenuItems.Add(new MenuItem { CategoryId = b.Category, Name = "Linked", Price = 1 });
            await Assert.ThrowsAsync<CrossTenantReferenceException>(() => db.SaveChangesAsync());
        }
        using (var db = DbFor(Tenant.DefaultId, out var scope))
        {
            using var _ = scope;
            db.Printers.Add(new Printer { LocationId = b.Location, StationId = b.Station, Name = "Linked", Host = "10.0.0.3" });
            await Assert.ThrowsAsync<CrossTenantReferenceException>(() => db.SaveChangesAsync());
        }
        // Changing a link on an existing row.
        using (var db = DbFor(Tenant.DefaultId, out var scope))
        {
            using var _ = scope;
            var category = await db.MenuCategories.FirstAsync();
            category.StationId = b.Station;
            await Assert.ThrowsAsync<CrossTenantReferenceException>(() => db.SaveChangesAsync());
        }
        // Loaded past the filter on purpose, then linked.
        using (var db = DbFor(Tenant.DefaultId, out var scope))
        {
            using var _ = scope;
            var foreign = await db.Ingredients.IgnoreQueryFilters().SingleAsync(i => i.Id == b.Ingredient);
            var ownItem = await db.MenuItems.FirstAsync();
            db.RecipeLines.Add(new RecipeLine { MenuItemId = ownItem.Id, Ingredient = foreign, Quantity = 1 });
            await Assert.ThrowsAsync<CrossTenantReferenceException>(() => db.SaveChangesAsync());
        }
        // Moving a row to another cafe.
        using (var db = DbFor(Tenant.DefaultId, out var scope))
        {
            using var _ = scope;
            var promo = new Promotion { Title = $"Move {Guid.NewGuid():N}", Description = "d" };
            db.Promotions.Add(promo);
            await db.SaveChangesAsync();
            promo.TenantId = b.TenantId;
            await Assert.ThrowsAsync<CrossTenantReferenceException>(() => db.SaveChangesAsync());
        }
        // Links inside one cafe still save.
        using (var db = DbFor(b.TenantId, out var scope))
        {
            using var _ = scope;
            db.Printers.Add(new Printer { LocationId = b.Location, StationId = b.Station, Name = "Fine", Host = "10.0.0.4" });
            await db.SaveChangesAsync();
        }
    }
}
