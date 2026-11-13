using System.Net;
using System.Net.Http.Json;
using System.Text.Json;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Yurt.Application.Features.Approvals;
using Yurt.Domain.Enums;
using Yurt.Infrastructure.Persistence;
using Yurt.IntegrationTests.Helpers;

namespace Yurt.IntegrationTests.Tests;

/// <summary>The manager PIN gate: cancels, refunds and discounts need a manager's PIN and a reason (module own_pos).</summary>
[Collection("Integration")]
public class ManagerPinTests(YurtWebAppFactory factory)
{
    private static readonly Guid LocationId = Guid.Parse("11111111-0000-0000-0000-000000000001");
    private const string Sales = "/api/v1/admin/counter-sales";

    private async Task<HttpClient> ClientAsync(string username, string password, AdminRole role)
    {
        var client = factory.CreateClient();
        ApiHelpers.Authorize(client, await ApiHelpers.CreateAdminTokenAsync(factory.Services, client, username, password, role: role));
        return client;
    }

    // A cashier (Worker) who is not allowed to approve anything, and the cafe's pin-manager.
    private async Task<HttpClient> CashierAsync()
    {
        ManagerApprovalService.ResetFailures();
        await ApiHelpers.EnsureManagerAsync(factory.Services);
        return await ClientAsync($"cashier-{Guid.NewGuid():N}"[..16], "Cashier@12345", AdminRole.Worker);
    }

    private async Task<Guid> MenuItemAsync()
    {
        await using var scope = factory.Services.CreateAsyncScope();
        var db = scope.ServiceProvider.GetRequiredService<ApplicationDbContext>();
        var category = await db.MenuCategories.FirstAsync();
        var item = new Yurt.Domain.Entities.MenuItem { CategoryId = category.Id, Name = $"Pin {Guid.NewGuid():N}"[..20], Description = "t", Price = 100m, IsAvailable = true };
        db.MenuItems.Add(item);
        await db.SaveChangesAsync();
        return item.Id;
    }

    private async Task<JsonElement> SellAsync(HttpClient till, Guid item, object? discount = null, HttpStatusCode expect = HttpStatusCode.Created)
    {
        var body = new Dictionary<string, object?>
        {
            ["locationId"] = LocationId, ["paymentMethod"] = "Cash", ["idempotencyKey"] = Guid.NewGuid().ToString(),
            ["items"] = new[] { new { menuItemId = item, quantity = 2 } },
        };
        if (discount is not null) body["discount"] = discount;
        var res = await till.PostAsJsonAsync(Sales, body);
        Assert.Equal(expect, res.StatusCode);
        return await res.Content.ReadFromJsonAsync<JsonElement>();
    }

    private async Task<string?> AuditAsync(string action, string entityId)
    {
        await using var scope = factory.Services.CreateAsyncScope();
        var db = scope.ServiceProvider.GetRequiredService<ApplicationDbContext>();
        return (await db.AuditLogs.IgnoreQueryFilters().Where(a => a.Action == action && a.EntityId == entityId).OrderByDescending(a => a.CreatedAt).FirstOrDefaultAsync())?.Details;
    }

    [Fact]
    public async Task Discount_AtTheTill_NeedsAReasonAndAManagerPin_AndIsAudited()
    {
        var till = await CashierAsync();
        var item = await MenuItemAsync();

        // No reason, no PIN, wrong PIN: nothing is sold.
        await SellAsync(till, item, new { percent = 10m, pin = ApiHelpers.ManagerPin }, HttpStatusCode.BadRequest);
        await SellAsync(till, item, new { percent = 10m, reason = "Regular guest" }, HttpStatusCode.UnprocessableEntity);
        await SellAsync(till, item, new { percent = 10m, pin = "0000", reason = "Regular guest" }, HttpStatusCode.UnprocessableEntity);

        // A manager's PIN takes 10% off 200.
        var sale = await SellAsync(till, item, new { percent = 10m, pin = ApiHelpers.ManagerPin, reason = "Regular guest" });
        Assert.Equal(200m, sale.GetProperty("subtotal").GetDecimal());
        Assert.Equal(20m, sale.GetProperty("discountAmount").GetDecimal());
        Assert.Equal(180m, sale.GetProperty("total").GetDecimal());

        var details = await AuditAsync("CounterSale", sale.GetProperty("id").GetString()!);
        Assert.Contains("Regular guest", details);
        Assert.Contains("approved by pin-manager", details);

        // A fixed amount works too, but not more than the bill.
        var fixedOff = await SellAsync(till, item, new { amount = 50m, pin = ApiHelpers.ManagerPin, reason = "Wrong order" });
        Assert.Equal(150m, fixedOff.GetProperty("total").GetDecimal());
        await SellAsync(till, item, new { amount = 500m, pin = ApiHelpers.ManagerPin, reason = "Too much" }, HttpStatusCode.BadRequest);
    }

