using System.Net;
using System.Net.Http.Json;
using Yurt.IntegrationTests.Helpers;

namespace Yurt.IntegrationTests.Tests;

/// <summary>
/// A guest can ask for a pickup time when ordering. It is kept on the order (UTC) and shown back; a time that has
/// already passed or is more than a day away is refused. No time = as soon as possible, like before.
/// </summary>
[Collection("Integration")]
public class PickupTimeTests(YurtWebAppFactory factory)
{
    private static readonly Guid LocationId = Guid.Parse("11111111-0000-0000-0000-000000000001");

    private record PickupOrder(Guid Id, DateTime? PickupAt);

    private async Task<HttpResponseMessage> PlaceAsync(string phone, DateTime? pickupAt)
    {
        var client = factory.CreateClient();
        var (token, _) = await ApiHelpers.CreateCustomerAsync(client, phone);
        ApiHelpers.Authorize(client, token);
        var (itemId, _, _) = await ApiHelpers.GetMenuItemAsync(factory.Services, "Espresso");

        return await client.PostAsJsonAsync("/api/v1/orders", new
        {
            locationId = LocationId,
            paymentMethod = "Cash",
            pickupAt,
            items = new[] { new { menuItemId = itemId, quantity = 1, toppings = Array.Empty<object>() } },
        });
    }

    [Fact]
    public async Task PlaceOrder_WithPickupTime_KeepsIt()
    {
        var wanted = DateTime.UtcNow.AddMinutes(45);
        wanted = new DateTime(wanted.Year, wanted.Month, wanted.Day, wanted.Hour, wanted.Minute, 0, DateTimeKind.Utc);

        var resp = await PlaceAsync("+77001009001", wanted);

        Assert.Equal(HttpStatusCode.Created, resp.StatusCode);
        var order = await resp.Content.ReadFromJsonAsync<PickupOrder>(ApiHelpers.JsonOpts);
        Assert.Equal(wanted, order!.PickupAt?.ToUniversalTime());
    }

    [Fact]
    public async Task PlaceOrder_WithoutPickupTime_IsAsSoonAsPossible()
    {
        var resp = await PlaceAsync("+77001009002", null);

        Assert.Equal(HttpStatusCode.Created, resp.StatusCode);
        var order = await resp.Content.ReadFromJsonAsync<PickupOrder>(ApiHelpers.JsonOpts);
        Assert.Null(order!.PickupAt);
    }

    [Fact]
    public async Task PlaceOrder_PickupTimeInThePast_Returns400()
    {
        var resp = await PlaceAsync("+77001009003", DateTime.UtcNow.AddMinutes(-10));
        Assert.Equal(HttpStatusCode.BadRequest, resp.StatusCode);
    }

    [Fact]
    public async Task PlaceOrder_PickupTimeMoreThanADayAhead_Returns400()
    {
        var resp = await PlaceAsync("+77001009004", DateTime.UtcNow.AddHours(30));
        Assert.Equal(HttpStatusCode.BadRequest, resp.StatusCode);
    }
}
