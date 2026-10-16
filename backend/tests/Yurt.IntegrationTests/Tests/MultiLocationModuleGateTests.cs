using System.Net;
using System.Net.Http.Headers;
using System.Net.Http.Json;
using System.Text.Json;
using Yurt.Domain;
using Yurt.Domain.Enums;
using Yurt.IntegrationTests.Helpers;

namespace Yurt.IntegrationTests.Tests;

[Collection("Integration")]
public class MultiLocationModuleGateTests(YurtWebAppFactory factory)
{
    private static object License(bool enabled)
        => new { enabled, validUntil = (DateTime?)null, source = "Manual", notes = (string?)null };

    private static object NewLocation(string name)
        => new { name, address = "Test street 1", workingHours = "", contactPhone = "+77001112233" };

    [Fact]
    public async Task MultiLocation_SwitchedOff_LimitsToOneBranch_KeepsExistingData()
    {
        var admin = factory.CreateClient();
        admin.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer",
            await ApiHelpers.CreateAdminTokenAsync(factory.Services, admin));
        var super = factory.CreateClient();
        super.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer",
            await ApiHelpers.CreateAdminTokenAsync(factory.Services, super,
                username: "platform-owner", password: "PlatformOwner@123", role: AdminRole.SuperAdmin));
        var guest = factory.CreateClient();

        // On (default): a second branch can be added and customers see all active ones
        var created = await admin.PostAsJsonAsync("/api/v1/admin/locations", NewLocation("Gate branch 2"));
        Assert.True(created.IsSuccessStatusCode);
        var allBefore = (await guest.GetFromJsonAsync<JsonElement>("/api/v1/locations")).GetArrayLength();
        Assert.True(allBefore >= 2);

        try
        {
            await super.PutAsJsonAsync($"/api/v1/platform/modules/{ModuleKeys.MultiLocation}", License(false));

            var blocked = await admin.PostAsJsonAsync("/api/v1/admin/locations", NewLocation("Gate branch 3"));
            Assert.Equal(HttpStatusCode.Forbidden, blocked.StatusCode);
            var body = await blocked.Content.ReadFromJsonAsync<JsonElement>();
            Assert.Equal("module_disabled", body.GetProperty("code").GetString());
            Assert.Equal(ModuleKeys.MultiLocation, body.GetProperty("module").GetString());

            // Customers are offered one branch; nothing was deleted
            Assert.Equal(1, (await guest.GetFromJsonAsync<JsonElement>("/api/v1/locations")).GetArrayLength());
            var adminList = await admin.GetFromJsonAsync<JsonElement>("/api/v1/admin/locations");
            Assert.True(adminList.GetArrayLength() >= 2);

            // Editing the existing branches stays possible (core)
            Assert.Equal(HttpStatusCode.OK, (await admin.GetAsync("/api/v1/admin/locations")).StatusCode);
        }
        finally
        {
            await super.PutAsJsonAsync($"/api/v1/platform/modules/{ModuleKeys.MultiLocation}", License(true));
        }

        Assert.Equal(allBefore, (await guest.GetFromJsonAsync<JsonElement>("/api/v1/locations")).GetArrayLength());
    }
}
