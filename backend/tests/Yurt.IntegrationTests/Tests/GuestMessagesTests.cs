using System.Collections.Concurrent;
using System.Net;
using System.Net.Http.Json;
using System.Text.Json;
using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Yurt.Application.Common.Interfaces;
using Yurt.Application.Features.Marketing;
using Yurt.Application.Features.Push;
using Yurt.Domain.Entities;
using Yurt.Domain.Enums;
using Yurt.Infrastructure.Persistence;
using Yurt.IntegrationTests.Helpers;

namespace Yurt.IntegrationTests.Tests;

/// <summary>Guest groups and guest messages with personal promo codes (marketing module).</summary>
[Collection("Integration")]
public class GuestMessagesTests(YurtWebAppFactory factory)
{
    private static readonly Guid LocationId = Guid.Parse("11111111-0000-0000-0000-000000000001");

    /// <summary>11:00 in the cafe (UTC+5) today: inside the sending hours.</summary>
    private static DateTime Daytime => DateTime.UtcNow.Date.AddHours(6);

    private sealed class RecordingSender : IPushNotificationSender
    {
        public ConcurrentQueue<(IReadOnlyList<string> Tokens, string Title, string Body, IReadOnlyDictionary<string, string>? Data)> Sent { get; } = new();

        public Task<PushSendResult> SendAsync(IReadOnlyList<string> tokens, string title, string body,
            IReadOnlyDictionary<string, string>? data, CancellationToken ct = default)
        {
            Sent.Enqueue((tokens, title, body, data));
            return Task.FromResult(new PushSendResult([]));
        }

        public List<(IReadOnlyList<string> Tokens, string Title, string Body, string? Code)> To(string token)
            => Sent.Where(s => s.Tokens.Contains(token) && s.Data?.GetValueOrDefault("type") == "promo")
                .Select(s => (s.Tokens, s.Title, s.Body, s.Data!.GetValueOrDefault("code"))).ToList();
    }

    private WebApplicationFactory<Program> WithPush(RecordingSender sender)
        => factory.WithWebHostBuilder(b => b.ConfigureServices(services =>
        {
            services.AddSingleton(new FirebaseOptions { Enabled = true });
            services.AddScoped<IPushNotificationSender>(_ => sender);
        }));

    private static async Task<(HttpClient Client, Guid Id)> GuestAsync(
        WebApplicationFactory<Program> app, string phone, string? deviceToken, string? language = null)
    {
        var client = app.CreateClient();
        var (token, id) = await ApiHelpers.CreateCustomerAsync(client, phone);
        ApiHelpers.Authorize(client, token);
        if (deviceToken is not null)
            (await client.PostAsJsonAsync("/api/v1/auth/device-tokens", new { token = deviceToken, platform = "android", language }))
                .EnsureSuccessStatusCode();
        return (client, id);
    }

    private static async Task CompletedOrderAsync(IServiceProvider services, Guid customerId, DateTime at, decimal total = 1500m)
    {
        await using var scope = services.CreateAsyncScope();
        var db = scope.ServiceProvider.GetRequiredService<ApplicationDbContext>();
        db.Orders.Add(new Order
        {
            CustomerUserId = customerId, LocationId = LocationId, Status = OrderStatus.Completed,
            Subtotal = total, Total = total, CreatedAt = at, CompletedAt = at, PaymentStatus = PaymentStatus.Paid,
            LoyaltyPointsEarned = 0, // already settled, so the loyalty sweep in other tests leaves it alone
        });
        await db.SaveChangesAsync();
    }

    private static async Task<T> WithServiceAsync<T>(IServiceProvider services, Func<MarketingService, Task<T>> work)
    {
        await using var scope = services.CreateAsyncScope();
        return await work(scope.ServiceProvider.GetRequiredService<MarketingService>());
    }

    private static async Task<Guid> CreateCampaignAsync(HttpClient admin, object body)
    {
        var created = await admin.PostAsJsonAsync("/api/v1/admin/marketing/campaigns", body);
        Assert.Equal(HttpStatusCode.Created, created.StatusCode);
        return (await created.Content.ReadFromJsonAsync<JsonElement>()).GetProperty("id").GetGuid();
    }

