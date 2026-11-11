using System.Net;
using System.Net.Http.Json;
using System.Text.Json;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Yurt.Application.Common.Interfaces;
using Yurt.Domain.Enums;
using Yurt.IntegrationTests.Helpers;

namespace Yurt.IntegrationTests.Tests;

/// <summary>Worker / Admin / CEO: Admins run the cafe (orders, stock, purchasing) but see no money screens; the CEO sees them.</summary>
[Collection("Integration")]
public class CeoRoleTests(YurtWebAppFactory factory)
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

    [Theory]
    [InlineData("/api/v1/admin/dashboard")]
    [InlineData("/api/v1/admin/analytics")]
    [InlineData("/api/v1/admin/audit-log")]
    public async Task MoneyScreens_AreForTheCeo_NotAdminsOrWorkers(string url)
    {
        var id = Guid.NewGuid().ToString("N")[..8];
        var admin = await ClientAsync($"adm-{id}", "Admin@12345", AdminRole.Admin);
        var worker = await ClientAsync($"wrk-{id}", "Worker@12345", AdminRole.Worker);
        var ceo = await ClientAsync($"ceo-{id}", "Ceo@123456", AdminRole.Ceo);

        Assert.Equal(HttpStatusCode.Forbidden, (await admin.GetAsync(url)).StatusCode);
        Assert.Equal(HttpStatusCode.Forbidden, (await worker.GetAsync(url)).StatusCode);
        Assert.NotEqual(HttpStatusCode.Forbidden, (await ceo.GetAsync(url)).StatusCode);
    }

    [Fact]
    public async Task Admin_StillRunsTheCafe_StockOrdersAndPurchasing()
    {
        var id = Guid.NewGuid().ToString("N")[..8];
        var admin = await ClientAsync($"adm-{id}", "Admin@12345", AdminRole.Admin);

        foreach (var url in new[] { "/api/v1/admin/orders", "/api/v1/admin/ingredients", "/api/v1/admin/purchasing/suppliers", "/api/v1/admin/shifts/current" })
            Assert.NotEqual(HttpStatusCode.Forbidden, (await admin.GetAsync(url)).StatusCode);
    }

    [Fact]
    public async Task Ceo_AddsAnAdmin_ButAnAdminCannotAddOneOrTouchTheCeo()
    {
        var id = Guid.NewGuid().ToString("N")[..8];
        var ceo = await ClientAsync($"ceo-{id}", "Ceo@123456", AdminRole.Ceo);
        var admin = await ClientAsync($"adm-{id}", "Admin@12345", AdminRole.Admin);

        var created = await ceo.PostAsJsonAsync("/api/v1/admin/workers", new { username = $"newadm-{id}", password = "NewAdmin@123", role = "Admin" });
        Assert.Equal(HttpStatusCode.Created, created.StatusCode);
        Assert.Equal("Admin", (await created.Content.ReadFromJsonAsync<JsonElement>()).GetProperty("role").GetString());

        var denied = await admin.PostAsJsonAsync("/api/v1/admin/workers", new { username = $"x-{id}", password = "Secret@123", role = "Admin" });
        Assert.Equal(HttpStatusCode.Forbidden, denied.StatusCode);

        // An Admin cannot switch off, rename or reset the CEO, nor another Admin.
        var ceoId = await UserIdAsync($"ceo-{id}");
        var peerId = await UserIdAsync($"newadm-{id}");
        foreach (var target in new[] { ceoId, peerId })
        {
            Assert.Equal(HttpStatusCode.Forbidden,
                (await admin.PatchAsJsonAsync($"/api/v1/admin/workers/{target}/active", new { isActive = false })).StatusCode);
            Assert.Equal(HttpStatusCode.Forbidden,
                (await admin.PostAsJsonAsync($"/api/v1/admin/workers/{target}/reset-password", new { newPassword = "Hijacked@123" })).StatusCode);
        }

        // The CEO resets an Admin's password, but not their own (the operator does that).
        Assert.Equal(HttpStatusCode.OK,
            (await ceo.PostAsJsonAsync($"/api/v1/admin/workers/{peerId}/reset-password", new { newPassword = "Fresh@12345" })).StatusCode);
        Assert.Equal(HttpStatusCode.Forbidden,
            (await ceo.PostAsJsonAsync($"/api/v1/admin/workers/{ceoId}/reset-password", new { newPassword = "Mine@12345" })).StatusCode);
    }

    [Fact]
    public async Task Operator_MakesAnAdminTheCeo_AndTheCafeKeepsOne()
    {
        var id = Guid.NewGuid().ToString("N")[..8];
        var op = await ClientAsync($"op-{id}", "OperatorPass@12345", AdminRole.SuperAdmin);
        await ClientAsync($"adm-{id}", "Admin@12345", AdminRole.Admin);
        var adminId = await UserIdAsync($"adm-{id}");
        var cafe = Guid.Parse("22222222-0000-0000-0000-000000000001");

        var up = await op.PutAsJsonAsync($"/api/v1/platform/cafes/{cafe}/admins/{adminId}/role", new { role = "Ceo" });
        Assert.Equal(HttpStatusCode.OK, up.StatusCode);
        Assert.Equal("Ceo", (await up.Content.ReadFromJsonAsync<JsonElement>()).GetProperty("role").GetString());

        // New sign-in carries the new role.
        var login = await factory.CreateClient().PostAsJsonAsync("/api/v1/admin/auth/login", new { username = $"adm-{id}", password = "Admin@12345" });
        var token = (await login.Content.ReadFromJsonAsync<JsonElement>()).GetProperty("accessToken").GetString();
        var client = factory.CreateClient();
        ApiHelpers.Authorize(client, token!);
        Assert.NotEqual(HttpStatusCode.Forbidden, (await client.GetAsync("/api/v1/admin/audit-log")).StatusCode);

        var bad = await op.PutAsJsonAsync($"/api/v1/platform/cafes/{cafe}/admins/{adminId}/role", new { role = "SuperAdmin" });
        Assert.Equal(HttpStatusCode.UnprocessableEntity, bad.StatusCode);
    }
}