    [Fact]
    public async Task ResendingADiscountedSale_NeverAsksForThePinAgain()
    {
        var till = await CashierAsync();
        var item = await MenuItemAsync();
        var body = new
        {
            locationId = LocationId, paymentMethod = "Cash", idempotencyKey = Guid.NewGuid().ToString(),
            items = new[] { new { menuItemId = item, quantity = 1 } },
            discount = new { percent = 50m, pin = ApiHelpers.ManagerPin, reason = "Birthday" },
        };
        Assert.Equal(HttpStatusCode.Created, (await till.PostAsJsonAsync(Sales, body)).StatusCode);
        var again = await till.PostAsJsonAsync(Sales, new { body.locationId, body.paymentMethod, body.idempotencyKey, body.items });
        Assert.Equal(HttpStatusCode.OK, again.StatusCode);
    }

    [Fact]
    public async Task CancellingASale_NeedsAReasonAndAManagerPin_MarksItRefunded_AndIsAudited()
    {
        var till = await CashierAsync();
        var sale = await SellAsync(till, await MenuItemAsync());
        var id = sale.GetProperty("id").GetGuid();
        var url = $"/api/v1/admin/orders/{id}/cancel";

        Assert.Equal(HttpStatusCode.BadRequest, (await till.PostAsJsonAsync(url, new { pin = ApiHelpers.ManagerPin })).StatusCode);
        Assert.Equal(HttpStatusCode.UnprocessableEntity, (await till.PostAsJsonAsync(url, new { reason = "Mistake" })).StatusCode);
        Assert.Equal(HttpStatusCode.UnprocessableEntity, (await till.PostAsJsonAsync(url, new { pin = "9999", reason = "Mistake" })).StatusCode);

        var res = await till.PostAsJsonAsync(url, new { pin = ApiHelpers.ManagerPin, reason = "Rang up twice" });
        Assert.Equal(HttpStatusCode.OK, res.StatusCode);
        var cancelled = await res.Content.ReadFromJsonAsync<JsonElement>();
        Assert.Equal("Declined", cancelled.GetProperty("status").GetString());
        Assert.Equal("Refunded", cancelled.GetProperty("paymentStatus").GetString());
        Assert.Equal("Rang up twice", cancelled.GetProperty("declineReason").GetString());

        var details = await AuditAsync("OrderCancelled", id.ToString());
        Assert.Contains("Rang up twice", details);
        Assert.Contains("approved by pin-manager", details);

        // Cancelling twice is refused.
        Assert.Equal(HttpStatusCode.UnprocessableEntity, (await till.PostAsJsonAsync(url, new { pin = ApiHelpers.ManagerPin, reason = "again" })).StatusCode);
    }

    [Fact]
    public async Task DiscountOnAnOpenOrder_NeedsPin_OneOnly_AndNotOnceFinished()
    {
        var till = await CashierAsync();
        var sale = await SellAsync(till, await MenuItemAsync());
        var id = sale.GetProperty("id").GetGuid();
        var url = $"/api/v1/admin/orders/{id}/discount";

        Assert.Equal(HttpStatusCode.UnprocessableEntity, (await till.PostAsJsonAsync(url, new { percent = 10m, reason = "Slow service" })).StatusCode);
        var ok = await till.PostAsJsonAsync(url, new { percent = 10m, pin = ApiHelpers.ManagerPin, reason = "Slow service" });
        Assert.Equal(HttpStatusCode.OK, ok.StatusCode);
        Assert.Equal(180m, (await ok.Content.ReadFromJsonAsync<JsonElement>()).GetProperty("total").GetDecimal());
        Assert.Contains("Slow service", await AuditAsync("OrderDiscount", id.ToString()));

        // One discount per order.
        Assert.Equal(HttpStatusCode.UnprocessableEntity, (await till.PostAsJsonAsync(url, new { percent = 5m, pin = ApiHelpers.ManagerPin, reason = "again" })).StatusCode);
    }

