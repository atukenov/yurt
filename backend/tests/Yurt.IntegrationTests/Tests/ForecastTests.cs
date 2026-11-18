using System.Net;
using System.Net.Http.Json;
using System.Text.Json;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Yurt.Application.Common.Interfaces;
using Yurt.Domain.Entities;
using Yurt.Domain.Enums;
using Yurt.IntegrationTests.Helpers;

namespace Yurt.IntegrationTests.Tests;

/// <summary>
/// Sales forecast and prep plan: learns from the same weekday of past weeks (closed days left out, recent weeks count
/// more), splits the day into parts, turns dishes into ingredient amounts and sets them against the branch's stock.
/// </summary>
[Collection("Integration")]
public class ForecastTests(YurtWebAppFactory factory)
{
    private static readonly TimeZoneInfo Almaty = TimeZoneInfo.FindSystemTimeZoneById("Asia/Almaty");
    private static DateTime Local(DateTime day, int hour)
        => TimeZoneInfo.ConvertTimeToUtc(DateTime.SpecifyKind(day.Date.AddHours(hour), DateTimeKind.Unspecified), Almaty);

    private async Task<HttpClient> ClientAsync(AdminRole role)
    {
        var client = factory.CreateClient();
        var id = Guid.NewGuid().ToString("N")[..8];
        ApiHelpers.Authorize(client, await ApiHelpers.CreateAdminTokenAsync(factory.Services, client, $"fc-{id}", "Fc@1234567", role: role));
        return client;
    }

    [Fact]
    public async Task Forecast_AveragesTheSameWeekday_AndSetsNeedsAgainstStock()
    {
        var day = new DateTime(2200, 5, 14); // any day; the history is the same weekday of the 8 weeks before it
        Guid location, latte, beans;
        await using (var scope = factory.Services.CreateAsyncScope())
        {
            var db = scope.ServiceProvider.GetRequiredService<IApplicationDbContext>();
            var loc = new Location { Name = $"Forecast {Guid.NewGuid():N}"[..20], Address = "x", WorkingHours = "", ContactPhone = "", IsActive = true };
            var category = await db.MenuCategories.FirstAsync();
            var item = new MenuItem { CategoryId = category.Id, Name = "Forecast latte", Description = "t", Price = 1000m, IsAvailable = true };
            var ingredient = new Ingredient { Name = $"Forecast beans {Guid.NewGuid():N}"[..24], Unit = "g" };
            db.Locations.Add(loc); db.MenuItems.Add(item); db.Ingredients.Add(ingredient);
            await db.SaveChangesAsync();
            db.RecipeLines.Add(new RecipeLine { MenuItemId = item.Id, IngredientId = ingredient.Id, Quantity = 18m });
            db.IngredientStocks.Add(new IngredientStock { IngredientId = ingredient.Id, LocationId = loc.Id, Quantity = 50m });

            void Sale(DateTime at, int quantity, OrderStatus status = OrderStatus.Completed)
            {
                var order = new Order { LocationId = loc.Id, Status = status, CreatedAt = at, CompletedAt = at, Subtotal = 1000m * quantity, Total = 1000m * quantity };
                db.Orders.Add(order);
                db.OrderItems.Add(new OrderItem { Order = order, MenuItemId = item.Id, MenuItemName = "Forecast latte", Quantity = quantity, UnitPrice = 1000m, LineTotal = 1000m * quantity });
            }
            Sale(Local(day.AddDays(-7), 9), 4);          // last week: 4 in the morning...
            Sale(Local(day.AddDays(-7), 13), 2);         // ...2 at lunch
            Sale(Local(day.AddDays(-14), 9), 2);         // the week before: 2 in the morning
            // Three weeks ago the branch sold nothing: closed, so it is left out, not counted as a quiet day.
            Sale(Local(day.AddDays(-1), 9), 50);         // another weekday: ignored
            Sale(Local(day.AddDays(-7), 10), 30, OrderStatus.Declined); // declined: never made
            await db.SaveChangesAsync();
            (location, latte, beans) = (loc.Id, item.Id, ingredient.Id);
        }

        var worker = await ClientAsync(AdminRole.Worker); // item counts, no money: a Worker may look
        var response = await worker.GetAsync($"/api/v1/admin/forecast?locationId={location}&date=2200-05-14&buffer=0");
        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        var body = await response.Content.ReadFromJsonAsync<JsonElement>();

        Assert.Equal(2, body.GetProperty("daysUsed").GetInt32());
        Assert.Equal("low", body.GetProperty("confidence").GetString());
        var dish = body.GetProperty("dishes").EnumerateArray().Single();
        Assert.Equal(latte, dish.GetProperty("menuItemId").GetGuid());
        // Morning (4*8 + 2*7) / 15 = 3.07 -> 4; lunch (2*8) / 15 = 1.07 -> 2; the day 4.13 -> 5.
        Assert.Equal(4, dish.GetProperty("morning").GetInt32());
        Assert.Equal(2, dish.GetProperty("lunch").GetInt32());
        Assert.Equal(5, dish.GetProperty("expected").GetInt32());
        Assert.Equal(9, body.GetProperty("peakHour").GetInt32());

        // 18 g per latte: (6*18*8 + 2*18*7) / 15 = 74.4 g, and the branch has 50 g.
        var beansLine = body.GetProperty("ingredients").EnumerateArray().Single(i => i.GetProperty("ingredientId").GetGuid() == beans);
        Assert.Equal(74.4m, beansLine.GetProperty("needed").GetDecimal());
        Assert.Equal(50m, beansLine.GetProperty("inStock").GetDecimal());
        Assert.Equal(24.4m, beansLine.GetProperty("missing").GetDecimal());

        // A busy day (+50 %) and the default 10 % safety margin make every number bigger.
        var busy = await (await worker.GetAsync($"/api/v1/admin/forecast?locationId={location}&date=2200-05-14&adjust=50"))
            .Content.ReadFromJsonAsync<JsonElement>();
        var busyBeans = busy.GetProperty("ingredients").EnumerateArray().Single(i => i.GetProperty("ingredientId").GetGuid() == beans);
        Assert.Equal(122.76m, busyBeans.GetProperty("needed").GetDecimal()); // 74.4 * 1.5 * 1.1
    }

