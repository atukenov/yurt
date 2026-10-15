using System.Net;
using System.Net.Http.Json;
using System.Text.Json;
using Yurt.IntegrationTests.Helpers;

namespace Yurt.IntegrationTests.Tests;

/// <summary>
/// Group-cart integration tests: multiple members add items, totals aggregate,
/// only the creator can check out, resulting order total is correct.
/// </summary>
[Collection("Integration")]
public class GroupCartTests(YurtWebAppFactory factory)
{
    private static readonly Guid LocationId = Guid.Parse("11111111-0000-0000-0000-000000000001");
    private static readonly Guid ExtraShotId = Guid.Parse("33333333-0000-0000-0000-000000000001");

    [Fact]
    public async Task GroupCart_TwoMembers_CheckoutTotalAggregatesAllItems()
    {
        // Member A: Espresso $2.50 * 1  = $2.50
        // Member B: Americano $3.00 * 1 + Extra Shot $0.75 = $3.75
        // Expected order total = $6.25

        var clientA = factory.CreateClient();
        var clientB = factory.CreateClient();

        var (tokenA, _) = await ApiHelpers.CreateCustomerAsync(clientA, "+77003000001");
        var (tokenB, _) = await ApiHelpers.CreateCustomerAsync(clientB, "+77003000002");

        var (espId, espPrice, espName) = await ApiHelpers.GetMenuItemAsync(factory.Services, "Espresso");
        var (amId,  amPrice,  amName)  = await ApiHelpers.GetMenuItemAsync(factory.Services, "Americano");

        // A creates the group cart
        ApiHelpers.Authorize(clientA, tokenA);
        var createResp = await clientA.PostAsJsonAsync("/api/v1/group-orders", new { locationId = LocationId });
        Assert.Equal(HttpStatusCode.Created, createResp.StatusCode);

        var cart = await createResp.Content.ReadFromJsonAsync<ApiHelpers.GroupCartResult>(ApiHelpers.JsonOpts);
        Assert.NotNull(cart);
        var cartId = cart.Id;
        var code   = cart.Code;

        // B joins
        ApiHelpers.Authorize(clientB, tokenB);
        var joinResp = await clientB.PostAsJsonAsync("/api/v1/group-orders/join", new { code });
        Assert.Equal(HttpStatusCode.OK, joinResp.StatusCode);

        // A adds Espresso (no toppings)
        ApiHelpers.Authorize(clientA, tokenA);
        var addA = await clientA.PostAsJsonAsync($"/api/v1/group-orders/{cartId}/items", new
        {
            menuItemId   = espId,
            menuItemName = espName,
            unitPrice    = espPrice,
            quantity     = 1,
            toppings     = Array.Empty<object>(),
            notes        = (string?)null
        });
        Assert.Equal(HttpStatusCode.OK, addA.StatusCode);

        // B adds Americano with Extra Shot
        ApiHelpers.Authorize(clientB, tokenB);
        var addB = await clientB.PostAsJsonAsync($"/api/v1/group-orders/{cartId}/items", new
        {
            menuItemId   = amId,
            menuItemName = amName,
            unitPrice    = amPrice,
            quantity     = 1,
            toppings     = new[] { new { toppingId = ExtraShotId, toppingName = "Extra Espresso Shot", price = 0.75m } },
            notes        = (string?)null
        });
        Assert.Equal(HttpStatusCode.OK, addB.StatusCode);

        // A (creator) checks out
        ApiHelpers.Authorize(clientA, tokenA);
        var checkoutResp = await clientA.PostAsync($"/api/v1/group-orders/{cartId}/checkout", null);
        Assert.Equal(HttpStatusCode.Created, checkoutResp.StatusCode);

        var order = await checkoutResp.Content.ReadFromJsonAsync<ApiHelpers.OrderResult>(ApiHelpers.JsonOpts);
        Assert.NotNull(order);
        Assert.Equal(6.25m, order.Total);
    }

