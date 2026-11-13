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

/// <summary>Tips: added at the till, on top of the bill; any change afterwards needs a manager PIN; reported per shift and staff.</summary>
[Collection("Integration")]
public class TipTests(YurtWebAppFactory factory)
{
    private static readonly Guid LocationId = Guid.Parse("11111111-0000-0000-0000-000000000001");
    private const string Sales = "/api/v1/admin/counter-sales";

    private async Task<HttpClient> ClientAsync(string username, AdminRole role)
    {
        var client = factory.CreateClient();
        ApiHelpers.Authorize(client, await ApiHelpers.CreateAdminTokenAsync(factory.Services, client, username, "Tipper@12345", role: role));
        return client;
    }

    private async Task<HttpClient> CashierAsync()
    {
        ManagerApprovalService.ResetFailures();
        await ApiHelpers.EnsureManagerAsync(factory.Services);
        return await ClientAsync($"tipper-{Guid.NewGuid():N}"[..16], AdminRole.Worker);
    }

    private async Task<Guid> MenuItemAsync()
    {
        await using var scope = factory.Services.CreateAsyncScope();
        var db = scope.ServiceProvider.GetRequiredService<ApplicationDbContext>();
        var item = new Yurt.Domain.Entities.MenuItem { CategoryId = (await db.MenuCategories.FirstAsync()).Id, Name = $"Tip {Guid.NewGuid():N}"[..20], Description = "t", Price = 100m, IsAvailable = true };
        db.MenuItems.Add(item);
        await db.SaveChangesAsync();
        return item.Id;
    }

