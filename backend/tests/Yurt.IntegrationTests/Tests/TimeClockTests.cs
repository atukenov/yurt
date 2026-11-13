using System.Net;
using System.Net.Http.Json;
using System.Text.Json;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Yurt.Application.Common.Interfaces;
using Yurt.Application.Features.TimeClock;
using Yurt.Domain.Enums;
using Yurt.IntegrationTests.Helpers;

namespace Yurt.IntegrationTests.Tests;

/// <summary>Staff PIN: switch the till to another person, clock in and out, worked hours, hours in payroll (module own_pos).</summary>
[Collection("Integration")]
public class TimeClockTests(YurtWebAppFactory factory)
{
    private const string Pin = "/api/v1/admin/staff-pin";
    private const string Clock = "/api/v1/admin/time-clock";

    private async Task<HttpClient> ClientAsync(string username, string password, AdminRole role)
    {
        StaffPinService.ResetFailures();
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

    private static string RandomPin() => Random.Shared.Next(100000, 999999).ToString();

    private static async Task SetPinAsync(HttpClient c, string password, string pin)
        => Assert.Equal(HttpStatusCode.NoContent, (await c.PutAsJsonAsync(Pin, new { currentPassword = password, pin })).StatusCode);

    [Fact]
    public async Task Pin_NeedsThePassword_Is4To6Digits_AndIsUnique()
    {
        var id = Guid.NewGuid().ToString("N")[..8];
        var a = await ClientAsync($"pa-{id}", "Worker@12345", AdminRole.Worker);
        var b = await ClientAsync($"pb-{id}", "Worker@12345", AdminRole.Worker);
        var pin = RandomPin();

        Assert.Equal(HttpStatusCode.BadRequest, (await a.PutAsJsonAsync(Pin, new { currentPassword = "nope", pin })).StatusCode);
        Assert.Equal(HttpStatusCode.BadRequest, (await a.PutAsJsonAsync(Pin, new { currentPassword = "Worker@12345", pin = "12" })).StatusCode);
        Assert.Equal(HttpStatusCode.BadRequest, (await a.PutAsJsonAsync(Pin, new { currentPassword = "Worker@12345", pin = "12ab56" })).StatusCode);
        await SetPinAsync(a, "Worker@12345", pin);
        Assert.Equal(HttpStatusCode.Conflict, (await b.PutAsJsonAsync(Pin, new { currentPassword = "Worker@12345", pin })).StatusCode);
        Assert.True((await a.GetFromJsonAsync<JsonElement>(Pin)).GetProperty("iHavePin").GetBoolean());
        Assert.False((await b.GetFromJsonAsync<JsonElement>(Pin)).GetProperty("iHavePin").GetBoolean());
    }

    [Fact]
    public async Task Pin_CannotBeAManagerPin_AndTheOtherWayRound()
    {
        var id = Guid.NewGuid().ToString("N")[..8];
        await ApiHelpers.EnsureManagerAsync(factory.Services);
        var worker = await ClientAsync($"pw-{id}", "Worker@12345", AdminRole.Worker);
        Assert.Equal(HttpStatusCode.Conflict, (await worker.PutAsJsonAsync(Pin, new { currentPassword = "Worker@12345", pin = ApiHelpers.ManagerPin })).StatusCode);

        var admin = await ClientAsync($"pm-{id}", "Admin@12345", AdminRole.Admin);
        var pin = RandomPin();
        await SetPinAsync(worker, "Worker@12345", pin);
        Assert.Equal(HttpStatusCode.Conflict, (await admin.PutAsJsonAsync("/api/v1/admin/manager-pin", new { currentPassword = "Admin@12345", pin })).StatusCode);
    }

    [Fact]
    public async Task Manager_SetsAndClearsPins_AdminOnlyForWorkers()
    {
        var id = Guid.NewGuid().ToString("N")[..8];
        var admin = await ClientAsync($"ad-{id}", "Admin@12345", AdminRole.Admin);
        var otherAdmin = await ClientAsync($"ao-{id}", "Admin@12345", AdminRole.Admin);
        var worker = await ClientAsync($"wk-{id}", "Worker@12345", AdminRole.Worker);
        var workerId = await UserIdAsync($"wk-{id}");
        var otherAdminId = await UserIdAsync($"ao-{id}");

        Assert.Equal(HttpStatusCode.Forbidden, (await worker.GetAsync(Pin + "/people")).StatusCode);
        Assert.Equal(HttpStatusCode.NoContent, (await admin.PutAsJsonAsync($"{Pin}/{workerId}", new { pin = RandomPin() })).StatusCode);
        Assert.Equal(HttpStatusCode.NotFound, (await admin.PutAsJsonAsync($"{Pin}/{otherAdminId}", new { pin = RandomPin() })).StatusCode);
        var people = await admin.GetFromJsonAsync<JsonElement>(Pin + "/people");
        Assert.True(people.EnumerateArray().Single(p => p.GetProperty("id").GetGuid() == workerId).GetProperty("hasPin").GetBoolean());
        Assert.Equal(HttpStatusCode.NoContent, (await admin.DeleteAsync($"{Pin}/{workerId}")).StatusCode);
        Assert.False((await worker.GetFromJsonAsync<JsonElement>(Pin)).GetProperty("iHavePin").GetBoolean());
    }

    [Fact]
    public async Task Switch_SignsInAsThePinOwner_WorkersAndAdminsOnly_AndLocksAfterFiveWrongPins()
    {
        var id = Guid.NewGuid().ToString("N")[..8];
        var till = await ClientAsync($"till-{id}", "Worker@12345", AdminRole.Worker);
        var anna = await ClientAsync($"anna-{id}", "Worker@12345", AdminRole.Worker);
        var ceo = await ClientAsync($"ceo-{id}", "Ceo@123456", AdminRole.Ceo);
        var annaPin = RandomPin();
        var ceoPin = RandomPin();
        await SetPinAsync(anna, "Worker@12345", annaPin);
        await SetPinAsync(ceo, "Ceo@123456", ceoPin);

        var res = await till.PostAsJsonAsync(Pin + "/switch", new { pin = annaPin });
        Assert.Equal(HttpStatusCode.OK, res.StatusCode);
        var body = await res.Content.ReadFromJsonAsync<JsonElement>();
        Assert.Equal("Worker", body.GetProperty("role").GetString());

        // The new token really is Anna's.
        var now = factory.CreateClient();
        ApiHelpers.Authorize(now, body.GetProperty("accessToken").GetString()!);
        Assert.Equal(HttpStatusCode.OK, (await now.GetAsync(Clock + "/now")).StatusCode);

        // The owner signs in with the password.
        Assert.Equal(HttpStatusCode.Forbidden, (await till.PostAsJsonAsync(Pin + "/switch", new { pin = ceoPin })).StatusCode);

        // Wrong PINs: 422 four times, 429 once locked.
        StaffPinService.ResetFailures();
        for (var i = 0; i < StaffPinService.MaxFailures; i++)
            Assert.Equal(HttpStatusCode.UnprocessableEntity, (await till.PostAsJsonAsync(Pin + "/switch", new { pin = "000000" })).StatusCode);
        Assert.Equal((HttpStatusCode)429, (await till.PostAsJsonAsync(Pin + "/switch", new { pin = annaPin })).StatusCode);
        StaffPinService.ResetFailures();
    }

    [Fact]
    public async Task Punch_ClocksInThenOut_AndWorkerCannotReadTheHoursReport()
    {
        var id = Guid.NewGuid().ToString("N")[..8];
        var till = await ClientAsync($"till-{id}", "Worker@12345", AdminRole.Worker);
        var admin = await ClientAsync($"adm-{id}", "Admin@12345", AdminRole.Admin);
        var anna = await ClientAsync($"anna-{id}", "Worker@12345", AdminRole.Worker);
        var annaId = await UserIdAsync($"anna-{id}");
        var pin = RandomPin();
        await SetPinAsync(anna, "Worker@12345", pin);

        var inRes = await till.PostAsJsonAsync(Clock + "/punch", new { pin });
        Assert.Equal(HttpStatusCode.OK, inRes.StatusCode);
        Assert.Equal("in", (await inRes.Content.ReadFromJsonAsync<JsonElement>()).GetProperty("action").GetString());
        var now = await till.GetFromJsonAsync<JsonElement>(Clock + "/now");
        Assert.Contains(now.EnumerateArray(), e => e.GetProperty("adminUserId").GetGuid() == annaId);

        var outRes = await till.PostAsJsonAsync(Clock + "/punch", new { pin });
        Assert.Equal("out", (await outRes.Content.ReadFromJsonAsync<JsonElement>()).GetProperty("action").GetString());
        now = await till.GetFromJsonAsync<JsonElement>(Clock + "/now");
        Assert.DoesNotContain(now.EnumerateArray(), e => e.GetProperty("adminUserId").GetGuid() == annaId);

        var url = $"{Clock}/hours?from={DateTime.UtcNow.AddDays(-1):yyyy-MM-dd}&to={DateTime.UtcNow.AddDays(1):yyyy-MM-dd}&staffId={annaId}";
        Assert.Equal(HttpStatusCode.Forbidden, (await till.GetAsync(url)).StatusCode);
        var report = await admin.GetFromJsonAsync<JsonElement>(url);
        Assert.Equal(1, report.GetProperty("entries").GetArrayLength());
        Assert.False(report.GetProperty("entries")[0].GetProperty("open").GetBoolean());
    }

    [Fact]
    public async Task EveryRole_CanClockInAndOutFromTheirOwnLogin_WithoutAPin()
    {
        var id = Guid.NewGuid().ToString("N")[..8];
        foreach (var (name, role) in new[] { ($"bar-{id}", AdminRole.Worker), ($"adm-{id}", AdminRole.Admin), ($"ceo-{id}", AdminRole.Ceo) })
        {
            var c = await ClientAsync(name, "Passw0rd@123", role);
            Assert.False((await c.GetFromJsonAsync<JsonElement>(Clock + "/me")).GetProperty("clockedIn").GetBoolean());
            var inRes = await (await c.PostAsJsonAsync(Clock + "/me/toggle", new { })).Content.ReadFromJsonAsync<JsonElement>();
            Assert.Equal("in", inRes.GetProperty("action").GetString());
            Assert.True((await c.GetFromJsonAsync<JsonElement>(Clock + "/me")).GetProperty("clockedIn").GetBoolean());
            var outRes = await (await c.PostAsJsonAsync(Clock + "/me/toggle", new { })).Content.ReadFromJsonAsync<JsonElement>();
            Assert.Equal("out", outRes.GetProperty("action").GetString());
        }
        var op = await ClientAsync($"op-{id}", "OperatorPass@12345", AdminRole.SuperAdmin);
        Assert.Equal(HttpStatusCode.NotFound, (await op.PostAsJsonAsync(Clock + "/me/toggle", new { })).StatusCode);
    }

    [Fact]
    public async Task ForgottenClockOut_IsClosedWithZeroHours_AndAManagerCanFixIt()
    {
        var id = Guid.NewGuid().ToString("N")[..8];
        var till = await ClientAsync($"till-{id}", "Worker@12345", AdminRole.Worker);
        var admin = await ClientAsync($"adm-{id}", "Admin@12345", AdminRole.Admin);
        var anna = await ClientAsync($"anna-{id}", "Worker@12345", AdminRole.Worker);
        var annaId = await UserIdAsync($"anna-{id}");
        var pin = RandomPin();
        await SetPinAsync(anna, "Worker@12345", pin);

        // Yesterday's shift was never closed.
        var stale = DateTime.UtcNow.AddHours(-30);
        Guid staleId;
        await using (var scope = factory.Services.CreateAsyncScope())
        {
            // Added by a manager so the tenant is set the normal way, then reopened directly.
            var added = await admin.PostAsJsonAsync(Clock + "/entries", new { adminUserId = annaId, clockInAt = stale, clockOutAt = stale.AddHours(8), reason = "setup" });
            Assert.Equal(HttpStatusCode.Created, added.StatusCode);
            staleId = (await added.Content.ReadFromJsonAsync<JsonElement>()).GetProperty("id").GetGuid();
            var db = scope.ServiceProvider.GetRequiredService<Yurt.Infrastructure.Persistence.ApplicationDbContext>();
            var row = await db.TimeEntries.IgnoreQueryFilters().SingleAsync(e => e.Id == staleId);
            row.ClockOutAt = null;
            await db.SaveChangesAsync();
        }

        var punch = await till.PostAsJsonAsync(Clock + "/punch", new { pin });
        var result = await punch.Content.ReadFromJsonAsync<JsonElement>();
        Assert.Equal("in", result.GetProperty("action").GetString());
        Assert.True(result.GetProperty("forgotLast").GetBoolean());

        var url = $"{Clock}/hours?from={stale.AddDays(-1):yyyy-MM-dd}&to={DateTime.UtcNow.AddDays(1):yyyy-MM-dd}&staffId={annaId}";
        var report = await admin.GetFromJsonAsync<JsonElement>(url);
        var entry = report.GetProperty("entries").EnumerateArray().Single(e => e.GetProperty("id").GetGuid() == staleId);
        Assert.True(entry.GetProperty("forgotten").GetBoolean());
        Assert.Equal(0m, entry.GetProperty("hours").GetDecimal());

        // A fix needs a reason.
        Assert.Equal(HttpStatusCode.BadRequest, (await admin.PutAsJsonAsync($"{Clock}/entries/{staleId}", new { clockOutAt = stale.AddHours(8) })).StatusCode);
        var fixedRes = await admin.PutAsJsonAsync($"{Clock}/entries/{staleId}", new { clockOutAt = stale.AddHours(8), reason = "Left at 18:00" });
        Assert.Equal(HttpStatusCode.OK, fixedRes.StatusCode);
        var fixedBody = await fixedRes.Content.ReadFromJsonAsync<JsonElement>();
        Assert.Equal(8m, fixedBody.GetProperty("hours").GetDecimal());
        Assert.False(fixedBody.GetProperty("forgotten").GetBoolean());
    }

    [Fact]
    public async Task Payroll_AddsHoursTimesRate_ToTheSalary()
    {
        var id = Guid.NewGuid().ToString("N")[..8];
        var ceo = await ClientAsync($"ceo-{id}", "Ceo@123456", AdminRole.Ceo);
        var admin = await ClientAsync($"adm-{id}", "Admin@12345", AdminRole.Admin);
        await ClientAsync($"anna-{id}", "Worker@12345", AdminRole.Worker);
        var annaId = await UserIdAsync($"anna-{id}");
        const string month = "2019-05";

        // Two shifts of 8 and 6.5 hours in May 2019 (Almaty is UTC+5): clock-in at 09:00 local = 04:00 UTC.
        foreach (var (day, hours) in new[] { (3, 8.0), (4, 6.5) })
        {
            var start = new DateTime(2019, 5, day, 4, 0, 0, DateTimeKind.Utc);
            Assert.Equal(HttpStatusCode.Created, (await admin.PostAsJsonAsync(Clock + "/entries",
                new { adminUserId = annaId, clockInAt = start, clockOutAt = start.AddHours(hours), reason = "Forgot to clock" })).StatusCode);
        }

        Assert.Equal(HttpStatusCode.OK, (await ceo.PutAsJsonAsync($"/api/v1/admin/payroll/staff/{annaId}/salary", new { monthlySalary = 100000, hourlyRate = 1000 })).StatusCode);
        var sheet = await ceo.GetFromJsonAsync<JsonElement>($"/api/v1/admin/payroll/months/{month}");
        var line = sheet.GetProperty("lines").EnumerateArray().Single(l => l.GetProperty("adminUserId").GetGuid() == annaId);
        Assert.Equal(14.5m, line.GetProperty("hours").GetDecimal());
        Assert.Equal(14500m, line.GetProperty("hourlyPay").GetDecimal());
        Assert.Equal(114500m, line.GetProperty("total").GetDecimal());

        // Leaving the rate out keeps it; a closed month keeps the hours it was paid for.
        await ceo.PutAsJsonAsync($"/api/v1/admin/payroll/staff/{annaId}/salary", new { monthlySalary = 100000 });
        var staff = await ceo.GetFromJsonAsync<JsonElement>("/api/v1/admin/payroll/staff");
        Assert.Equal(1000m, staff.EnumerateArray().Single(s => s.GetProperty("id").GetGuid() == annaId).GetProperty("hourlyRate").GetDecimal());
        Assert.Equal(HttpStatusCode.OK, (await ceo.PostAsync($"/api/v1/admin/payroll/months/{month}/close", null)).StatusCode);
        await ceo.PutAsJsonAsync($"/api/v1/admin/payroll/staff/{annaId}/salary", new { monthlySalary = 100000, hourlyRate = 5000 });
        var closed = await ceo.GetFromJsonAsync<JsonElement>($"/api/v1/admin/payroll/months/{month}");
        var closedLine = closed.GetProperty("lines").EnumerateArray().Single(l => l.GetProperty("adminUserId").GetGuid() == annaId);
        Assert.Equal(114500m, closedLine.GetProperty("total").GetDecimal());
        Assert.Equal(14.5m, closedLine.GetProperty("hours").GetDecimal());
        await ceo.PostAsync($"/api/v1/admin/payroll/months/{month}/reopen", null);
    }
}
