using System.Net;
using System.Net.Http.Headers;
using System.Net.Http.Json;
using System.Text.Json;
using Yurt.Domain;
using Yurt.Domain.Enums;
using Yurt.IntegrationTests.Helpers;

namespace Yurt.IntegrationTests.Tests;

[Collection("Integration")]
public class PlatformTests(YurtWebAppFactory factory)
{
    private async Task<HttpClient> SuperAdminClientAsync()
    {
        var client = factory.CreateClient();
        var token = await ApiHelpers.CreateAdminTokenAsync(factory.Services, client,
            username: "platform-owner", password: "PlatformOwner@123", role: AdminRole.SuperAdmin);
        client.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", token);
        return client;
    }

    private async Task<HttpClient> CafeAdminClientAsync()
    {
        var client = factory.CreateClient();
        var token = await ApiHelpers.CreateAdminTokenAsync(factory.Services, client);
        client.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", token);
        return client;
    }

    [Theory]
    [InlineData("/api/v1/platform/modules")]
    [InlineData("/api/v1/platform/maintenance")]
    [InlineData("/api/v1/platform/integrations")]
    [InlineData("/api/v1/platform/app-releases")]
    public async Task PlatformEndpoints_AreForbiddenForCafeAdmin_AndUnauthorizedAnonymously(string url)
    {
        var admin = await CafeAdminClientAsync();
        Assert.Equal(HttpStatusCode.Forbidden, (await admin.GetAsync(url)).StatusCode);

        var anon = factory.CreateClient();
        Assert.Equal(HttpStatusCode.Unauthorized, (await anon.GetAsync(url)).StatusCode);
    }

    [Fact]
    public async Task SuperAdmin_CanReadPlatformEndpoints_AndKeepsAdminPowers()
    {
        var client = await SuperAdminClientAsync();

        Assert.Equal(HttpStatusCode.OK, (await client.GetAsync("/api/v1/platform/modules")).StatusCode);
        Assert.Equal(HttpStatusCode.OK, (await client.GetAsync("/api/v1/platform/integrations")).StatusCode);
        Assert.Equal(HttpStatusCode.OK, (await client.GetAsync("/api/v1/platform/app-releases")).StatusCode);
        // Admin-only areas still work for a super admin
        Assert.Equal(HttpStatusCode.OK, (await client.GetAsync("/api/v1/admin/workers")).StatusCode);
        Assert.Equal(HttpStatusCode.OK, (await client.GetAsync("/api/v1/admin/audit-log")).StatusCode);
    }

    [Fact]
    public async Task Workers_DoesNotListSuperAdmins()
    {
        var super = await SuperAdminClientAsync();
        var admin = await CafeAdminClientAsync();

        var json = await admin.GetFromJsonAsync<JsonElement>("/api/v1/admin/workers");
        var names = json.EnumerateArray().Select(w => w.GetProperty("username").GetString()).ToList();
        Assert.DoesNotContain("platform-owner", names);
        _ = super;
    }

    [Fact]
    public async Task UpdateModule_SwitchesItOff_ThenOn_AndShowsInAppConfig()
    {
        var client = await SuperAdminClientAsync();
        var key = ModuleKeys.GroupOrders;
        try
        {
            var off = await client.PutAsJsonAsync($"/api/v1/platform/modules/{key}",
                new { enabled = false, validUntil = (DateTime?)null, source = "Manual", notes = "test" });
            Assert.Equal(HttpStatusCode.OK, off.StatusCode);

            var cfg = await factory.CreateClient().GetFromJsonAsync<JsonElement>("/api/v1/app/config");
            var keys = cfg.GetProperty("modules").EnumerateArray().Select(e => e.GetString()).ToList();
            Assert.DoesNotContain(key, keys);
            Assert.Contains(ModuleKeys.Marketing, keys);
        }
        finally
        {
            var on = await client.PutAsJsonAsync($"/api/v1/platform/modules/{key}",
                new { enabled = true, validUntil = (DateTime?)null, source = "Manual", notes = (string?)null });
            Assert.Equal(HttpStatusCode.OK, on.StatusCode);
        }

        var cfg2 = await factory.CreateClient().GetFromJsonAsync<JsonElement>("/api/v1/app/config");
        Assert.Contains(key, cfg2.GetProperty("modules").EnumerateArray().Select(e => e.GetString()));
    }

    [Fact]
    public async Task UpdateModule_CoreCannotBeDisabled_UnknownKeyIs404()
    {
        var client = await SuperAdminClientAsync();

        var core = await client.PutAsJsonAsync("/api/v1/platform/modules/core",
            new { enabled = false, validUntil = (DateTime?)null, source = "Manual", notes = (string?)null });
        Assert.Equal(HttpStatusCode.BadRequest, core.StatusCode);

        var unknown = await client.PutAsJsonAsync("/api/v1/platform/modules/nope",
            new { enabled = false, validUntil = (DateTime?)null, source = "Manual", notes = (string?)null });
        Assert.Equal(HttpStatusCode.NotFound, unknown.StatusCode);
    }

    [Fact]
    public async Task Maintenance_Blocks503ForCustomerApi_ButNotForConfigOrStaff()
    {
        var client = await SuperAdminClientAsync();
        var anon = factory.CreateClient();
        try
        {
            var on = await client.PutAsJsonAsync("/api/v1/platform/maintenance",
                new { enabled = true, message = "Back at 18:00" });
            Assert.Equal(HttpStatusCode.OK, on.StatusCode);

            var menu = await anon.GetAsync("/api/v1/menu");
            Assert.Equal(HttpStatusCode.ServiceUnavailable, menu.StatusCode);

            var cfg = await anon.GetFromJsonAsync<JsonElement>("/api/v1/app/config");
            Assert.True(cfg.GetProperty("maintenance").GetProperty("enabled").GetBoolean());
            Assert.Equal("Back at 18:00", cfg.GetProperty("maintenance").GetProperty("message").GetString());

            Assert.Equal(HttpStatusCode.OK, (await client.GetAsync("/api/v1/platform/modules")).StatusCode);
        }
        finally
        {
            var off = await client.PutAsJsonAsync("/api/v1/platform/maintenance", new { enabled = false, message = "" });
            Assert.Equal(HttpStatusCode.OK, off.StatusCode);
        }

        Assert.NotEqual(HttpStatusCode.ServiceUnavailable, (await anon.GetAsync("/api/v1/menu")).StatusCode);
    }

    [Fact]
    public async Task ModuleChanges_AreWrittenToTheAuditLogWithTheActor()
    {
        var client = await SuperAdminClientAsync();
        var put = await client.PutAsJsonAsync("/api/v1/platform/modules/engagement",
            new { enabled = true, validUntil = (DateTime?)null, source = "Trial", notes = "audit test" });
        Assert.Equal(HttpStatusCode.OK, put.StatusCode);

        var log = await client.GetStringAsync("/api/v1/admin/audit-log?pageSize=50");
        Assert.Contains("ModuleLicenseUpdated", log);
        Assert.Contains("platform-owner", log);
    }
}
