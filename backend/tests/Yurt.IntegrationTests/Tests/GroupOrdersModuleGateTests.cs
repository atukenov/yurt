using System.Net;
using System.Net.Http.Headers;
using System.Net.Http.Json;
using System.Text.Json;
using Yurt.Domain;
using Yurt.Domain.Enums;
using Yurt.IntegrationTests.Helpers;

namespace Yurt.IntegrationTests.Tests;

[Collection("Integration")]
public class GroupOrdersModuleGateTests(YurtWebAppFactory factory)
{
    private static readonly Guid LocationId = Guid.Parse("11111111-0000-0000-0000-000000000001");

    private static object License(bool enabled)
        => new { enabled, validUntil = (DateTime?)null, source = "Manual", notes = (string?)null };

    [Fact]
    public async Task GroupOrders_SwitchedOff_Returns403ModuleDisabled_ThenWorksAgain()
    {
        var super = factory.CreateClient();
        super.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer",
            await ApiHelpers.CreateAdminTokenAsync(factory.Services, super,
                username: "platform-owner", password: "PlatformOwner@123", role: AdminRole.SuperAdmin));

        var customer = factory.CreateClient();
        var (token, _) = await ApiHelpers.CreateCustomerAsync(customer, "+77002990002");
        ApiHelpers.Authorize(customer, token);

        Assert.True((await customer.PostAsJsonAsync("/api/v1/group-orders", new { locationId = LocationId })).IsSuccessStatusCode);

        try
        {
            await super.PutAsJsonAsync($"/api/v1/platform/modules/{ModuleKeys.GroupOrders}", License(false));

            var blocked = await customer.PostAsJsonAsync("/api/v1/group-orders", new { locationId = LocationId });
            Assert.Equal(HttpStatusCode.Forbidden, blocked.StatusCode);
            var body = await blocked.Content.ReadFromJsonAsync<JsonElement>();
            Assert.Equal("module_disabled", body.GetProperty("code").GetString());
            Assert.Equal(ModuleKeys.GroupOrders, body.GetProperty("module").GetString());
            Assert.Equal(HttpStatusCode.Forbidden,
                (await customer.PostAsJsonAsync("/api/v1/group-orders/join", new { code = "ABC123" })).StatusCode);

            // Regular ordering (core) is unaffected
            Assert.NotEqual(HttpStatusCode.Forbidden, (await customer.GetAsync("/api/v1/orders")).StatusCode);
        }
        finally
        {
            await super.PutAsJsonAsync($"/api/v1/platform/modules/{ModuleKeys.GroupOrders}", License(true));
        }

        Assert.True((await customer.PostAsJsonAsync("/api/v1/group-orders", new { locationId = LocationId })).IsSuccessStatusCode);
    }
}
