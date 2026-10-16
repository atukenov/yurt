using System.Net;
using System.Net.Http.Headers;
using System.Net.Http.Json;
using Yurt.Domain;
using Yurt.Domain.Enums;
using Yurt.IntegrationTests.Helpers;

namespace Yurt.IntegrationTests.Tests;

[Collection("Integration")]
public class PushModuleGateTests(YurtWebAppFactory factory)
{
    private static object License(bool enabled)
        => new { enabled, validUntil = (DateTime?)null, source = "Manual", notes = (string?)null };

    [Fact]
    public async Task Push_SwitchedOff_DeviceRegistrationAndOrderingKeepWorking()
    {
        var super = factory.CreateClient();
        super.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer",
            await ApiHelpers.CreateAdminTokenAsync(factory.Services, super,
                username: "platform-owner", password: "PlatformOwner@123", role: AdminRole.SuperAdmin));
        var customer = factory.CreateClient();
        var (token, _) = await ApiHelpers.CreateCustomerAsync(customer, "+77002990004");
        ApiHelpers.Authorize(customer, token);

        try
        {
            await super.PutAsJsonAsync($"/api/v1/platform/modules/{ModuleKeys.Push}", License(false));

            // The app config reports the module as off, so clients stop registering...
            var cfg = await customer.GetFromJsonAsync<System.Text.Json.JsonElement>("/api/v1/app/config");
            var modules = cfg.GetProperty("modules").EnumerateArray().Select(e => e.GetString()).ToList();
            Assert.DoesNotContain(ModuleKeys.Push, modules);

            // ...but a client that still registers is not rejected (tokens are kept for when it is back on)
            var reg = await customer.PostAsJsonAsync("/api/v1/auth/device-tokens", new { token = "gate-test-token", platform = "android" });
            Assert.True(reg.IsSuccessStatusCode);

            // Ordering (core) is unaffected
            Assert.Equal(HttpStatusCode.OK, (await customer.GetAsync("/api/v1/orders/history")).StatusCode);
        }
        finally
        {
            await super.PutAsJsonAsync($"/api/v1/platform/modules/{ModuleKeys.Push}", License(true));
        }
    }
}