    [Fact]
    public async Task GroupCart_NonCreator_CannotCheckout()
    {
        var clientA = factory.CreateClient();
        var clientB = factory.CreateClient();

        var (tokenA, _) = await ApiHelpers.CreateCustomerAsync(clientA, "+77003000003");
        var (tokenB, _) = await ApiHelpers.CreateCustomerAsync(clientB, "+77003000004");

        // A creates
        ApiHelpers.Authorize(clientA, tokenA);
        var createResp = await clientA.PostAsJsonAsync("/api/v1/group-orders", new { locationId = LocationId });
        var cart       = await createResp.Content.ReadFromJsonAsync<ApiHelpers.GroupCartResult>(ApiHelpers.JsonOpts);
        var cartId     = cart!.Id;
        var code       = cart.Code;

        // B joins
        ApiHelpers.Authorize(clientB, tokenB);
        await clientB.PostAsJsonAsync("/api/v1/group-orders/join", new { code });

        // B tries to checkout — should be forbidden
        var checkoutResp = await clientB.PostAsync($"/api/v1/group-orders/{cartId}/checkout", null);
        Assert.Equal(HttpStatusCode.Forbidden, checkoutResp.StatusCode);
    }

    [Fact]
    public async Task GroupCart_Member_CanRemoveOwnItem()
    {
        var clientA = factory.CreateClient();
        var clientB = factory.CreateClient();

        var (tokenA, _) = await ApiHelpers.CreateCustomerAsync(clientA, "+77003000005");
        var (tokenB, _) = await ApiHelpers.CreateCustomerAsync(clientB, "+77003000006");

        var (espId, espPrice, espName) = await ApiHelpers.GetMenuItemAsync(factory.Services, "Espresso");

        ApiHelpers.Authorize(clientA, tokenA);
        var createResp = await clientA.PostAsJsonAsync("/api/v1/group-orders", new { locationId = LocationId });
        var cart   = await createResp.Content.ReadFromJsonAsync<ApiHelpers.GroupCartResult>(ApiHelpers.JsonOpts);
        var cartId = cart!.Id;
        var code   = cart.Code;

        ApiHelpers.Authorize(clientB, tokenB);
        await clientB.PostAsJsonAsync("/api/v1/group-orders/join", new { code });

        // B adds an item; endpoint returns the full cart DTO — extract the item id from Items
        var addB = await clientB.PostAsJsonAsync($"/api/v1/group-orders/{cartId}/items", new
        {
            menuItemId = espId, menuItemName = espName, unitPrice = espPrice,
            quantity = 1, toppings = Array.Empty<object>(), notes = (string?)null
        });
        Assert.Equal(HttpStatusCode.OK, addB.StatusCode);
        var cartAfterAdd = await addB.Content.ReadFromJsonAsync<ApiHelpers.GroupCartResult>(ApiHelpers.JsonOpts);
        var itemId = cartAfterAdd!.Items.Last().Id;

        // B removes their own item; endpoint returns updated cart (200 OK)
        var deleteResp = await clientB.DeleteAsync($"/api/v1/group-orders/{cartId}/items/{itemId}");
        Assert.Equal(HttpStatusCode.OK, deleteResp.StatusCode);
    }

    [Fact]
    public async Task GroupCart_Member_CannotRemoveOtherMembersItem()
    {
        var clientA = factory.CreateClient();
        var clientB = factory.CreateClient();

        var (tokenA, _) = await ApiHelpers.CreateCustomerAsync(clientA, "+77003000007");
        var (tokenB, _) = await ApiHelpers.CreateCustomerAsync(clientB, "+77003000008");

        var (espId, espPrice, espName) = await ApiHelpers.GetMenuItemAsync(factory.Services, "Espresso");

        ApiHelpers.Authorize(clientA, tokenA);
        var createResp = await clientA.PostAsJsonAsync("/api/v1/group-orders", new { locationId = LocationId });
        var cart   = await createResp.Content.ReadFromJsonAsync<ApiHelpers.GroupCartResult>(ApiHelpers.JsonOpts);
        var cartId = cart!.Id;
        var code   = cart.Code;

        // A adds an item; endpoint returns the full cart DTO — extract the item id from Items
        var addA = await clientA.PostAsJsonAsync($"/api/v1/group-orders/{cartId}/items", new
        {
            menuItemId = espId, menuItemName = espName, unitPrice = espPrice,
            quantity = 1, toppings = Array.Empty<object>(), notes = (string?)null
        });
        var cartAfterAddA = await addA.Content.ReadFromJsonAsync<ApiHelpers.GroupCartResult>(ApiHelpers.JsonOpts);
        var itemId = cartAfterAddA!.Items.Last().Id;

        // B joins, then tries to delete A's item — the API returns Forbidden
        ApiHelpers.Authorize(clientB, tokenB);
        await clientB.PostAsJsonAsync("/api/v1/group-orders/join", new { code });
        var deleteResp = await clientB.DeleteAsync($"/api/v1/group-orders/{cartId}/items/{itemId}");
        Assert.Equal(HttpStatusCode.Forbidden, deleteResp.StatusCode);
    }
}
