using System.Net;
using System.Net.Http.Headers;
using System.Net.Http.Json;
using System.Text.Json;
using Yurt.Domain;
using Yurt.Domain.Enums;
using Yurt.IntegrationTests.Helpers;

namespace Yurt.IntegrationTests.Tests;

[Collection("Integration")]
public class MarketingModuleGateTests(YurtWebAppFactory factory)
{
    private static readonly Guid LocationId = Guid.Parse("11111111-0000-0000-0000-000000000001");

    private static object License(bool enabled)
        => new { enabled, validUntil = (DateTime?)null, source = "Manual", notes = (string?)null };

    [Fact]
    public async Task Marketing_SwitchedOff_BlocksPromotionsAndCodes_ButOrdersStillWork()
    {
        var admin = factory.CreateClient();
        admin.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer",
            await ApiHelpers.CreateAdminTokenAsync(factory.Services, admin));
        var super = factory.CreateClient();
        super.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer",
            await ApiHelpers.CreateAdminTokenAsync(factory.Services, super,
                username: "platform-owner", password: "PlatformOwner@123", role: AdminRole.SuperAdmin));

        await ApiHelpers.SeedDiscountCodeAsync(factory.Services, "MKTGATE10",
            DiscountType.Percentage, 10m, expiresAt: DateTime.UtcNow.AddYears(1));
        var customer = factory.CreateClient();
        var (token, _) = await ApiHelpers.CreateCustomerAsync(customer, "+77002990001");
        ApiHelpers.Authorize(customer, token);
        var (itemId, _, _) = await ApiHelpers.GetMenuItemAsync(factory.Services, "Espresso");

        Assert.Equal(HttpStatusCode.OK, (await admin.GetAsync("/api/v1/admin/promotions")).StatusCode);
        Assert.Equal(HttpStatusCode.OK, (await customer.GetAsync("/api/v1/promotions/active")).StatusCode);

        try
        {
            await super.PutAsJsonAsync($"/api/v1/platform/modules/{ModuleKeys.Marketing}", License(false));

            foreach (var url in new[] { "/api/v1/admin/promotions", "/api/v1/admin/discount-codes", "/api/v1/promotions/active" })
            {
                var r = await admin.GetAsync(url);
                Assert.Equal(HttpStatusCode.Forbidden, r.StatusCode);
                var body = await r.Content.ReadFromJsonAsync<JsonElement>();
                Assert.Equal("module_disabled", body.GetProperty("code").GetString());
                Assert.Equal(ModuleKeys.Marketing, body.GetProperty("module").GetString());
            }
            Assert.Equal(HttpStatusCode.Forbidden,
                (await customer.PostAsJsonAsync("/api/v1/discount-codes/validate", new { code = "MKTGATE10", subtotal = 2.5m })).StatusCode);

            // Checkout still works; the code is ignored
            var resp = await customer.PostAsJsonAsync("/api/v1/orders", new
            {
                locationId = LocationId, paymentMethod = "Cash", discountCode = "MKTGATE10",
                items = new[] { new { menuItemId = itemId, quantity = 1, toppings = Array.Empty<object>() } }
            });
            Assert.Equal(HttpStatusCode.Created, resp.StatusCode);
            var order = await resp.Content.ReadFromJsonAsync<ApiHelpers.OrderResult>(ApiHelpers.JsonOpts);
            Assert.Equal(0m, order!.DiscountAmount);
            Assert.Equal(order.Subtotal, order.Total);
        }
        finally
        {
            await super.PutAsJsonAsync($"/api/v1/platform/modules/{ModuleKeys.Marketing}", License(true));
        }

        Assert.Equal(HttpStatusCode.OK, (await admin.GetAsync("/api/v1/admin/promotions")).StatusCode);
    }
}