    [Fact]
    public async Task ComeBackMessage_SendsPersonalCodeOnce_OnlyThatGuestCanUseIt()
    {
        var sender = new RecordingSender();
        using var app = WithPush(sender);
        var admin = app.CreateClient();
        ApiHelpers.Authorize(admin, await ApiHelpers.CreateAdminTokenAsync(app.Services, admin));

        var (sleeper, sleeperId) = await GuestAsync(app, "+77003110001", "gm-sleeper", "kk");
        await CompletedOrderAsync(app.Services, sleeperId, DateTime.UtcNow.AddDays(-40));
        var (_, activeId) = await GuestAsync(app, "+77003110002", "gm-active");
        await CompletedOrderAsync(app.Services, activeId, DateTime.UtcNow.AddDays(-2));
        var (quiet, quietId) = await GuestAsync(app, "+77003110003", "gm-quiet");
        await CompletedOrderAsync(app.Services, quietId, DateTime.UtcNow.AddDays(-40));
        // This guest switched offers off in the profile.
        var profile = await quiet.PutAsJsonAsync("/api/v1/auth/me", new { firstName = "Q", lastName = "G", marketingOptOut = true });
        profile.EnsureSuccessStatusCode();
        Assert.True((await profile.Content.ReadFromJsonAsync<JsonElement>()).GetProperty("marketingOptOut").GetBoolean());

        var id = await CreateCampaignAsync(admin, new
        {
            kind = "ComeBack", name = "We miss you", isActive = true, days = 30,
            title = "We miss you", titleKk = "Сізді сағындық, {name}", body = "Here is {discount} off", bodyKk = "{discount} жеңілдік",
            discountType = "Percentage", discountValue = 15, codeValidDays = 7,
        });
        try
        {
            // Only one come-back message per cafe.
            var twice = await admin.PostAsJsonAsync("/api/v1/admin/marketing/campaigns", new
            {
                kind = "ComeBack", name = "Again", isActive = true, days = 30, title = "x", body = "y",
            });
            Assert.Equal(HttpStatusCode.Conflict, twice.StatusCode);

            // At night nothing goes out.
            Assert.Equal(0, await WithServiceAsync(app.Services, s => s.RunAutomaticAsync(DateTime.UtcNow.Date.AddHours(20))));
            Assert.Empty(sender.To("gm-sleeper"));

            await WithServiceAsync(app.Services, s => s.RunAutomaticAsync(Daytime));
            var message = Assert.Single(sender.To("gm-sleeper"));
            Assert.Equal("Сізді сағындық, Test", message.Title); // in the phone's language
            Assert.StartsWith("15% жеңілдік", message.Body);
            Assert.Contains(message.Code!, message.Body);
            Assert.StartsWith("BACK-", message.Code);
            Assert.Empty(sender.To("gm-active"));
            Assert.Empty(sender.To("gm-quiet"));

            // Not again while the guest still hasn't come back.
            await WithServiceAsync(app.Services, s => s.RunAutomaticAsync(Daytime.AddHours(1)));
            Assert.Single(sender.To("gm-sleeper"));

            // The code is the guest's own: invisible to other guests, listed for the guest, and works in an order.
            var other = app.CreateClient();
            ApiHelpers.Authorize(other, (await ApiHelpers.CreateCustomerAsync(other, "+77003110004")).Token);
            var foreign = await (await other.PostAsJsonAsync("/api/v1/discount-codes/validate", new { code = message.Code, subtotal = 2000 }))
                .Content.ReadFromJsonAsync<JsonElement>();
            Assert.False(foreign.GetProperty("isValid").GetBoolean());

            var mine = await sleeper.GetFromJsonAsync<List<JsonElement>>("/api/v1/discount-codes/mine");
            Assert.Contains(mine!, c => c.GetProperty("code").GetString() == message.Code);

            Guid itemId; decimal price;
            await using (var scope = app.Services.CreateAsyncScope())
            {
                var db = scope.ServiceProvider.GetRequiredService<ApplicationDbContext>();
                var category = await db.MenuCategories.FirstAsync();
                var item = new MenuItem { CategoryId = category.Id, Name = $"Guest msg cake {Guid.NewGuid():N}"[..24], Description = "t", Price = 2000m, IsAvailable = true };
                db.MenuItems.Add(item);
                await db.SaveChangesAsync();
                await scope.ServiceProvider.GetRequiredService<IMenuCacheService>().InvalidateAllMenuAsync();
                (itemId, price) = (item.Id, item.Price);
            }
            var order = await sleeper.PostAsJsonAsync("/api/v1/orders", new
            {
                locationId = LocationId, paymentMethod = "Cash", discountCode = message.Code,
                items = new[] { new { menuItemId = itemId, quantity = 2, toppings = Array.Empty<object>() } },
            });
            Assert.Equal(HttpStatusCode.Created, order.StatusCode);
            var placed = await order.Content.ReadFromJsonAsync<JsonElement>();
            Assert.Equal(Math.Round(price * 2 * 0.15m, 2), placed.GetProperty("discountAmount").GetDecimal());

            // Used once: gone from the list; results count it; the shared codes page doesn't show personal codes.
            Assert.Empty((await sleeper.GetFromJsonAsync<List<JsonElement>>("/api/v1/discount-codes/mine"))!
                .Where(c => c.GetProperty("code").GetString() == message.Code));
            var campaigns = await admin.GetFromJsonAsync<List<JsonElement>>("/api/v1/admin/marketing/campaigns");
            var results = campaigns!.Single(c => c.GetProperty("id").GetGuid() == id).GetProperty("results");
            Assert.Equal(1, results.GetProperty("sent").GetInt32());
            Assert.Equal(1, results.GetProperty("codesUsed").GetInt32());
            Assert.True(results.GetProperty("revenue").GetDecimal() > 0);
            var shared = await admin.GetFromJsonAsync<List<JsonElement>>("/api/v1/admin/discount-codes");
            Assert.DoesNotContain(shared!, c => c.GetProperty("code").GetString() == message.Code);
        }
        finally
        {
            await admin.DeleteAsync($"/api/v1/admin/marketing/campaigns/{id}");
        }
    }

