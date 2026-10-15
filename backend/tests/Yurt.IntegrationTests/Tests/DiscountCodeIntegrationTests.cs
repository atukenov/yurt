using System.Net;
using System.Net.Http.Json;
using Yurt.Domain.Enums;
using Yurt.IntegrationTests.Helpers;

namespace Yurt.IntegrationTests.Tests;

/// <summary>
/// Integration tests for discount code application at order checkout.
/// Codes are seeded directly into the DB to control StartsAt/ExpiresAt precisely.
/// </summary>
[Collection("Integration")]
public class DiscountCodeIntegrationTests(YurtWebAppFactory factory)
{
    private static readonly Guid LocationId = Guid.Parse("11111111-0000-0000-0000-000000000001");

    // ------------------------------------------------------------------
    // Helper: place an order for Espresso (2.50) with an optional discount code
    // ------------------------------------------------------------------
    private async Task<(HttpStatusCode status, ApiHelpers.OrderResult? order)> PlaceOrderAsync(
        HttpClient client, Guid itemId, string? discountCode = null)
    {
        var body = new
        {
            locationId    = LocationId,
            paymentMethod = "Cash",
            discountCode,
            items         = new[] { new { menuItemId = itemId, quantity = 1, toppings = Array.Empty<object>() } }
        };
        var resp = await client.PostAsJsonAsync("/api/v1/orders", body);
        var order = resp.IsSuccessStatusCode
            ? await resp.Content.ReadFromJsonAsync<ApiHelpers.OrderResult>(ApiHelpers.JsonOpts)
            : null;
        return ((HttpStatusCode)resp.StatusCode, order);
    }

    [Fact]
    public async Task ValidPercentageCode_ReducesTotal()
    {
        // 10% off Espresso $2.50 → discount $0.25 → total $2.25
        await ApiHelpers.SeedDiscountCodeAsync(factory.Services, "VALID10PCT",
            DiscountType.Percentage, 10m, expiresAt: DateTime.UtcNow.AddYears(1));

        var client = factory.CreateClient();
        var (token, _) = await ApiHelpers.CreateCustomerAsync(client, "+77002000001");
        ApiHelpers.Authorize(client, token);
        var (itemId, _, _) = await ApiHelpers.GetMenuItemAsync(factory.Services, "Espresso");

        var (status, order) = await PlaceOrderAsync(client, itemId, "VALID10PCT");

        Assert.Equal(HttpStatusCode.Created, status);
        Assert.NotNull(order);
        Assert.Equal(2.50m, order.Subtotal);
        Assert.Equal(0.25m, order.DiscountAmount);
        Assert.Equal(2.25m, order.Total);
    }

    [Fact]
    public async Task ValidFixedCode_ReducesTotal()
    {
        // $1 off Espresso $2.50 → total $1.50
        await ApiHelpers.SeedDiscountCodeAsync(factory.Services, "FIXED1OFF",
            DiscountType.FixedAmount, 1.00m, expiresAt: DateTime.UtcNow.AddYears(1));

        var client = factory.CreateClient();
        var (token, _) = await ApiHelpers.CreateCustomerAsync(client, "+77002000002");
        ApiHelpers.Authorize(client, token);
        var (itemId, _, _) = await ApiHelpers.GetMenuItemAsync(factory.Services, "Espresso");

        var (status, order) = await PlaceOrderAsync(client, itemId, "FIXED1OFF");

        Assert.Equal(HttpStatusCode.Created, status);
        Assert.NotNull(order);
        Assert.Equal(1.50m, order.Total);
    }

