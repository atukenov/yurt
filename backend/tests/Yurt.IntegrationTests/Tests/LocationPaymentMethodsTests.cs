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

/// <summary>A branch offering only some payment methods (own_pos, Phase 6).</summary>
[Collection("Integration")]
public class LocationPaymentMethodsTests(YurtWebAppFactory factory)
{
    private static readonly Guid LocationId = Guid.Parse("11111111-0000-0000-0000-000000000001");

    [Fact]
    public async Task Branch_OffersOnlyTheChosenMethods_AndRefusesOrdersWithOthers()
    {
        var admin = factory.CreateClient();
        admin.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer",
            await ApiHelpers.CreateAdminTokenAsync(factory.Services, admin));
        var loc = await admin.GetFromJsonAsync<JsonElement>($"/api/v1/admin/locations/{LocationId}");

        Task<HttpResponseMessage> Save(string[] methods) => admin.PutAsJsonAsync($"/api/v1/admin/locations/{LocationId}", new
        {
            name = loc.GetProperty("name").GetString(),
            address = loc.GetProperty("address").GetString(),
            workingHours = loc.GetProperty("workingHours").GetString(),
            contactPhone = loc.GetProperty("contactPhone").GetString(),
            isActive = true,
            paymentMethods = methods,
        });

        try
        {
            Guid itemId;
            await using (var scope = factory.Services.CreateAsyncScope())
            {
                var db = scope.ServiceProvider.GetRequiredService<ApplicationDbContext>();
                var item = new MenuItem { CategoryId = (await db.MenuCategories.FirstAsync()).Id, Name = $"Pay item {Guid.NewGuid():N}"[..18], Description = "t", Price = 5m, IsAvailable = true };
                db.MenuItems.Add(item);
                await db.SaveChangesAsync();
                itemId = item.Id;
                await scope.ServiceProvider.GetRequiredService<Yurt.Application.Common.Interfaces.IMenuCacheService>().InvalidateAllMenuAsync();
            }
            var customer = factory.CreateClient();
            var (token, _) = await ApiHelpers.CreateCustomerAsync(customer, "+77002990303");
            ApiHelpers.Authorize(customer, token);
            Task<HttpResponseMessage> Place(string method) => customer.PostAsJsonAsync("/api/v1/orders", new
            {
                locationId = LocationId, paymentMethod = method,
                items = new[] { new { menuItemId = itemId, quantity = 1, toppings = Array.Empty<object>() } },
            });

            // Unknown names are dropped, the rest is kept in a fixed order.
            (await Save(["Card", "Cash", "Gold"])).EnsureSuccessStatusCode();
            var shown = (await customer.GetFromJsonAsync<JsonElement>("/api/v1/locations")).EnumerateArray()
                .First(l => l.GetProperty("id").GetGuid() == LocationId);
            Assert.Equal(["Cash", "Card"], shown.GetProperty("paymentMethods").EnumerateArray().Select(x => x.GetString()).ToArray());

            Assert.Equal(HttpStatusCode.UnprocessableEntity, (await Place("KaspiBank")).StatusCode);
            Assert.Equal(HttpStatusCode.Created, (await Place("Card")).StatusCode);

            // An empty list means every method again, and a save without the field leaves it as it is.
            (await Save([])).EnsureSuccessStatusCode();
            shown = (await customer.GetFromJsonAsync<JsonElement>("/api/v1/locations")).EnumerateArray()
                .First(l => l.GetProperty("id").GetGuid() == LocationId);
            Assert.False(shown.TryGetProperty("paymentMethods", out var pm) && pm.ValueKind == JsonValueKind.Array);
            Assert.Equal(HttpStatusCode.Created, (await Place("KaspiBank")).StatusCode);
        }
        finally
        {
            (await Save([])).EnsureSuccessStatusCode();
        }
    }
}