    [Fact]
    public async Task BirthdayAndOneOff_FollowGroupsAndNotTooOftenRule()
    {
        var sender = new RecordingSender();
        using var app = WithPush(sender);
        var admin = app.CreateClient();
        ApiHelpers.Authorize(admin, await ApiHelpers.CreateAdminTokenAsync(app.Services, admin));

        var today = DateOnly.FromDateTime(Daytime.AddHours(5));
        var soon = today.AddDays(2);
        var (bday, bdayId) = await GuestAsync(app, "+77003120001", "gm-bday");
        (await bday.PutAsJsonAsync("/api/v1/auth/me", new { firstName = "Aru", lastName = "B", dateOfBirth = new DateOnly(1996, soon.Month, soon.Day).ToString("yyyy-MM-dd") }))
            .EnsureSuccessStatusCode();
        var (noApp, _) = await GuestAsync(app, "+77003120002", deviceToken: null);
        (await noApp.PutAsJsonAsync("/api/v1/auth/me", new { firstName = "N", lastName = "A", dateOfBirth = new DateOnly(1996, soon.Month, soon.Day).ToString("yyyy-MM-dd") }))
            .EnsureSuccessStatusCode();

        // The group shows both, but only one can be reached.
        var overview = await admin.GetFromJsonAsync<JsonElement>("/api/v1/admin/marketing/overview");
        Assert.True(overview.GetProperty("pushReady").GetBoolean());
        var group = overview.GetProperty("segments").EnumerateArray().Single(s => s.GetProperty("key").GetString() == GuestSegments.BirthdayWeek);
        Assert.True(group.GetProperty("guests").GetInt32() >= 2);
        var listed = await admin.GetFromJsonAsync<List<JsonElement>>($"/api/v1/admin/marketing/segments/{GuestSegments.BirthdayWeek}");
        Assert.Contains(listed!, g => g.GetProperty("id").GetGuid() == bdayId && g.GetProperty("reachable").GetBoolean());

        var birthday = await CreateCampaignAsync(admin, new
        {
            kind = "Birthday", name = "Birthday gift", isActive = true, days = 3,
            titleRu = "С днём рождения, {name}!", bodyRu = "Дарим {discount} по коду {code}, {days} дней",
            discountType = "FixedAmount", discountValue = 1000, codeValidDays = 10,
        });
        var oneOff = await CreateCampaignAsync(admin, new
        {
            kind = "OneOff", name = "New drink", isActive = true, segment = GuestSegments.BirthdayWeek,
            titleRu = "Новый напиток", bodyRu = "Попробуйте!",
        });
        try
        {
            await WithServiceAsync(app.Services, s => s.RunAutomaticAsync(Daytime));
            var gift = Assert.Single(sender.To("gm-bday"));
            Assert.Equal("С днём рождения, Aru!", gift.Title);
            Assert.Equal($"Дарим 1000 ₸ по коду {gift.Code}, 10 дней", gift.Body);
            await using (var scope = app.Services.CreateAsyncScope())
            {
                var db = scope.ServiceProvider.GetRequiredService<ApplicationDbContext>();
                // Nothing kept for the guest without the app.
                Assert.Equal(1, await db.DiscountCodes.CountAsync(d => d.CampaignId == birthday));
            }

            // The one-off message skips the guest who just got a birthday message.
            var sent = await WithServiceAsync(app.Services, s => s.SendNowAsync(oneOff, Daytime.AddMinutes(5)));
            Assert.True(sent.Succeeded);
            Assert.True(sent.Data!.SkippedRecent >= 1);
            Assert.True(sent.Data.NoApp >= 1);
            Assert.Single(sender.To("gm-bday"));

            // Sent once only; and a new one can't go out at night.
            var again = await WithServiceAsync(app.Services, s => s.SendNowAsync(oneOff, Daytime.AddMinutes(6)));
            Assert.Equal(409, again.StatusCode);
            var night = await CreateCampaignAsync(admin, new
            {
                kind = "OneOff", name = "Late", isActive = true, segment = GuestSegments.All, title = "x", body = "y",
            });
            var late = await WithServiceAsync(app.Services, s => s.SendNowAsync(night, DateTime.UtcNow.Date.AddHours(19)));
            Assert.Equal(422, late.StatusCode);
            await admin.DeleteAsync($"/api/v1/admin/marketing/campaigns/{night}");
        }
        finally
        {
            await admin.DeleteAsync($"/api/v1/admin/marketing/campaigns/{birthday}");
            await admin.DeleteAsync($"/api/v1/admin/marketing/campaigns/{oneOff}");
        }
    }

