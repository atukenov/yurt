using System.Net;
using System.Net.Http.Headers;
using System.Net.Http.Json;
using System.Text.Json;
using Yurt.Domain;
using Yurt.Domain.Enums;
using Yurt.IntegrationTests.Helpers;

namespace Yurt.IntegrationTests.Tests;

[Collection("Integration")]
public class IikoSyncModuleGateTests(YurtWebAppFactory factory)
{
    private static object License(bool enabled)
        => new { enabled, validUntil = (DateTime?)null, source = "Manual", notes = (string?)null };

    [Fact]
    public async Task IikoSync_SwitchedOff_BlocksAdapterEndpoints_ButOrdersStillWork()
    {
        var admin = factory.CreateClient();
        admin.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer",
            await ApiHelpers.CreateAdminTokenAsync(factory.Services, admin));
        var super = factory.CreateClient();
        super.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer",
            await ApiHelpers.CreateAdminTokenAsync(factory.Services, super,
                username: "platform-owner", password: "PlatformOwner@123", role: AdminRole.SuperAdmin));

        try
        {
            await super.PutAsJsonAsync($"/api/v1/platform/modules/{ModuleKeys.IikoSync}", License(false));

            foreach (var url in new[] { "/api/v1/admin/iiko/nomenclature", "/api/v1/admin/iiko/payment-types", "/api/v1/admin/iiko/terminal-groups" })
            {
                var r = await admin.GetAsync(url);
                Assert.Equal(HttpStatusCode.Forbidden, r.StatusCode);
                var body = await r.Content.ReadFromJsonAsync<JsonElement>();
                Assert.Equal("module_disabled", body.GetProperty("code").GetString());
                Assert.Equal(ModuleKeys.IikoSync, body.GetProperty("module").GetString());
            }

            var hook = await factory.CreateClient().PostAsJsonAsync("/api/v1/webhooks/iiko", Array.Empty<object>());
            Assert.Equal(HttpStatusCode.Forbidden, hook.StatusCode);

            // Orders (core) are unaffected
            Assert.NotEqual(HttpStatusCode.Forbidden, (await admin.GetAsync("/api/v1/admin/orders")).StatusCode);
        }
        finally
        {
            await super.PutAsJsonAsync($"/api/v1/platform/modules/{ModuleKeys.IikoSync}", License(true));
        }

        Assert.NotEqual(HttpStatusCode.Forbidden, (await admin.GetAsync("/api/v1/admin/iiko/terminal-groups")).StatusCode);
    }
}
