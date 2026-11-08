using System.Net;
using System.Net.Http.Json;
using System.Text.Json;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Yurt.Application.Common.Interfaces;
using Yurt.Domain.Enums;
using Yurt.IntegrationTests.Helpers;

namespace Yurt.IntegrationTests.Tests;

/// <summary>Who may reset whose password: the platform operator resets cafe admins; a cafe admin resets workers only.</summary>
[Collection("Integration")]
public class PasswordResetRulesTests(YurtWebAppFactory factory)
{
    private async Task<HttpClient> ClientAsync(string username, string password, AdminRole role)
    {
        var client = factory.CreateClient();
        ApiHelpers.Authorize(client, await ApiHelpers.CreateAdminTokenAsync(factory.Services, client, username, password, role: role));
        return client;
    }

    private async Task<Guid> AdminIdAsync(string username)
    {
        await using var scope = factory.Services.CreateAsyncScope();
        var db = scope.ServiceProvider.GetRequiredService<IApplicationDbContext>();
        return await db.AdminUsers.IgnoreQueryFilters().Where(u => u.Username == username).Select(u => u.Id).SingleAsync();
    }

    [Fact]
    public async Task CafeAdmin_ResetsWorkers_ButNotAdmins()
    {
        var id = Guid.NewGuid().ToString("N")[..8];
        var admin = await ClientAsync($"boss-{id}", "Boss@12345", AdminRole.Admin);
        await ClientAsync($"peer-{id}", "Peer@12345", AdminRole.Admin);

        var created = await admin.PostAsJsonAsync("/api/v1/admin/workers", new { username = $"barista-{id}", password = "Barista@123" });
        Assert.Equal(HttpStatusCode.Created, created.StatusCode);
        var worker = (await created.Content.ReadFromJsonAsync<JsonElement>()).GetProperty("id").GetGuid();

        var ok = await admin.PostAsJsonAsync($"/api/v1/admin/workers/{worker}/reset-password", new { newPassword = "NewBarista@123" });
        Assert.Equal(HttpStatusCode.OK, ok.StatusCode);
        var login = await factory.CreateClient().PostAsJsonAsync("/api/v1/admin/auth/login", new { username = $"barista-{id}", password = "NewBarista@123" });
        Assert.Equal(HttpStatusCode.OK, login.StatusCode);

        // Another admin, and the admin's own account, are not reset from the staff page.
        var peer = await AdminIdAsync($"peer-{id}");
        var self = await AdminIdAsync($"boss-{id}");
        foreach (var target in new[] { peer, self })
        {
            var denied = await admin.PostAsJsonAsync($"/api/v1/admin/workers/{target}/reset-password", new { newPassword = "Hijacked@123" });
            Assert.Equal(HttpStatusCode.Forbidden, denied.StatusCode);
        }
        var stillOld = await factory.CreateClient().PostAsJsonAsync("/api/v1/admin/auth/login", new { username = $"peer-{id}", password = "Peer@12345" });
        Assert.Equal(HttpStatusCode.OK, stillOld.StatusCode);
    }

    [Fact]
    public async Task Operator_ResetsACafeAdmin_WhoMustChooseANewPasswordAndLosesOldSessions()
    {
        var op = await ClientAsync("platform-owner", "PlatformOwner@123", AdminRole.SuperAdmin);
        var id = Guid.NewGuid().ToString("N")[..8];
        var cafe = await (await op.PostAsJsonAsync("/api/v1/platform/cafes",
            new { slug = $"reset-{id}", name = $"Reset {id}", domain = (string?)null, adminUsername = $"owner-{id}", adminPassword = "FirstLogin@123" }))
            .Content.ReadFromJsonAsync<JsonElement>();
        var cafeId = cafe.GetProperty("id").GetGuid();

        var oldLogin = await (await factory.CreateClient().PostAsJsonAsync("/api/v1/admin/auth/login",
            new { username = $"owner-{id}", password = "FirstLogin@123" })).Content.ReadFromJsonAsync<JsonElement>();
        var oldRefresh = oldLogin.GetProperty("refreshToken").GetString();

        var admins = await op.GetFromJsonAsync<List<JsonElement>>($"/api/v1/platform/cafes/{cafeId}/admins");
        var owner = Assert.Single(admins!);
        Assert.Equal($"owner-{id}", owner.GetProperty("username").GetString());
        var ownerId = owner.GetProperty("id").GetGuid();

        Assert.Equal(HttpStatusCode.UnprocessableEntity,
            (await op.PostAsJsonAsync($"/api/v1/platform/cafes/{cafeId}/admins/{ownerId}/reset-password", new { newPassword = "short" })).StatusCode);

        var reset = await op.PostAsJsonAsync($"/api/v1/platform/cafes/{cafeId}/admins/{ownerId}/reset-password", new { newPassword = "Operator@Reset1" });
        Assert.Equal(HttpStatusCode.OK, reset.StatusCode);

        Assert.Equal(HttpStatusCode.Unauthorized, (await factory.CreateClient().PostAsJsonAsync("/api/v1/admin/auth/login",
            new { username = $"owner-{id}", password = "FirstLogin@123" })).StatusCode);
        var fresh = await factory.CreateClient().PostAsJsonAsync("/api/v1/admin/auth/login", new { username = $"owner-{id}", password = "Operator@Reset1" });
        Assert.Equal(HttpStatusCode.OK, fresh.StatusCode);
        Assert.True((await fresh.Content.ReadFromJsonAsync<JsonElement>()).GetProperty("mustChangePassword").GetBoolean());

        var onCafe = factory.CreateClient();
        onCafe.DefaultRequestHeaders.Add("X-Tenant", $"reset-{id}");
        Assert.Equal(HttpStatusCode.Unauthorized,
            (await onCafe.PostAsJsonAsync("/api/v1/admin/auth/refresh", new { refreshToken = oldRefresh })).StatusCode);
    }

    [Fact]
    public async Task OperatorReset_IsForThatCafesAdminsOnly_AndOnlyForTheOperator()
    {
        var op = await ClientAsync("platform-owner", "PlatformOwner@123", AdminRole.SuperAdmin);
        var admin = await ClientAsync("testadmin", "TestAdmin@123", AdminRole.Admin);
        var id = Guid.NewGuid().ToString("N")[..8];
        var cafeId = (await (await op.PostAsJsonAsync("/api/v1/platform/cafes",
            new { slug = $"only-{id}", name = $"Only {id}", domain = (string?)null, adminUsername = $"own-{id}", adminPassword = "FirstLogin@123" }))
            .Content.ReadFromJsonAsync<JsonElement>()).GetProperty("id").GetGuid();

        // An admin of another cafe is not found under this cafe, and a cafe admin cannot use the operator endpoints at all.
        var other = await AdminIdAsync("testadmin");
        Assert.Equal(HttpStatusCode.NotFound,
            (await op.PostAsJsonAsync($"/api/v1/platform/cafes/{cafeId}/admins/{other}/reset-password", new { newPassword = "Operator@Reset1" })).StatusCode);
        Assert.Equal(HttpStatusCode.Forbidden, (await admin.GetAsync($"/api/v1/platform/cafes/{cafeId}/admins")).StatusCode);
        Assert.Equal(HttpStatusCode.Forbidden,
            (await admin.PostAsJsonAsync($"/api/v1/platform/cafes/{cafeId}/admins/{other}/reset-password", new { newPassword = "Hijacked@123" })).StatusCode);
    }
}
