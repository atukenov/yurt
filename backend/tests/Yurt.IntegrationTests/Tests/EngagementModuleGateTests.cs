using System.Net;
using System.Net.Http.Headers;
using System.Net.Http.Json;
using System.Text.Json;
using Yurt.Domain;
using Yurt.Domain.Enums;
using Yurt.IntegrationTests.Helpers;

namespace Yurt.IntegrationTests.Tests;

[Collection("Integration")]
public class EngagementModuleGateTests(YurtWebAppFactory factory)
{
    private static object License(bool enabled)
        => new { enabled, validUntil = (DateTime?)null, source = "Manual", notes = (string?)null };

    [Fact]
    public async Task Engagement_SwitchedOff_BlocksRating_ButOrdersStillWork()
    {
        var super = factory.CreateClient();
        super.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer",
            await ApiHelpers.CreateAdminTokenAsync(factory.Services, super,
                username: "platform-owner", password: "PlatformOwner@123", role: AdminRole.SuperAdmin));

        var customer = factory.CreateClient();
        var (token, _) = await ApiHelpers.CreateCustomerAsync(customer, "+77002990003");
        ApiHelpers.Authorize(customer, token);
        var missingOrder = Guid.NewGuid();
        var rating = new { rating = 5, comment = "great" };

        // On: the request reaches the service (unknown order), not the gate
        Assert.Equal(HttpStatusCode.NotFound,
            (await customer.PostAsJsonAsync($"/api/v1/orders/{missingOrder}/rating", rating)).StatusCode);

        try
        {
            await super.PutAsJsonAsync($"/api/v1/platform/modules/{ModuleKeys.Engagement}", License(false));

            var blocked = await customer.PostAsJsonAsync($"/api/v1/orders/{missingOrder}/rating", rating);
            Assert.Equal(HttpStatusCode.Forbidden, blocked.StatusCode);
            var body = await blocked.Content.ReadFromJsonAsync<JsonElement>();
            Assert.Equal("module_disabled", body.GetProperty("code").GetString());
            Assert.Equal(ModuleKeys.Engagement, body.GetProperty("module").GetString());

            // Ordering (core) is unaffected
            Assert.Equal(HttpStatusCode.OK, (await customer.GetAsync("/api/v1/orders/history")).StatusCode);
        }
        finally
        {
            await super.PutAsJsonAsync($"/api/v1/platform/modules/{ModuleKeys.Engagement}", License(true));
        }

        Assert.Equal(HttpStatusCode.NotFound,
            (await customer.PostAsJsonAsync($"/api/v1/orders/{missingOrder}/rating", rating)).StatusCode);
    }
}