    [Fact]
    public async Task TakingBackPaid_NeedsAManagerPin_ButMarkingPaidDoesNot()
    {
        var till = await CashierAsync();
        var sale = await SellAsync(till, await MenuItemAsync());
        var url = $"/api/v1/admin/orders/{sale.GetProperty("id").GetGuid()}/payment";

        Assert.Equal(HttpStatusCode.BadRequest, (await till.PostAsJsonAsync(url, new { paymentStatus = "Unpaid" })).StatusCode);
        Assert.Equal(HttpStatusCode.UnprocessableEntity, (await till.PostAsJsonAsync(url, new { paymentStatus = "Unpaid", reason = "Guest left" })).StatusCode);
        var undone = await till.PostAsJsonAsync(url, new { paymentStatus = "Unpaid", pin = ApiHelpers.ManagerPin, reason = "Guest left" });
        Assert.Equal(HttpStatusCode.OK, undone.StatusCode);
        // Marking it paid again is the normal cash-desk move.
        Assert.Equal(HttpStatusCode.OK, (await till.PostAsJsonAsync(url, new { paymentStatus = "Paid" })).StatusCode);
    }

    [Fact]
    public async Task TooManyWrongPins_LockTheCashierOutForAWhile()
    {
        var till = await CashierAsync();
        var item = await MenuItemAsync();
        for (var i = 0; i < ManagerApprovalService.MaxFailures; i++)
            await SellAsync(till, item, new { percent = 10m, pin = "1111", reason = "guess" }, HttpStatusCode.UnprocessableEntity);

        // Even the right PIN is refused now.
        await SellAsync(till, item, new { percent = 10m, pin = ApiHelpers.ManagerPin, reason = "guess" }, (HttpStatusCode)429);
        ManagerApprovalService.ResetFailures();
        await SellAsync(till, item, new { percent = 10m, pin = ApiHelpers.ManagerPin, reason = "later" });
    }

    [Fact]
    public async Task SettingAPin_IsForAdminsAndCeos_NeedsThePassword_AndEachPinIsUnique()
    {
        var id = Guid.NewGuid().ToString("N")[..8];
        var worker = await ClientAsync($"w-{id}", "Worker@12345", AdminRole.Worker);
        var admin = await ClientAsync($"a-{id}", "Admin@12345", AdminRole.Admin);
        var ceo = await ClientAsync($"c-{id}", "Ceo@123456", AdminRole.Ceo);
        const string url = "/api/v1/admin/manager-pin";
        var pin = Random.Shared.Next(100000, 999999).ToString();

        Assert.Equal(HttpStatusCode.Forbidden, (await worker.PutAsJsonAsync(url, new { currentPassword = "Worker@12345", pin })).StatusCode);
        Assert.Equal(HttpStatusCode.BadRequest, (await admin.PutAsJsonAsync(url, new { currentPassword = "Admin@12345", pin = "12" })).StatusCode);
        Assert.Equal(HttpStatusCode.BadRequest, (await admin.PutAsJsonAsync(url, new { currentPassword = "Admin@12345", pin = "12ab" })).StatusCode);
        Assert.Equal(HttpStatusCode.BadRequest, (await admin.PutAsJsonAsync(url, new { currentPassword = "wrong", pin })).StatusCode);

        Assert.Equal(HttpStatusCode.NoContent, (await admin.PutAsJsonAsync(url, new { currentPassword = "Admin@12345", pin })).StatusCode);
        Assert.Equal(HttpStatusCode.Conflict, (await ceo.PutAsJsonAsync(url, new { currentPassword = "Ceo@123456", pin })).StatusCode);
        // Changing your own PIN to the same number is fine.
        Assert.Equal(HttpStatusCode.NoContent, (await admin.PutAsJsonAsync(url, new { currentPassword = "Admin@12345", pin })).StatusCode);

        var status = await admin.GetFromJsonAsync<JsonElement>(url);
        Assert.True(status.GetProperty("required").GetBoolean());
        Assert.True(status.GetProperty("iHavePin").GetBoolean());
        Assert.False((await ceo.GetFromJsonAsync<JsonElement>(url)).GetProperty("iHavePin").GetBoolean());
        Assert.False((await worker.GetFromJsonAsync<JsonElement>(url)).GetProperty("iHavePin").GetBoolean());
    }
}