    [Fact]
    public async Task ExpiredCode_OrderSucceedsWithNoDiscount()
    {
        // Service silently ignores invalid/expired codes; order still goes through at full price
        await ApiHelpers.SeedDiscountCodeAsync(factory.Services, "EXPIRED10",
            DiscountType.Percentage, 10m, expiresAt: DateTime.UtcNow.AddDays(-1));

        var client = factory.CreateClient();
        var (token, _) = await ApiHelpers.CreateCustomerAsync(client, "+77002000003");
        ApiHelpers.Authorize(client, token);
        var (itemId, price, _) = await ApiHelpers.GetMenuItemAsync(factory.Services, "Espresso");

        var (status, order) = await PlaceOrderAsync(client, itemId, "EXPIRED10");

        Assert.Equal(HttpStatusCode.Created, status);
        Assert.NotNull(order);
        Assert.Equal(0m,    order.DiscountAmount);
        Assert.Equal(price, order.Total);
    }

    [Fact]
    public async Task NotYetActiveCode_OrderSucceedsWithNoDiscount()
    {
        await ApiHelpers.SeedDiscountCodeAsync(factory.Services, "FUTURE10",
            DiscountType.Percentage, 10m,
            startsAt: DateTime.UtcNow.AddDays(1), expiresAt: DateTime.UtcNow.AddYears(1));

        var client = factory.CreateClient();
        var (token, _) = await ApiHelpers.CreateCustomerAsync(client, "+77002000004");
        ApiHelpers.Authorize(client, token);
        var (itemId, price, _) = await ApiHelpers.GetMenuItemAsync(factory.Services, "Espresso");

        var (status, order) = await PlaceOrderAsync(client, itemId, "FUTURE10");

        Assert.Equal(HttpStatusCode.Created, status);
        Assert.NotNull(order);
        Assert.Equal(0m,    order.DiscountAmount);
        Assert.Equal(price, order.Total);
    }

    [Fact]
    public async Task MaxUsesReached_OrderSucceedsWithNoDiscount()
    {
        await ApiHelpers.SeedDiscountCodeAsync(factory.Services, "MAXUSE1",
            DiscountType.Percentage, 20m, maxUses: 1, expiresAt: DateTime.UtcNow.AddYears(1));

        // First use (by customer A) — consumes the only remaining use
        var clientA = factory.CreateClient();
        var (tokenA, _) = await ApiHelpers.CreateCustomerAsync(clientA, "+77002000005");
        ApiHelpers.Authorize(clientA, tokenA);
        var (itemId, price, _) = await ApiHelpers.GetMenuItemAsync(factory.Services, "Espresso");
        var (s1, o1) = await PlaceOrderAsync(clientA, itemId, "MAXUSE1");
        Assert.Equal(HttpStatusCode.Created, s1);
        Assert.True(o1!.DiscountAmount > 0); // applied on first use

        // Second use (by customer B) — limit reached, no discount
        var clientB = factory.CreateClient();
        var (tokenB, _) = await ApiHelpers.CreateCustomerAsync(clientB, "+77002000006");
        ApiHelpers.Authorize(clientB, tokenB);
        var (s2, o2) = await PlaceOrderAsync(clientB, itemId, "MAXUSE1");
        Assert.Equal(HttpStatusCode.Created, s2);
        Assert.Equal(0m, o2!.DiscountAmount);
        Assert.Equal(price, o2.Total);
    }

    [Fact]
    public async Task SameCustomerReusesCode_SecondOrderSucceedsWithNoDiscount()
    {
        await ApiHelpers.SeedDiscountCodeAsync(factory.Services, "ONEUSE10",
            DiscountType.Percentage, 10m, expiresAt: DateTime.UtcNow.AddYears(1));

        var client = factory.CreateClient();
        var (token, _) = await ApiHelpers.CreateCustomerAsync(client, "+77002000009");
        ApiHelpers.Authorize(client, token);
        var (itemId, price, _) = await ApiHelpers.GetMenuItemAsync(factory.Services, "Espresso");

        // First order — discount applies
        var (s1, o1) = await PlaceOrderAsync(client, itemId, "ONEUSE10");
        Assert.Equal(HttpStatusCode.Created, s1);
        Assert.True(o1!.DiscountAmount > 0);

        // Second order, same customer, same code — code already redeemed, no discount
        var (s2, o2) = await PlaceOrderAsync(client, itemId, "ONEUSE10");
        Assert.Equal(HttpStatusCode.Created, s2);
        Assert.Equal(0m, o2!.DiscountAmount);
        Assert.Equal(price, o2.Total);
    }

