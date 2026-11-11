using System.Net;
using System.Net.Http.Json;
using System.Text.Json;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Yurt.Application.Common.Interfaces;
using Yurt.Domain.Enums;
using Yurt.IntegrationTests.Helpers;

namespace Yurt.IntegrationTests.Tests;

/// <summary>Monthly pay: fixed salary + bonuses − deductions, closed months stay as they were, CEO only.</summary>
[Collection("Integration")]
public class PayrollTests(YurtWebAppFactory factory)
{
    private async Task<HttpClient> ClientAsync(string username, string password, AdminRole role)
    {
        var client = factory.CreateClient();
        ApiHelpers.Authorize(client, await ApiHelpers.CreateAdminTokenAsync(factory.Services, client, username, password, role: role));
        return client;
    }

    private async Task<Guid> UserIdAsync(string username)
    {
        await using var scope = factory.Services.CreateAsyncScope();
        var db = scope.ServiceProvider.GetRequiredService<IApplicationDbContext>();
        return await db.AdminUsers.IgnoreQueryFilters().Where(u => u.Username == username).Select(u => u.Id).SingleAsync();
    }

    private static string Month => $"{2100 + Random.Shared.Next(0, 800)}-{Random.Shared.Next(1, 13):00}";

    [Fact]
    public async Task OnlyTheCeo_SeesPayroll()
    {
        var id = Guid.NewGuid().ToString("N")[..8];
        var admin = await ClientAsync($"adm-{id}", "Admin@12345", AdminRole.Admin);
        var worker = await ClientAsync($"wrk-{id}", "Worker@12345", AdminRole.Worker);
        var op = await ClientAsync($"op-{id}", "OperatorPass@12345", AdminRole.SuperAdmin);
        var ceo = await ClientAsync($"ceo-{id}", "Ceo@123456", AdminRole.Ceo);

        foreach (var c in new[] { admin, worker, op })
            Assert.Equal(HttpStatusCode.Forbidden, (await c.GetAsync("/api/v1/admin/payroll/staff")).StatusCode);
        Assert.Equal(HttpStatusCode.OK, (await ceo.GetAsync("/api/v1/admin/payroll/staff")).StatusCode);
    }

    [Fact]
    public async Task Sheet_AddsSalaryAndBonus_TakesOffDeduction_AndClosedMonthKeepsItsAmounts()
    {
        var id = Guid.NewGuid().ToString("N")[..8];
        var ceo = await ClientAsync($"ceo-{id}", "Ceo@123456", AdminRole.Ceo);
        await ClientAsync($"wrk-{id}", "Worker@12345", AdminRole.Worker);
        var worker = await UserIdAsync($"wrk-{id}");
        var month = Month;

        Assert.Equal(HttpStatusCode.OK,
            (await ceo.PutAsJsonAsync($"/api/v1/admin/payroll/staff/{worker}/salary", new { monthlySalary = 200000 })).StatusCode);
        Assert.Equal(HttpStatusCode.Created,
            (await ceo.PostAsJsonAsync("/api/v1/admin/payroll/adjustments", new { adminUserId = worker, month, kind = "bonus", amount = 15000, reason = "Busy weekend" })).StatusCode);
        Assert.Equal(HttpStatusCode.Created,
            (await ceo.PostAsJsonAsync("/api/v1/admin/payroll/adjustments", new { adminUserId = worker, month, kind = "deduction", amount = 50000, reason = "Advance" })).StatusCode);

        var sheet = await ceo.GetFromJsonAsync<JsonElement>($"/api/v1/admin/payroll/months/{month}");
        var line = sheet.GetProperty("lines").EnumerateArray().Single(l => l.GetProperty("adminUserId").GetGuid() == worker);
        Assert.Equal(165000m, line.GetProperty("total").GetDecimal());
        Assert.False(sheet.GetProperty("closed").GetBoolean());

        Assert.Equal(HttpStatusCode.OK, (await ceo.PostAsync($"/api/v1/admin/payroll/months/{month}/close", null)).StatusCode);

        // A raise after closing does not change the closed month, and the closed month cannot be edited.
        await ceo.PutAsJsonAsync($"/api/v1/admin/payroll/staff/{worker}/salary", new { monthlySalary = 300000 });
        var closed = await ceo.GetFromJsonAsync<JsonElement>($"/api/v1/admin/payroll/months/{month}");
        Assert.True(closed.GetProperty("closed").GetBoolean());
        Assert.Equal(165000m, closed.GetProperty("lines").EnumerateArray().Single(l => l.GetProperty("adminUserId").GetGuid() == worker).GetProperty("total").GetDecimal());
        Assert.Equal(HttpStatusCode.Conflict,
            (await ceo.PostAsJsonAsync("/api/v1/admin/payroll/adjustments", new { adminUserId = worker, month, kind = "bonus", amount = 1, reason = "late" })).StatusCode);
        Assert.Equal(HttpStatusCode.Conflict, (await ceo.PostAsync($"/api/v1/admin/payroll/months/{month}/close", null)).StatusCode);

        // Reopened, the sheet follows the new salary again.
        Assert.Equal(HttpStatusCode.OK, (await ceo.PostAsync($"/api/v1/admin/payroll/months/{month}/reopen", null)).StatusCode);
        var open = await ceo.GetFromJsonAsync<JsonElement>($"/api/v1/admin/payroll/months/{month}");
        Assert.Equal(265000m, open.GetProperty("lines").EnumerateArray().Single(l => l.GetProperty("adminUserId").GetGuid() == worker).GetProperty("total").GetDecimal());
    }

    [Theory]
    [InlineData("bonus", 0, "x")]
    [InlineData("bonus", -5, "x")]
    [InlineData("gift", 10, "x")]
    [InlineData("bonus", 10, " ")]
    public async Task BadAdjustments_AreRefused(string kind, decimal amount, string reason)
    {
        var id = Guid.NewGuid().ToString("N")[..8];
        var ceo = await ClientAsync($"ceo-{id}", "Ceo@123456", AdminRole.Ceo);
        var me = await UserIdAsync($"ceo-{id}");
        var r = await ceo.PostAsJsonAsync("/api/v1/admin/payroll/adjustments", new { adminUserId = me, month = Month, kind, amount, reason });
        Assert.Equal(HttpStatusCode.UnprocessableEntity, r.StatusCode);
        Assert.Equal(HttpStatusCode.UnprocessableEntity, (await ceo.GetAsync("/api/v1/admin/payroll/months/2026-13")).StatusCode);
    }
}