    [Fact]
    public async Task Forecast_WithNoHistory_IsEmptyAndHonestAboutIt()
    {
        Guid location;
        await using (var scope = factory.Services.CreateAsyncScope())
        {
            var db = scope.ServiceProvider.GetRequiredService<IApplicationDbContext>();
            var loc = new Location { Name = $"Forecast empty {Guid.NewGuid():N}"[..24], Address = "x", WorkingHours = "", ContactPhone = "", IsActive = true };
            db.Locations.Add(loc);
            await db.SaveChangesAsync();
            location = loc.Id;
        }
        var admin = await ClientAsync(AdminRole.Admin);
        var body = await (await admin.GetAsync($"/api/v1/admin/forecast?locationId={location}")).Content.ReadFromJsonAsync<JsonElement>();
        Assert.Equal(0, body.GetProperty("daysUsed").GetInt32());
        Assert.Equal("none", body.GetProperty("confidence").GetString());
        Assert.Empty(body.GetProperty("dishes").EnumerateArray());
    }

    [Fact]
    public async Task Forecast_RefusesBadInput()
    {
        var admin = await ClientAsync(AdminRole.Admin);
        Assert.Equal(HttpStatusCode.NotFound, (await admin.GetAsync($"/api/v1/admin/forecast?locationId={Guid.NewGuid()}")).StatusCode);
        Assert.Equal((HttpStatusCode)422, (await admin.GetAsync($"/api/v1/admin/forecast?locationId={Guid.NewGuid()}&date=tomorrow")).StatusCode);
        Assert.Equal((HttpStatusCode)422, (await admin.GetAsync($"/api/v1/admin/forecast?locationId={Guid.NewGuid()}&adjust=500")).StatusCode);
        Assert.Equal(HttpStatusCode.Unauthorized, (await factory.CreateClient().GetAsync($"/api/v1/admin/forecast?locationId={Guid.NewGuid()}")).StatusCode);
    }
}
