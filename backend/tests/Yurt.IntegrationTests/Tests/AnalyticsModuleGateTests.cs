using System.Net;
using System.Net.Http.Headers;
using System.Net.Http.Json;
using System.Text.Json;
using Yurt.Domain;
using Yurt.Domain.Enums;
using Yurt.IntegrationTests.Helpers;

namespace Yurt.IntegrationTests.Tests;

[Collection("Integration")]
public class AnalyticsModuleGateTests(YurtWebAppFactory factory)
{
    private static object License(bool enabled, DateTime? validUntil = null)
        => new { enabled, validUntil, source = "Manual", notes = (string?)null };

    private async Task<(HttpClient admin, HttpClient super)> ClientsAsync()
    {
        var admin = factory.CreateClient();
        var adminToken = await ApiHelpers.CreateAdminTokenAsync(factory.Services, admin);
        admin.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", adminToken);

        var super = factory.CreateClient();
        var superToken = await ApiHelpers.CreateAdminTokenAsync(factory.Services, super,
            username: "platform-owner", password: "PlatformOwner@123", role: AdminRole.SuperAdmin);
        super.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", superToken);
        return (admin, super);
    }

    [Fact]
    public async Task Analytics_IsOpenByDefault()
    {
        var (admin, _) = await ClientsAsync();
        Assert.Equal(HttpStatusCode.OK, (await admin.GetAsync("/api/v1/admin/analytics?period=7d")).StatusCode);
    }

    [Fact]
    public async Task Analytics_SwitchedOff_Returns403ModuleDisabled_ThenWorksAgain()
    {
        var (admin, super) = await ClientsAsync();
        try
        {
            Assert.Equal(HttpStatusCode.OK,
                (await super.PutAsJsonAsync($"/api/v1/platform/modules/{ModuleKeys.Analytics}", License(false))).StatusCode);

            var blocked = await admin.GetAsync("/api/v1/admin/analytics?period=7d");
            Assert.Equal(HttpStatusCode.Forbidden, blocked.StatusCode);
            var body = await blocked.Content.ReadFromJsonAsync<JsonElement>();
            Assert.Equal("module_disabled", body.GetProperty("code").GetString());
            Assert.Equal(ModuleKeys.Analytics, body.GetProperty("module").GetString());

            Assert.Equal(HttpStatusCode.Forbidden, (await admin.GetAsync("/api/v1/admin/dashboard")).StatusCode);
            // Orders (core) are unaffected
            Assert.NotEqual(HttpStatusCode.Forbidden, (await admin.GetAsync("/api/v1/admin/orders")).StatusCode);
        }
        finally
        {
            await super.PutAsJsonAsync($"/api/v1/platform/modules/{ModuleKeys.Analytics}", License(true));
        }

        Assert.Equal(HttpStatusCode.OK, (await admin.GetAsync("/api/v1/admin/analytics?period=7d")).StatusCode);
    }

    [Fact]
    public async Task Analytics_ExpiredInsideGrace_StillWorks_PastGrace_Blocked()
    {
        var (admin, super) = await ClientsAsync();
        try
        {
            await super.PutAsJsonAsync($"/api/v1/platform/modules/{ModuleKeys.Analytics}", License(true, DateTime.UtcNow.AddDays(-3)));
            Assert.Equal(HttpStatusCode.OK, (await admin.GetAsync("/api/v1/admin/analytics?period=7d")).StatusCode);

            await super.PutAsJsonAsync($"/api/v1/platform/modules/{ModuleKeys.Analytics}", License(true, DateTime.UtcNow.AddDays(-30)));
            Assert.Equal(HttpStatusCode.Forbidden, (await admin.GetAsync("/api/v1/admin/analytics?period=7d")).StatusCode);
        }
        finally
        {
            await super.PutAsJsonAsync($"/api/v1/platform/modules/{ModuleKeys.Analytics}", License(true));
        }
    }
}
