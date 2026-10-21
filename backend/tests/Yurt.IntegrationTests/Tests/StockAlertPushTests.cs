using System.Collections.Concurrent;
using System.Net;
using System.Net.Http.Headers;
using System.Net.Http.Json;
using System.Text.Json;
using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Yurt.Application.Common.Interfaces;
using Yurt.Application.Features.Push;
using Yurt.Domain.Entities;
using Yurt.Domain.Enums;
using Yurt.Infrastructure.Persistence;
using Yurt.IntegrationTests.Helpers;

namespace Yurt.IntegrationTests.Tests;

/// <summary>Phone alerts when an accepted order takes stock down to its warn-at number (own_pos, Phase 6).</summary>
[Collection("Integration")]
public class StockAlertPushTests(YurtWebAppFactory factory)
{
    private static readonly Guid LocationId = Guid.Parse("11111111-0000-0000-0000-000000000001");

    private sealed class RecordingSender : IPushNotificationSender
    {
        public ConcurrentQueue<(IReadOnlyList<string> Tokens, string Title, string Body, IReadOnlyDictionary<string, string>? Data)> Sent { get; } = new();

        public Task<PushSendResult> SendAsync(IReadOnlyList<string> tokens, string title, string body,
            IReadOnlyDictionary<string, string>? data, CancellationToken ct = default)
        {
            Sent.Enqueue((tokens, title, body, data));
            return Task.FromResult(new PushSendResult([]));
        }

        public List<(IReadOnlyList<string> Tokens, string Title, string Body)> StockAlerts()
            => Sent.Where(s => s.Data?.GetValueOrDefault("type") == "stock-alert").Select(s => (s.Tokens, s.Title, s.Body)).ToList();
    }

    private WebApplicationFactory<Program> WithPush(RecordingSender sender)
        => factory.WithWebHostBuilder(b => b.ConfigureServices(services =>
        {
            services.AddSingleton(new FirebaseOptions { Enabled = true });
            services.AddScoped<IPushNotificationSender>(_ => sender);
        }));

    private static async Task<Guid> NewItemAsync(IServiceProvider services, string name)
    {
        await using var scope = services.CreateAsyncScope();
        var db = scope.ServiceProvider.GetRequiredService<ApplicationDbContext>();
        var category = await db.MenuCategories.FirstAsync();
        var item = new MenuItem { CategoryId = category.Id, Name = name, Description = "test", Price = 5m, IsAvailable = true };
        db.MenuItems.Add(item);
        await db.SaveChangesAsync();
        await scope.ServiceProvider.GetRequiredService<IMenuCacheService>().InvalidateAllMenuAsync();
        return item.Id;
    }

    private static async Task OrderAndAcceptAsync(HttpClient customer, HttpClient admin, Guid itemId, int quantity)
    {
        var placed = await customer.PostAsJsonAsync("/api/v1/orders", new
        {
            locationId = LocationId, paymentMethod = "Cash",
            items = new[] { new { menuItemId = itemId, quantity, toppings = Array.Empty<object>() } },
        });
        Assert.Equal(HttpStatusCode.Created, placed.StatusCode);
        var order = await placed.Content.ReadFromJsonAsync<ApiHelpers.OrderResult>(ApiHelpers.JsonOpts);
        (await admin.PostAsJsonAsync($"/api/v1/admin/orders/{order!.Id}/payment", new { paymentStatus = "Paid", paymentMethod = "Cash" }))
            .EnsureSuccessStatusCode();
        (await admin.PostAsJsonAsync($"/api/v1/admin/orders/{order.Id}/accept", new { etaMinutes = 5 })).EnsureSuccessStatusCode();
    }

    [Fact]
    public async Task OrderThatCrossesWarnLevel_PushesTheAlertPhoneOnce()
    {
        var sender = new RecordingSender();
        using var app = WithPush(sender);
        var admin = app.CreateClient();
        admin.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer",
            await ApiHelpers.CreateAdminTokenAsync(app.Services, admin));

        // A manager who has the cafe app on their phone.
        var staff = app.CreateClient();
        var (staffToken, _) = await ApiHelpers.CreateCustomerAsync(staff, "+77002994101");
        ApiHelpers.Authorize(staff, staffToken);
        (await staff.PostAsJsonAsync("/api/v1/auth/device-tokens", new { token = "stock-alert-phone", platform = "android" }))
            .EnsureSuccessStatusCode();

