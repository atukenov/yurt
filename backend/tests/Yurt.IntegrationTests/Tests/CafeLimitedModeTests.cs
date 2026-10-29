using System.Net;
using System.Net.Http.Headers;
using System.Net.Http.Json;
using System.Text.Json;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Yurt.Domain.Entities;
using Yurt.Domain.Enums;
using Yurt.Infrastructure.Persistence;
using Yurt.IntegrationTests.Helpers;

namespace Yurt.IntegrationTests.Tests;

/// <summary>Limited mode for a cafe that stopped paying: sign-in and history work, new orders are refused.</summary>
[Collection("Integration")]
public class CafeLimitedModeTests(YurtWebAppFactory factory)
{
    private static readonly Guid LocationId = Guid.Parse("11111111-0000-0000-0000-000000000001");

    [Fact]
    public async Task UnpaidCafe_KeepsSignInAndHistory_ButRefusesNewOrders()
    {
        var platform = factory.CreateClient();
        platform.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer",
            await ApiHelpers.CreateAdminTokenAsync(factory.Services, platform,
                username: "limited-owner", password: "LimitedOwner@123", role: AdminRole.SuperAdmin));
        Task<HttpResponseMessage> SetAccess(DateTime? paidUntil, bool byHand)
            => platform.PutAsJsonAsync("/api/v1/platform/access", new { paidUntil, limitedByOperator = byHand });

        Guid itemId;
        await using (var scope = factory.Services.CreateAsyncScope())
        {
            var db = scope.ServiceProvider.GetRequiredService<ApplicationDbContext>();
            var item = new MenuItem { CategoryId = (await db.MenuCategories.FirstAsync()).Id, Name = $"Lim {Guid.NewGuid():N}"[..16], Description = "t", Price = 5m, IsAvailable = true };
            db.MenuItems.Add(item);
            await db.SaveChangesAsync();
            itemId = item.Id;
            await scope.ServiceProvider.GetRequiredService<Yurt.Application.Common.Interfaces.IMenuCacheService>().InvalidateAllMenuAsync();
        }
        var customer = factory.CreateClient();
        ApiHelpers.Authorize(customer, (await ApiHelpers.CreateCustomerAsync(customer, "+77002990404")).Token);
        Task<HttpResponseMessage> Place() => customer.PostAsJsonAsync("/api/v1/orders", new
        {
            locationId = LocationId, paymentMethod = "Cash",
            items = new[] { new { menuItemId = itemId, quantity = 1, toppings = Array.Empty<object>() } },
        });
        Assert.Equal(HttpStatusCode.Created, (await Place()).StatusCode);

        try
        {
            // Paid period ended 30 days ago: past the grace days, so limited.
            var saved = await (await SetAccess(DateTime.UtcNow.AddDays(-30), false)).Content.ReadFromJsonAsync<JsonElement>();
            Assert.Equal("limited", saved.GetProperty("state").GetString());

            var config = await factory.CreateClient().GetFromJsonAsync<JsonElement>("/api/v1/app/config");
            Assert.True(config.GetProperty("ordering").GetProperty("paused").GetBoolean());

            var refused = await Place();
            Assert.Equal(HttpStatusCode.Forbidden, refused.StatusCode);
            Assert.Equal("cafe_limited", (await refused.Content.ReadFromJsonAsync<JsonElement>()).GetProperty("code").GetString());
            Assert.Equal(HttpStatusCode.Forbidden,
                (await customer.PostAsJsonAsync("/api/v1/group-orders", new { locationId = LocationId })).StatusCode);

            // History, menu and a fresh sign-in still work, and staff can still open the admin app.
            Assert.Equal(HttpStatusCode.OK, (await customer.GetAsync("/api/v1/orders/history")).StatusCode);
            Assert.Equal(HttpStatusCode.OK, (await customer.GetAsync("/api/v1/orders/active")).StatusCode);
            Assert.Equal(HttpStatusCode.OK, (await customer.GetAsync("/api/v1/menu")).StatusCode);
            var other = factory.CreateClient();
            await ApiHelpers.CreateCustomerAsync(other, "+77002990405");
            var staff = factory.CreateClient();
            staff.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer",
                await ApiHelpers.CreateAdminTokenAsync(factory.Services, staff));
            var sub = await staff.GetFromJsonAsync<JsonElement>("/api/v1/admin/subscription");
            Assert.Equal("limited", sub.GetProperty("access").GetProperty("state").GetString());
            Assert.Equal(HttpStatusCode.OK, (await staff.GetAsync("/api/v1/admin/orders")).StatusCode);

            // Two days late is inside the grace days: orders work again.
            saved = await (await SetAccess(DateTime.UtcNow.AddDays(-2), false)).Content.ReadFromJsonAsync<JsonElement>();
            Assert.Equal("grace", saved.GetProperty("state").GetString());
            Assert.Equal(HttpStatusCode.Created, (await Place()).StatusCode);

            // The hand switch limits a cafe whatever the date says.
            saved = await (await SetAccess(DateTime.UtcNow.AddYears(1), true)).Content.ReadFromJsonAsync<JsonElement>();
            Assert.Equal("limited", saved.GetProperty("state").GetString());
            Assert.Equal(HttpStatusCode.Forbidden, (await Place()).StatusCode);
        }
        finally
        {
            (await SetAccess(null, false)).EnsureSuccessStatusCode();
        }
        Assert.Equal(HttpStatusCode.Created, (await Place()).StatusCode);
    }

    [Fact]
    public async Task AccessSettings_AreOperatorOnly()
    {
        var staff = factory.CreateClient();
        staff.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer",
            await ApiHelpers.CreateAdminTokenAsync(factory.Services, staff));
        Assert.Equal(HttpStatusCode.Forbidden,
            (await staff.PutAsJsonAsync("/api/v1/platform/access", new { paidUntil = (DateTime?)null, limitedByOperator = true })).StatusCode);
    }
}
