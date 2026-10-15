using System.Net.Http.Json;
using Yurt.IntegrationTests.Helpers;

namespace Yurt.IntegrationTests.Tests;

/// <summary>
/// Verifies that order totals are computed correctly for items with variants and toppings.
///
/// Pricing rules (from OrderService):
///   unitPrice  = variantPrice ?? menuItemPrice   (variant overrides base, not additive)
///   unitPrice += sum(toppingPrices)
///   lineTotal  = unitPrice * quantity
///   orderTotal = sum(lineTotals) - discountAmount
/// </summary>
[Collection("Integration")]
public class OrderTotalTests(YurtWebAppFactory factory)
{
    private static readonly Guid LocationId = Guid.Parse("11111111-0000-0000-0000-000000000001");

    // Seeded topping fixed GUIDs
    private static readonly Guid ExtraShotId   = Guid.Parse("33333333-0000-0000-0000-000000000001");
    private static readonly Guid VanillaSyrupId = Guid.Parse("33333333-0000-0000-0000-000000000002");

    [Fact]
    public async Task PlaceOrder_ItemWithVariantAndToppings_TotalMatchesExpected()
    {
        // Espresso base $2.50 → Large variant $3.50
        // Extra Shot $0.75, Vanilla Syrup $0.50
        // unitPrice = $3.50 + $0.75 + $0.50 = $4.75  (variant overrides base)
        // lineTotal = $4.75 * 2 = $9.50

        var client = factory.CreateClient();
        var (token, _) = await ApiHelpers.CreateCustomerAsync(client, "+77001000001");
        ApiHelpers.Authorize(client, token);

        var (itemId, _, _) = await ApiHelpers.GetMenuItemAsync(factory.Services, "Espresso");
        var variantId      = await ApiHelpers.AddVariantAsync(factory.Services, itemId, "Large", 3.50m);

        var resp = await client.PostAsJsonAsync("/api/v1/orders", new
        {
            locationId    = LocationId,
            paymentMethod = "Cash",
            items         = new[]
            {
                new
                {
                    menuItemId = itemId,
                    quantity   = 2,
                    variantId,
                    toppings   = new[]
                    {
                        new { toppingId = ExtraShotId,   toppingName = "Extra Espresso Shot", price = 0.75m },
                        new { toppingId = VanillaSyrupId, toppingName = "Vanilla Syrup",       price = 0.50m },
                    }
                }
            }
        });

        Assert.Equal(System.Net.HttpStatusCode.Created, resp.StatusCode);
        var order = await resp.Content.ReadFromJsonAsync<ApiHelpers.OrderResult>(ApiHelpers.JsonOpts);
        Assert.NotNull(order);
        Assert.Equal(9.50m, order.Subtotal);
        Assert.Equal(0m,    order.DiscountAmount);
        Assert.Equal(9.50m, order.Total);
    }

    [Fact]
    public async Task PlaceOrder_ItemWithoutVariant_UsesBasePrice()
    {
        // Cappuccino base $3.75, no variant, no toppings, qty 1
        // lineTotal = $3.75

        var client = factory.CreateClient();
        var (token, _) = await ApiHelpers.CreateCustomerAsync(client, "+77001000002");
        ApiHelpers.Authorize(client, token);

        var (itemId, price, _) = await ApiHelpers.GetMenuItemAsync(factory.Services, "Cappuccino");

        var resp = await client.PostAsJsonAsync("/api/v1/orders", new
        {
            locationId    = LocationId,
            paymentMethod = "Cash",
            items         = new[] { new { menuItemId = itemId, quantity = 1, toppings = Array.Empty<object>() } }
        });

        Assert.Equal(System.Net.HttpStatusCode.Created, resp.StatusCode);
        var order = await resp.Content.ReadFromJsonAsync<ApiHelpers.OrderResult>(ApiHelpers.JsonOpts);
        Assert.NotNull(order);
        Assert.Equal(price,  order.Subtotal);
        Assert.Equal(price,  order.Total);
    }

    [Fact]
    public async Task PlaceOrder_MultipleItems_SubtotalSumsLineTotals()
    {
        // Espresso $2.50 * 1 + Americano $3.00 * 2 = $8.50

        var client = factory.CreateClient();
        var (token, _) = await ApiHelpers.CreateCustomerAsync(client, "+77001000003");
        ApiHelpers.Authorize(client, token);

        var (espId, _, _) = await ApiHelpers.GetMenuItemAsync(factory.Services, "Espresso");
        var (amId,  _, _) = await ApiHelpers.GetMenuItemAsync(factory.Services, "Americano");

        var resp = await client.PostAsJsonAsync("/api/v1/orders", new
        {
            locationId    = LocationId,
            paymentMethod = "Cash",
            items         = new[]
            {
                new { menuItemId = espId, quantity = 1, toppings = Array.Empty<object>() },
                new { menuItemId = amId,  quantity = 2, toppings = Array.Empty<object>() },
            }
        });

        Assert.Equal(System.Net.HttpStatusCode.Created, resp.StatusCode);
        var order = await resp.Content.ReadFromJsonAsync<ApiHelpers.OrderResult>(ApiHelpers.JsonOpts);
        Assert.NotNull(order);
        Assert.Equal(8.50m, order.Subtotal);
        Assert.Equal(8.50m, order.Total);
    }

    [Fact]
    public async Task PlaceOrder_EmptyItems_Returns400()
    {
        var client = factory.CreateClient();
        var (token, _) = await ApiHelpers.CreateCustomerAsync(client, "+77001000004");
        ApiHelpers.Authorize(client, token);

        var resp = await client.PostAsJsonAsync("/api/v1/orders", new
        {
            locationId    = LocationId,
            paymentMethod = "Cash",
            items         = Array.Empty<object>()
        });

        Assert.Equal(System.Net.HttpStatusCode.BadRequest, resp.StatusCode);
    }

    [Fact]
    public async Task PlaceOrder_InactiveLocation_Returns400()
    {
        // Location 3 is inactive in the seed data
        var inactiveLocationId = Guid.Parse("11111111-0000-0000-0000-000000000003");

        var client = factory.CreateClient();
        var (token, _) = await ApiHelpers.CreateCustomerAsync(client, "+77001000005");
        ApiHelpers.Authorize(client, token);

        var (itemId, _, _) = await ApiHelpers.GetMenuItemAsync(factory.Services, "Espresso");

        var resp = await client.PostAsJsonAsync("/api/v1/orders", new
        {
            locationId    = inactiveLocationId,
            paymentMethod = "Cash",
            items         = new[] { new { menuItemId = itemId, quantity = 1, toppings = Array.Empty<object>() } }
        });

        Assert.Equal(System.Net.HttpStatusCode.BadRequest, resp.StatusCode);
    }
}
