using System.Net;
using System.Net.Http.Headers;
using System.Net.Http.Json;
using System.Text.Json;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Yurt.Domain.Entities;
using Yurt.Infrastructure.Persistence;
using Yurt.IntegrationTests.Helpers;

namespace Yurt.IntegrationTests.Tests;

/// <summary>Stations (Kitchen/Bar) and routing of menu categories to them (own_pos, Phase 6).</summary>
[Collection("Integration")]
public class StationTests(YurtWebAppFactory factory)
{
    [Fact]
    public async Task Categories_RouteToStations_AndRoutingListsTheStationOfEachItem()
    {
        Guid categoryId, itemId;
        await using (var scope = factory.Services.CreateAsyncScope())
        {
            var db = scope.ServiceProvider.GetRequiredService<ApplicationDbContext>();
            var category = new MenuCategory { Name = $"Drinks {Guid.NewGuid():N}"[..14] };
            db.MenuCategories.Add(category);
            await db.SaveChangesAsync();
            var item = new MenuItem { CategoryId = category.Id, Name = $"Station item {Guid.NewGuid():N}"[..20], Description = "t", Price = 3m, IsAvailable = true };
            db.MenuItems.Add(item);
            await db.SaveChangesAsync();
            categoryId = category.Id;
            itemId = item.Id;
        }
        var admin = factory.CreateClient();
        admin.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer",
            await ApiHelpers.CreateAdminTokenAsync(factory.Services, admin));

        var name = $"Bar {Guid.NewGuid():N}"[..10];
        Assert.Equal(HttpStatusCode.BadRequest, (await admin.PostAsJsonAsync("/api/v1/admin/stations", new { name = "  " })).StatusCode);
        var created = await admin.PostAsJsonAsync("/api/v1/admin/stations", new { name });
        Assert.Equal(HttpStatusCode.Created, created.StatusCode);
        var stationId = (await created.Content.ReadFromJsonAsync<JsonElement>()).GetProperty("id").GetGuid();
        Assert.Equal(HttpStatusCode.Conflict, (await admin.PostAsJsonAsync("/api/v1/admin/stations", new { name = name.ToUpperInvariant() })).StatusCode);

        Assert.Equal(HttpStatusCode.NotFound, (await admin.PutAsJsonAsync($"/api/v1/admin/stations/categories/{categoryId}", new { stationId = Guid.NewGuid() })).StatusCode);
        (await admin.PutAsJsonAsync($"/api/v1/admin/stations/categories/{categoryId}", new { stationId })).EnsureSuccessStatusCode();

        var routing = await admin.GetFromJsonAsync<JsonElement>("/api/v1/admin/stations/routing");
        Assert.Equal(stationId, routing.GetProperty("itemStations").GetProperty(itemId.ToString()).GetGuid());
        Assert.Equal(1, routing.GetProperty("stations").EnumerateArray().Single(s => s.GetProperty("id").GetGuid() == stationId).GetProperty("categories").GetInt32());

        // Deleting the station only un-routes the category.
        Assert.Equal(HttpStatusCode.OK, (await admin.DeleteAsync($"/api/v1/admin/stations/{stationId}")).StatusCode);
        routing = await admin.GetFromJsonAsync<JsonElement>("/api/v1/admin/stations/routing");
        Assert.False(routing.GetProperty("itemStations").TryGetProperty(itemId.ToString(), out _));
        Assert.Equal(HttpStatusCode.NotFound, (await admin.DeleteAsync($"/api/v1/admin/stations/{stationId}")).StatusCode);
    }
}