    [Fact]
    public async Task ValidateEndpoint_AlreadyUsedByCustomer_ReturnsInvalid()
    {
        await ApiHelpers.SeedDiscountCodeAsync(factory.Services, "PREVONCE",
            DiscountType.FixedAmount, 2m, expiresAt: DateTime.UtcNow.AddYears(1));

        var client = factory.CreateClient();
        var (token, _) = await ApiHelpers.CreateCustomerAsync(client, "+77002000010");
        ApiHelpers.Authorize(client, token);
        var (itemId, _, _) = await ApiHelpers.GetMenuItemAsync(factory.Services, "Espresso");

        // Consume the code via an order
        var (status, order) = await PlaceOrderAsync(client, itemId, "PREVONCE");
        Assert.Equal(HttpStatusCode.Created, status);
        Assert.True(order!.DiscountAmount > 0);

        // Previewing the same code again for this customer should now report invalid
        var resp = await client.PostAsJsonAsync("/api/v1/discount-codes/validate", new
        {
            code     = "PREVONCE",
            subtotal = 5m
        });

        Assert.Equal(HttpStatusCode.OK, resp.StatusCode);
        var result = await resp.Content.ReadFromJsonAsync<ApiHelpers.DiscountValidResult>(ApiHelpers.JsonOpts);
        Assert.NotNull(result);
        Assert.False(result.IsValid);
        Assert.Contains("already used", result.Message, StringComparison.OrdinalIgnoreCase);
    }

    [Fact]
    public async Task MinOrderAmountNotMet_ValidateEndpointRejectsCode()
    {
        // The /validate endpoint lets customers preview whether a code applies
        await ApiHelpers.SeedDiscountCodeAsync(factory.Services, "MIN50OFF",
            DiscountType.Percentage, 15m, minOrderAmount: 50m, expiresAt: DateTime.UtcNow.AddYears(1));

        var client = factory.CreateClient();
        var (token, _) = await ApiHelpers.CreateCustomerAsync(client, "+77002000007");
        ApiHelpers.Authorize(client, token);

        var resp = await client.PostAsJsonAsync("/api/v1/discount-codes/validate", new
        {
            code     = "MIN50OFF",
            subtotal = 10m   // below the $50 minimum
        });

        Assert.Equal(System.Net.HttpStatusCode.OK, resp.StatusCode);
        var result = await resp.Content.ReadFromJsonAsync<ApiHelpers.DiscountValidResult>(ApiHelpers.JsonOpts);
        Assert.NotNull(result);
        Assert.False(result.IsValid);
        Assert.Equal(0m, result.DiscountAmount);
    }

    [Fact]
    public async Task ValidateEndpoint_ValidCode_ReturnsIsValidTrue()
    {
        await ApiHelpers.SeedDiscountCodeAsync(factory.Services, "PREVIEWOK",
            DiscountType.FixedAmount, 2m, expiresAt: DateTime.UtcNow.AddYears(1));

        var client = factory.CreateClient();
        var (token, _) = await ApiHelpers.CreateCustomerAsync(client, "+77002000008");
        ApiHelpers.Authorize(client, token);

        var resp = await client.PostAsJsonAsync("/api/v1/discount-codes/validate", new
        {
            code     = "PREVIEWOK",
            subtotal = 5m
        });

        Assert.Equal(System.Net.HttpStatusCode.OK, resp.StatusCode);
        var result = await resp.Content.ReadFromJsonAsync<ApiHelpers.DiscountValidResult>(ApiHelpers.JsonOpts);
        Assert.NotNull(result);
        Assert.True(result.IsValid);
        Assert.Equal(2m, result.DiscountAmount);
    }
}
