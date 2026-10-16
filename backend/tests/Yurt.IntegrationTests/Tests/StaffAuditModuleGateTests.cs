using System.Net;
using System.Net.Http.Headers;
using System.Net.Http.Json;
using System.Text.Json;
using Yurt.Domain;
using Yurt.Domain.Enums;
using Yurt.IntegrationTests.Helpers;

namespace Yurt.IntegrationTests.Tests;

[Collection("Integration")]
public class StaffAuditModuleGateTests(YurtWebAppFactory factory)
{
    private static object License(bool enabled)
        => new { enabled, validUntil = (DateTime?)null, source = "Manual", notes = (string?)null };

    [Fact]
    public async Task StaffAudit_SwitchedOff_BlocksWorkersAndAuditLog_ButOrdersStillWork()
    {
        var admin = factory.CreateClient();
        admin.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer",
            await ApiHelpers.CreateAdminTokenAsync(factory.Services, admin));
        var super = factory.CreateClient();
        super.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer",
            await ApiHelpers.CreateAdminTokenAsync(factory.Services, super,
                username: "platform-owner", password: "PlatformOwner@123", role: AdminRole.SuperAdmin));

        Assert.Equal(HttpStatusCode.OK, (await admin.GetAsync("/api/v1/admin/workers")).StatusCode);
        Assert.Equal(HttpStatusCode.OK, (await admin.GetAsync("/api/v1/admin/audit-log")).StatusCode);

        try
        {
            await super.PutAsJsonAsync($"/api/v1/platform/modules/{ModuleKeys.StaffAudit}", License(false));

            foreach (var url in new[] { "/api/v1/admin/workers", "/api/v1/admin/audit-log" })
            {
                var r = await admin.GetAsync(url);
                Assert.Equal(HttpStatusCode.Forbidden, r.StatusCode);
                var body = await r.Content.ReadFromJsonAsync<JsonElement>();
                Assert.Equal("module_disabled", body.GetProperty("code").GetString());
                Assert.Equal(ModuleKeys.StaffAudit, body.GetProperty("module").GetString());
            }
            Assert.Equal(HttpStatusCode.Forbidden,
                (await admin.PostAsJsonAsync("/api/v1/admin/workers", new { username = "w1", password = "secret123" })).StatusCode);

            // Orders (core) are unaffected
            Assert.NotEqual(HttpStatusCode.Forbidden, (await admin.GetAsync("/api/v1/admin/orders")).StatusCode);
        }
        finally
        {
            await super.PutAsJsonAsync($"/api/v1/platform/modules/{ModuleKeys.StaffAudit}", License(true));
        }

        Assert.Equal(HttpStatusCode.OK, (await admin.GetAsync("/api/v1/admin/workers")).StatusCode);
    }
}