        var customer = app.CreateClient();
        var (token, _) = await ApiHelpers.CreateCustomerAsync(customer, "+77002994102");
        ApiHelpers.Authorize(customer, token);

        var itemId = await NewItemAsync(app.Services, $"Alert cake {Guid.NewGuid():N}"[..24]);
        try
        {
            // Typed the local way; still matches the +7 account.
            var saved = await admin.PutAsJsonAsync("/api/v1/admin/stock/alert-phones", new { phones = new[] { "8 700 299 41 01" } });
            saved.EnsureSuccessStatusCode();
            var phones = await saved.Content.ReadFromJsonAsync<List<JsonElement>>();
            var phone = Assert.Single(phones!);
            Assert.True(phone.GetProperty("hasApp").GetBoolean());

            (await admin.PutAsJsonAsync($"/api/v1/admin/stock/{itemId}/{LocationId}", new { quantity = 5, lowStockAt = 3 }))
                .EnsureSuccessStatusCode();

            await OrderAndAcceptAsync(customer, admin, itemId, 1); // 4 left: still above 3
            Assert.Empty(sender.StockAlerts());

            await OrderAndAcceptAsync(customer, admin, itemId, 1); // 3 left: crosses the line
            var alert = Assert.Single(sender.StockAlerts());
            Assert.Equal(["stock-alert-phone"], alert.Tokens);
            Assert.Equal("Мало на складе", alert.Title);
            Assert.Contains("осталось 3", alert.Body);

            await OrderAndAcceptAsync(customer, admin, itemId, 1); // 2 left: already low, stays quiet
            Assert.Single(sender.StockAlerts());

            await OrderAndAcceptAsync(customer, admin, itemId, 2); // 0 left: out
            Assert.Equal(2, sender.StockAlerts().Count);
            Assert.Equal("Закончилось на складе", sender.StockAlerts()[1].Title);
        }
        finally
        {
            await admin.PutAsJsonAsync("/api/v1/admin/stock/alert-phones", new { phones = Array.Empty<string>() });
            await admin.PutAsJsonAsync($"/api/v1/admin/stock/{itemId}/{LocationId}", new { quantity = (int?)null, lowStockAt = (int?)null });
        }
    }

    [Fact]
    public async Task AlertPhones_Validated_AndOnlyAdminsChangeThem()
    {
        var admin = factory.CreateClient();
        admin.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer",
            await ApiHelpers.CreateAdminTokenAsync(factory.Services, admin));
        var worker = factory.CreateClient();
        worker.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer",
            await ApiHelpers.CreateAdminTokenAsync(factory.Services, worker,
                username: "stock-alert-worker", password: "Worker@12345", role: AdminRole.Worker));

        var bad = await admin.PutAsJsonAsync("/api/v1/admin/stock/alert-phones", new { phones = new[] { "call me" } });
        Assert.Equal(HttpStatusCode.BadRequest, bad.StatusCode);
        var tooMany = await admin.PutAsJsonAsync("/api/v1/admin/stock/alert-phones",
            new { phones = Enumerable.Range(1, 6).Select(i => $"+7700000000{i}").ToArray() });
        Assert.Equal(HttpStatusCode.BadRequest, tooMany.StatusCode);

        Assert.Equal(HttpStatusCode.Forbidden,
            (await worker.PutAsJsonAsync("/api/v1/admin/stock/alert-phones", new { phones = new[] { "+77001234567" } })).StatusCode);
        Assert.Equal(HttpStatusCode.OK, (await worker.GetAsync("/api/v1/admin/stock/alert-phones")).StatusCode);

        // A number with no cafe-app account is kept but shown as not reachable yet.
        var saved = await admin.PutAsJsonAsync("/api/v1/admin/stock/alert-phones", new { phones = new[] { "+77009999999" } });
        saved.EnsureSuccessStatusCode();
        var phone = Assert.Single((await saved.Content.ReadFromJsonAsync<List<JsonElement>>())!);
        Assert.Equal("+77009999999", phone.GetProperty("phone").GetString());
        Assert.False(phone.GetProperty("hasApp").GetBoolean());
        await admin.PutAsJsonAsync("/api/v1/admin/stock/alert-phones", new { phones = Array.Empty<string>() });
    }
}
