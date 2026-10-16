using System.Net;
using System.Net.Http.Headers;
using System.Net.Http.Json;
using System.Text.Json;
using Yurt.Domain;
using Yurt.Domain.Enums;
using Yurt.IntegrationTests.Helpers;

namespace Yurt.IntegrationTests.Tests;

[Collection("Integration")]
public class OnlinePaymentsModuleGateTests(YurtWebAppFactory factory)
{
    private static object License(bool enabled)
        => new { enabled, validUntil = (DateTime?)null, source = "Manual", notes = (string?)null };

    [Fact]
    public async Task OnlinePayments_SwitchedOff_BlocksNewPayments_AndHidesApplePay()
    {
        var super = factory.CreateClient();
        super.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer",
            await ApiHelpers.CreateAdminTokenAsync(factory.Services, super,
                username: "platform-owner", password: "PlatformOwner@123", role: AdminRole.SuperAdmin));
        var customer = factory.CreateClient();
        var (token, _) = await ApiHelpers.CreateCustomerAsync(customer, "+77002990005");
        ApiHelpers.Authorize(customer, token);

        try
        {
            await super.PutAsJsonAsync($"/api/v1/platform/modules/{ModuleKeys.OnlinePayments}", License(false));

            var create = await customer.PostAsJsonAsync("/api/v1/payments/create",
                new { orderId = Guid.NewGuid(), provider = 0 });
            Assert.Equal(HttpStatusCode.Forbidden, create.StatusCode);
            var body = await create.Content.ReadFromJsonAsync<JsonElement>();
            Assert.Equal("module_disabled", body.GetProperty("code").GetString());
            Assert.Equal(ModuleKeys.OnlinePayments, body.GetProperty("module").GetString());

            // Apple Pay config answers "disabled" instead of an error, so the app hides the button
            var cfg = await customer.GetFromJsonAsync<JsonElement>("/api/v1/payments/apple-pay/config");
            Assert.False(cfg.GetProperty("enabled").GetBoolean());

            // Looking up an existing payment is not blocked (an unknown invoice is just 404)
            var status = await customer.GetAsync("/api/v1/payments/status/unknown-invoice");
            Assert.NotEqual(HttpStatusCode.Forbidden, status.StatusCode);
        }
        finally
        {
            await super.PutAsJsonAsync($"/api/v1/platform/modules/{ModuleKeys.OnlinePayments}", License(true));
        }

        var again = await customer.PostAsJsonAsync("/api/v1/payments/create",
            new { orderId = Guid.NewGuid(), provider = 0 });
        Assert.NotEqual(HttpStatusCode.Forbidden, again.StatusCode);
    }
}