    [Fact]
    public async Task Campaigns_AreValidated_AndForAdminsOnly()
    {
        var admin = factory.CreateClient();
        ApiHelpers.Authorize(admin, await ApiHelpers.CreateAdminTokenAsync(factory.Services, admin));
        var worker = factory.CreateClient();
        ApiHelpers.Authorize(worker, await ApiHelpers.CreateAdminTokenAsync(factory.Services, worker,
            username: "marketing-worker", password: "Worker@12345", role: AdminRole.Worker));

        Assert.Equal(HttpStatusCode.Forbidden, (await worker.GetAsync("/api/v1/admin/marketing/campaigns")).StatusCode);
        Assert.Equal(HttpStatusCode.BadRequest, (await admin.PostAsJsonAsync("/api/v1/admin/marketing/campaigns",
            new { kind = "OneOff", name = "x", isActive = true, segment = "nobody", title = "x", body = "y" })).StatusCode);
        Assert.Equal(HttpStatusCode.BadRequest, (await admin.PostAsJsonAsync("/api/v1/admin/marketing/campaigns",
            new { kind = "ComeBack", name = "x", isActive = true, days = 2, title = "x", body = "y" })).StatusCode);
        Assert.Equal(HttpStatusCode.BadRequest, (await admin.PostAsJsonAsync("/api/v1/admin/marketing/campaigns",
            new { kind = "Birthday", name = "x", isActive = true, days = 3, title = "", body = "" })).StatusCode);
        Assert.Equal(HttpStatusCode.BadRequest, (await admin.PostAsJsonAsync("/api/v1/admin/marketing/campaigns",
            new { kind = "Birthday", name = "x", isActive = true, days = 3, title = "x", body = "y", discountType = "Percentage", discountValue = 150 })).StatusCode);
        Assert.Equal(HttpStatusCode.NotFound, (await admin.GetAsync("/api/v1/admin/marketing/segments/nobody")).StatusCode);
    }
}
