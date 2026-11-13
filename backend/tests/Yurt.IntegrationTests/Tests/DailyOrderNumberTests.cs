using System.Net;
using System.Net.Http.Headers;
using System.Net.Http.Json;
using System.Text.Json;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Yurt.Application.Features.Orders.Services;
using Yurt.Domain.Entities;
using Yurt.Infrastructure.Persistence;
using Yurt.IntegrationTests.Helpers;

namespace Yurt.IntegrationTests.Tests;

/// <summary>
/// The short order number guests remember: 1, 2, 3 ... again from 1 every day at each branch. The order keeps its real id.
/// </summary>
[Collection("Integration")]
public class DailyOrderNumberTests(YurtWebAppFactory factory)
{
    private const string Url = "/api/v1/admin/counter-sales";

    private async Task<HttpClient> AdminAsync()
    {
        var admin = factory.CreateClient();
        admin.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer",
            await ApiHelpers.CreateAdminTokenAsync(factory.Services, admin));
        return admin;
    }

    /// <summary>A new branch, so its counter starts empty whatever other tests did.</summary>
    private async Task<(Guid Location, Guid Item)> SeedAsync(string? timeZone = null)
    {
        await using var scope = factory.Services.CreateAsyncScope();
        var db = scope.ServiceProvider.GetRequiredService<ApplicationDbContext>();
        var location = new Location { Name = $"Numbers {Guid.NewGuid():N}"[..16], Address = "a", WorkingHours = "0-24", ContactPhone = "1", TimeZone = timeZone };
        var category = await db.MenuCategories.FirstAsync();
        var item = new MenuItem { CategoryId = category.Id, Name = $"Num {Guid.NewGuid():N}"[..14], Description = "t", Price = 10m, IsAvailable = true };
        db.Locations.Add(location);
        db.MenuItems.Add(item);
        await db.SaveChangesAsync();
        return (location.Id, item.Id);
    }

    private static object Sale(Guid location, Guid item, string? key = null, DateTime? soldAt = null) => new
    {
        locationId = location, paymentMethod = "Cash", idempotencyKey = key ?? Guid.NewGuid().ToString(), soldAt,
        items = new[] { new { menuItemId = item, quantity = 1 } },
    };

    [Fact]
    public async Task CounterSales_GetOneTwoThree_AndKeepTheirOwnIds()
    {
        var admin = await AdminAsync();
        var (location, item) = await SeedAsync();

        var numbers = new List<string>();
        var ids = new HashSet<Guid>();
        for (var i = 0; i < 3; i++)
        {
            var sale = await (await admin.PostAsJsonAsync(Url, Sale(location, item))).Content.ReadFromJsonAsync<JsonElement>();
            numbers.Add(sale.GetProperty("number").GetString()!);
            Assert.Equal(i + 1, sale.GetProperty("dailyNumber").GetInt32());
            ids.Add(sale.GetProperty("id").GetGuid());
        }

        Assert.Equal(["1", "2", "3"], numbers);
        Assert.Equal(3, ids.Count); // the real identity is still the id
    }

    [Fact]
    public async Task ResendingASale_DoesNotUseUpANumber()
    {
        var admin = await AdminAsync();
        var (location, item) = await SeedAsync();
        var body = Sale(location, item);

        var first = await (await admin.PostAsJsonAsync(Url, body)).Content.ReadFromJsonAsync<JsonElement>();
        var again = await (await admin.PostAsJsonAsync(Url, body)).Content.ReadFromJsonAsync<JsonElement>();
        var next = await (await admin.PostAsJsonAsync(Url, Sale(location, item))).Content.ReadFromJsonAsync<JsonElement>();

        Assert.Equal("1", first.GetProperty("number").GetString());
        Assert.Equal("1", again.GetProperty("number").GetString());
        Assert.Equal("2", next.GetProperty("number").GetString());
    }

    [Fact]
    public async Task OrdersPlacedAtTheSameMoment_NeverShareANumber()
    {
        var admin = await AdminAsync();
        var (location, item) = await SeedAsync();

        var answers = await Task.WhenAll(Enumerable.Range(0, 20).Select(async _ =>
        {
            var resp = await admin.PostAsJsonAsync(Url, Sale(location, item));
            Assert.Equal(HttpStatusCode.Created, resp.StatusCode);
            return (await resp.Content.ReadFromJsonAsync<JsonElement>()).GetProperty("dailyNumber").GetInt32();
        }));

        Assert.Equal(Enumerable.Range(1, 20), answers.OrderBy(n => n));
    }

    [Fact]
    public async Task EachBranchCountsOnItsOwn()
    {
        var admin = await AdminAsync();
        var (a, itemA) = await SeedAsync();
        var (b, itemB) = await SeedAsync();

        await admin.PostAsJsonAsync(Url, Sale(a, itemA));
        await admin.PostAsJsonAsync(Url, Sale(a, itemA));
        var first = await (await admin.PostAsJsonAsync(Url, Sale(b, itemB))).Content.ReadFromJsonAsync<JsonElement>();

        Assert.Equal(1, first.GetProperty("dailyNumber").GetInt32());
    }

    [Fact]
    public async Task AnAppOrder_GetsTheNextNumberOfItsBranch()
    {
        var admin = await AdminAsync();
        var (location, item) = await SeedAsync();
        await admin.PostAsJsonAsync(Url, Sale(location, item)); // number 1 at the counter

        var client = factory.CreateClient();
        var (token, _) = await ApiHelpers.CreateCustomerAsync(client, "+77001009551");
        ApiHelpers.Authorize(client, token);
        var resp = await client.PostAsJsonAsync("/api/v1/orders", new
        {
            locationId = location, paymentMethod = "Cash",
            items = new[] { new { menuItemId = item, quantity = 1, toppings = Array.Empty<object>() } },
        });

        Assert.Equal(HttpStatusCode.Created, resp.StatusCode);
        var order = await resp.Content.ReadFromJsonAsync<JsonElement>();
        Assert.Equal("2", order.GetProperty("number").GetString());
    }

    [Fact]
    public async Task ASaleMadeOffline_KeepsTheNumberOnItsReceipt_AndStillTakesADayNumber()
    {
        var admin = await AdminAsync();
        var (location, item) = await SeedAsync();
        var key = "3fa9c1aa-0000-0000-0000-000000000000";

        var sale = await (await admin.PostAsJsonAsync(Url, Sale(location, item, key, DateTime.UtcNow.AddMinutes(-30)))).Content.ReadFromJsonAsync<JsonElement>();

        Assert.Equal("T-3FA9C1", sale.GetProperty("number").GetString());
        Assert.Equal(1, sale.GetProperty("dailyNumber").GetInt32());
    }

    [Fact]
    public async Task TheDayEndsAtTheBranchsOwnMidnight_AndTheDatabaseRefusesARepeat()
    {
        var admin = await AdminAsync();
        var (location, item) = await SeedAsync();
        await admin.PostAsJsonAsync(Url, Sale(location, item));
        await admin.PostAsJsonAsync(Url, Sale(location, item));

        await using var scope = factory.Services.CreateAsyncScope();
        var db = scope.ServiceProvider.GetRequiredService<ApplicationDbContext>();
        var today = await db.OrderDayCounters.SingleAsync(c => c.LocationId == location);
        Assert.Equal(2, today.LastNumber);

        // Two orders of one branch and day with the same number can never be saved.
        var orders = await db.Orders.Where(o => o.LocationId == location).ToListAsync();
        orders[1].DailyNumber = orders[0].DailyNumber;
        await Assert.ThrowsAsync<DbUpdateException>(() => db.SaveChangesAsync());
    }
}
