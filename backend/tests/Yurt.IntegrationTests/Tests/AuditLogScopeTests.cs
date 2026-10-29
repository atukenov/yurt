using System.Net;
using System.Net.Http.Json;
using System.Text.Json;
using Microsoft.Extensions.DependencyInjection;
using Yurt.Application.Common;
using Yurt.Application.Common.Interfaces;
using Yurt.Domain.Entities;
using Yurt.Domain.Enums;
using Yurt.Infrastructure.Persistence;
using Yurt.IntegrationTests.Helpers;

namespace Yurt.IntegrationTests.Tests;

/// <summary>
/// The audit log follows who is signed in: a cafe admin sees only their own cafe's history, the platform operator
/// sees the operators' own actions, and can still open one cafe's history.
/// </summary>
[Collection("Integration")]
public class AuditLogScopeTests(YurtWebAppFactory factory)
{
    private const string Password = "CafeAdmin@123";

    private static HttpClient Client(YurtWebAppFactory f, string token, string? cafe = null)
    {
        var c = f.CreateClient();
        ApiHelpers.Authorize(c, token);
        if (cafe is not null) c.DefaultRequestHeaders.Add("X-Tenant", cafe);
        return c;
    }

    private static async Task<List<string>> ActionsAsync(HttpClient client, string query = "")
    {
        var res = await client.GetAsync($"/api/v1/admin/audit-log?pageSize=200{query}");
        Assert.Equal(HttpStatusCode.OK, res.StatusCode);
        var body = await res.Content.ReadFromJsonAsync<JsonElement>();
        return body.GetProperty("items").EnumerateArray().Select(i => i.GetProperty("action").GetString()!).ToList();
    }

    /// <summary>A second cafe with its own admin, signed in on the shared admin address.</summary>
    private async Task<(Guid Id, string Slug, string Token)> SecondCafeAsync(string id)
    {
        var slug = $"log-{id}";
        Guid tenantId;
        using (var scope = factory.Services.CreateScope())
        {
            var db = scope.ServiceProvider.GetRequiredService<ApplicationDbContext>();
            var tenant = new Tenant { Slug = slug, Name = slug };
            db.Tenants.Add(tenant);
            db.AdminUsers.Add(new AdminUser
            {
                TenantId = tenant.Id, Username = $"log-b-{id}", Role = AdminRole.Admin, IsActive = true,
                PasswordHash = scope.ServiceProvider.GetRequiredService<IPasswordHasher>().Hash(Password),
            });
            await db.SaveChangesAsync();
            tenantId = tenant.Id;
        }
        factory.Services.GetRequiredService<ITenantDirectory>().Invalidate();

        var login = await factory.CreateClient().PostAsJsonAsync("/api/v1/admin/auth/login", new { username = $"log-b-{id}", password = Password });
        login.EnsureSuccessStatusCode();
        var token = (await login.Content.ReadFromJsonAsync<JsonElement>()).GetProperty("accessToken").GetString()!;
        return (tenantId, slug, token);
    }

    private async Task LogInCafeAsync(Guid tenantId, string action)
    {
        using var scope = factory.Services.CreateScope();
        scope.ServiceProvider.GetRequiredService<ITenantContext>().Set(tenantId, string.Empty);
        await scope.ServiceProvider.GetRequiredService<IAuditLogService>().LogAsync(action, "MenuCategory");
    }

    [Fact]
    public async Task EachAdminSeesOnlyTheirCafe_OperatorSeesPlatformActions()
    {
        var id = Guid.NewGuid().ToString("N")[..8];
        var adminA = Client(factory, await ApiHelpers.CreateAdminTokenAsync(factory.Services, factory.CreateClient(),
            username: $"log-a-{id}", password: Password));
        var operatorToken = await ApiHelpers.CreateAdminTokenAsync(factory.Services, factory.CreateClient(),
            username: "platform-owner", password: "PlatformOwner@123", role: AdminRole.SuperAdmin);
        var b = await SecondCafeAsync(id);
        var adminB = Client(factory, b.Token, b.Slug);

        var actionA = $"CafeA-{id}";
        var actionB = $"CafeB-{id}";
        await LogInCafeAsync(Tenant.DefaultId, actionA);
        await LogInCafeAsync(b.Id, actionB);
        // A platform action done while the operator had the first cafe selected: it still is not that cafe's history.
        var maintenanceOff = new { enabled = false, message = "" };
        Assert.Equal(HttpStatusCode.OK, (await Client(factory, operatorToken).PutAsJsonAsync("/api/v1/platform/maintenance", maintenanceOff)).StatusCode);
        Assert.Equal(HttpStatusCode.OK, (await Client(factory, operatorToken, b.Slug).PutAsJsonAsync("/api/v1/platform/maintenance", maintenanceOff)).StatusCode);

        var seenByA = await ActionsAsync(adminA);
        Assert.Contains(actionA, seenByA);
        Assert.DoesNotContain(actionB, seenByA);
        Assert.DoesNotContain("MaintenanceDisabled", seenByA);

        var seenByB = await ActionsAsync(adminB);
        Assert.Contains(actionB, seenByB);
        Assert.DoesNotContain(actionA, seenByB);
        Assert.DoesNotContain("MaintenanceDisabled", seenByB);

        // A cafe admin can neither ask for the platform history nor open another cafe's.
        Assert.Equal(HttpStatusCode.Forbidden, (await adminA.GetAsync("/api/v1/admin/audit-log?scope=platform")).StatusCode);
        var bIntoA = await Client(factory, b.Token, "default").GetAsync("/api/v1/admin/audit-log");
        Assert.True(bIntoA.StatusCode is HttpStatusCode.Unauthorized or HttpStatusCode.Forbidden, $"got {bIntoA.StatusCode}");

        // The operator: platform actions by default, no cafe's own rows.
        var platform = await ActionsAsync(Client(factory, operatorToken));
        Assert.True(platform.Count(a => a == "MaintenanceDisabled") >= 2);
        Assert.DoesNotContain(actionA, platform);
        Assert.DoesNotContain(actionB, platform);

        // ...and one cafe's history when asked for it.
        var cafeB = await ActionsAsync(Client(factory, operatorToken, b.Slug), "&scope=cafe");
        Assert.Contains(actionB, cafeB);
        Assert.DoesNotContain(actionA, cafeB);
        Assert.DoesNotContain("MaintenanceDisabled", cafeB);
    }
}