    private static async Task<JsonElement> SellAsync(HttpClient till, Guid item, decimal? tip, string method = "Card", string? tipMethod = null,
        bool handedOver = true, HttpStatusCode expect = HttpStatusCode.Created)
    {
        var body = new Dictionary<string, object?>
        {
            ["locationId"] = LocationId, ["paymentMethod"] = method, ["idempotencyKey"] = Guid.NewGuid().ToString(), ["handedOver"] = handedOver,
            ["items"] = new[] { new { menuItemId = item, quantity = 1 } }, ["tip"] = tip, ["tipMethod"] = tipMethod,
        };
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
    public async Task TipAtTheTill_IsOnTopOfTheBill_AndNeedsNoPin()
    {
        var till = await CashierAsync();
        var item = await MenuItemAsync();

        var sale = await SellAsync(till, item, 15m);
        Assert.Equal(100m, sale.GetProperty("total").GetDecimal());
        Assert.Equal(15m, sale.GetProperty("tipAmount").GetDecimal());
        Assert.Contains("tip 15", await AuditAsync("CounterSale", sale.GetProperty("id").GetString()!));

        // No tip, a negative tip, more than the bill and a method that can't take a tip: no tip / no sale.
        Assert.Equal(0m, (await SellAsync(till, item, null)).GetProperty("tipAmount").GetDecimal());
        await SellAsync(till, item, -1m, expect: HttpStatusCode.BadRequest);
        await SellAsync(till, item, 101m, expect: HttpStatusCode.BadRequest);
        await SellAsync(till, item, 5m, tipMethod: "Other", expect: HttpStatusCode.BadRequest);
    }

    [Fact]
    public async Task ChangingOrRemovingATip_NeedsAReasonAndAManagerPin_AndIsAudited()
    {
        var till = await CashierAsync();
        var sale = await SellAsync(till, await MenuItemAsync(), 10m);
        var id = sale.GetProperty("id").GetGuid();
        var url = $"/api/v1/admin/orders/{id}/tip";

        Assert.Equal(HttpStatusCode.BadRequest, (await till.PutAsJsonAsync(url, new { amount = 20m, pin = ApiHelpers.ManagerPin })).StatusCode);
        Assert.Equal(HttpStatusCode.UnprocessableEntity, (await till.PutAsJsonAsync(url, new { amount = 20m, reason = "Guest added" })).StatusCode);
        Assert.Equal(HttpStatusCode.UnprocessableEntity, (await till.PutAsJsonAsync(url, new { amount = 20m, pin = "0000", reason = "Guest added" })).StatusCode);
        Assert.Equal(HttpStatusCode.BadRequest, (await till.PutAsJsonAsync(url, new { amount = 500m, pin = ApiHelpers.ManagerPin, reason = "Too big" })).StatusCode);

        var changed = await till.PutAsJsonAsync(url, new { amount = 20m, pin = ApiHelpers.ManagerPin, reason = "Guest added" });
        Assert.Equal(HttpStatusCode.OK, changed.StatusCode);
        var order = await changed.Content.ReadFromJsonAsync<JsonElement>();
        Assert.Equal(20m, order.GetProperty("tipAmount").GetDecimal());
        Assert.Equal(100m, order.GetProperty("total").GetDecimal());
        var details = await AuditAsync("OrderTipChanged", id.ToString());
        Assert.Contains("10 → 20", details);
        Assert.Contains("approved by pin-manager", details);

        // Removing it needs the PIN as well.
        Assert.Equal(HttpStatusCode.UnprocessableEntity, (await till.PutAsJsonAsync(url, new { amount = 0m, reason = "Mistake" })).StatusCode);
        var removed = await till.PutAsJsonAsync(url, new { amount = 0m, pin = ApiHelpers.ManagerPin, reason = "Mistake" });
        Assert.Equal(0m, (await removed.Content.ReadFromJsonAsync<JsonElement>()).GetProperty("tipAmount").GetDecimal());
        Assert.Contains("Mistake", await AuditAsync("OrderTipRemoved", id.ToString()));

        // Saying the same thing again is refused.
        Assert.Equal(HttpStatusCode.UnprocessableEntity, (await till.PutAsJsonAsync(url, new { amount = 0m, pin = ApiHelpers.ManagerPin, reason = "again" })).StatusCode);
    }

    [Fact]
    public async Task CashierShiftReport_ShowsTips_AndCashTipsAreInTheDrawer()
    {
        var till = await CashierAsync();
        var current = await till.GetAsync($"/api/v1/admin/shifts/current?locationId={LocationId}");
        var text = await current.Content.ReadAsStringAsync();
        if (text.StartsWith('{'))
            (await till.PostAsJsonAsync($"/api/v1/admin/shifts/{JsonDocument.Parse(text).RootElement.GetProperty("id").GetGuid()}/close", new { countedCash = 0m })).EnsureSuccessStatusCode();
        var shift = await (await till.PostAsJsonAsync("/api/v1/admin/shifts/open", new { locationId = LocationId, openingCash = 50m })).Content.ReadFromJsonAsync<JsonElement>();
        var shiftId = shift.GetProperty("id").GetGuid();

        var item = await MenuItemAsync();
        await SellAsync(till, item, 10m, method: "Cash");                    // cash bill, cash tip
        await SellAsync(till, item, 7m, method: "Card");                     // card bill, card tip
        await SellAsync(till, item, 3m, method: "Card", tipMethod: "Cash");  // card bill, tip given in cash

        var report = await till.GetFromJsonAsync<JsonElement>($"/api/v1/admin/shifts/{shiftId}/report");
        Assert.Equal(20m, report.GetProperty("tips").GetDecimal());
        Assert.Equal(13m, report.GetProperty("cashTips").GetDecimal());
        // 50 opening + 100 cash sale + 13 cash tips.
        Assert.Equal(163m, report.GetProperty("expectedCash").GetDecimal());
        Assert.Equal(300m, report.GetProperty("takings").GetDecimal());
        var staff = report.GetProperty("tipsByStaff");
        Assert.Equal(1, staff.GetArrayLength());
        Assert.Equal(20m, staff[0].GetProperty("total").GetDecimal());
        Assert.Equal(7m, staff[0].GetProperty("nonCash").GetDecimal());

        // The cafe-wide tip report is for the CEO only.
        Assert.Equal(HttpStatusCode.Forbidden, (await till.GetAsync("/api/v1/admin/tips?from=2020-01-01&to=2099-01-01")).StatusCode);
        var ceo = await ClientAsync($"ceo-tips-{Guid.NewGuid():N}"[..16], AdminRole.Ceo);
        var day = DateTime.UtcNow.ToString("yyyy-MM-dd");
        var tips = await ceo.GetFromJsonAsync<JsonElement>($"/api/v1/admin/tips?from={day}&to={day}&locationId={LocationId}");
        Assert.True(tips.GetProperty("total").GetDecimal() >= 20m);
        Assert.Contains(tips.GetProperty("byShift").EnumerateArray(), s => s.GetProperty("shiftId").GetGuid() == shiftId && s.GetProperty("total").GetDecimal() == 20m);
        Assert.Equal(HttpStatusCode.BadRequest, (await ceo.GetAsync("/api/v1/admin/tips?from=nope&to=nope")).StatusCode);
    }
}
