using System.Net;
using System.Net.Http.Headers;
using System.Net.Http.Json;
using System.Text.Json;
using Microsoft.AspNetCore.Hosting;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.DependencyInjection.Extensions;
using Yurt.Application.Common.Interfaces;
using Yurt.Domain;
using Yurt.Domain.Entities;
using Yurt.Infrastructure.Persistence;
using Yurt.IntegrationTests.Helpers;

namespace Yurt.IntegrationTests.Tests;

/// <summary>Staff watch (staff_watch): the camera box key, zones, shift plan, minute counts, alerts to Telegram, the day's report.</summary>
[Collection("Integration")]
public class StaffWatchTests(YurtWebAppFactory factory)
{
    private sealed class FakeTelegram : IWatchTelegram
    {
        public List<(string Token, string Chat, string Text)> Sent { get; } = [];

        public Task<bool> SendAsync(string botToken, string chatId, string text, CancellationToken ct = default)
        {
            lock (Sent) Sent.Add((botToken, chatId, text));
            return Task.FromResult(true);
        }

        public Task<string?> LatestChatAsync(string botToken, CancellationToken ct = default) => Task.FromResult<string?>("424242");
    }

    private static object License(bool enabled)
        => new { enabled, validUntil = (DateTime?)null, source = "Manual", notes = (string?)null };

    private static async Task<HttpClient> AdminAsync(Microsoft.AspNetCore.Mvc.Testing.WebApplicationFactory<Program> f)
    {
        var admin = f.CreateClient();
        admin.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", await ApiHelpers.CreateAdminTokenAsync(f.Services, admin));
        return admin;
    }

    private static async Task<Guid> NewLocationAsync(IServiceProvider services)
    {
        await using var scope = services.CreateAsyncScope();
        var db = scope.ServiceProvider.GetRequiredService<ApplicationDbContext>();
        var location = new Location { Name = $"Watch {Guid.NewGuid():N}"[..14], Address = "a", WorkingHours = "08:00-22:00" };
        db.Locations.Add(location);
        await db.SaveChangesAsync();
        return location.Id;
    }

    private static HttpRequestMessage Box(HttpMethod method, string path, string key, object? body = null)
    {
        var req = new HttpRequestMessage(method, $"/api/v1/watch-agent/{path}");
        if (body is not null) req.Content = JsonContent.Create(body);
        req.Headers.Add("X-Watch-Key", key);
        req.Headers.Add("X-Tenant", key.Split('.')[0]);
        return req;
    }

    [Fact]
    public async Task Box_GetsZonesAndShifts_SendsCounts_AlertsGoToTelegram_ReportShows()
    {
        var fake = new FakeTelegram();
        using var app = factory.WithWebHostBuilder(b => b.ConfigureServices(s =>
        {
            s.RemoveAll<IWatchTelegram>();
            s.AddSingleton<IWatchTelegram>(fake);
        }));
        var admin = await AdminAsync(app);
        var box = app.CreateClient();
        var loc = await NewLocationAsync(app.Services);

        Assert.Equal(HttpStatusCode.Unauthorized, (await box.SendAsync(Box(HttpMethod.Get, "config", "default.not-a-key"))).StatusCode);

        var created = await admin.PostAsync($"/api/v1/admin/watch/boxes/{loc}", null);
        Assert.Equal(HttpStatusCode.Created, created.StatusCode);
        var key = (await created.Content.ReadFromJsonAsync<JsonElement>()).GetProperty("key").GetString()!;
        Assert.StartsWith("default.", key);

        // first contact: the box's cameras appear in the admin, each waiting for a picture
        var cfg = await box.SendAsync(Box(HttpMethod.Get, "config?cameras=counter,door&version=2", key));
        Assert.Equal(HttpStatusCode.OK, cfg.StatusCode);
        var first = await cfg.Content.ReadFromJsonAsync<JsonElement>();
        Assert.Equal(2, first.GetProperty("snapshotRequests").GetArrayLength());
        var cameras = await admin.GetFromJsonAsync<JsonElement>($"/api/v1/admin/watch/cameras?locationId={loc}");
        Assert.Equal(["counter", "door"], cameras.EnumerateArray().Select(c => c.GetProperty("name").GetString()).ToArray());
        var counterId = cameras[0].GetProperty("id").GetGuid();

        // the box sends a picture (any JPEG), the request is cleared and admins can fetch it
        var jpeg = new byte[200];
        jpeg[0] = 0xFF; jpeg[1] = 0xD8;
        var snap = await box.SendAsync(Box(HttpMethod.Post, "snapshots", key, new { camera = "counter", width = 640, height = 360, jpegBase64 = Convert.ToBase64String(jpeg) }));
        Assert.Equal(HttpStatusCode.OK, snap.StatusCode);
        var pic = await admin.GetAsync($"/api/v1/admin/watch/cameras/{counterId}/snapshot");
        Assert.Equal("image/jpeg", pic.Content.Headers.ContentType?.MediaType);

        // zones: bad ones are refused, good ones reach the box
        var bad = await admin.PutAsJsonAsync($"/api/v1/admin/watch/cameras/{counterId}", new
        {
            locationId = loc, name = "counter",
            zones = new object[] { new { name = "door", kind = "door", points = new[] { new[] { 0d, 50d }, new[] { 100d, 50d } } } },
        });
        Assert.Equal(HttpStatusCode.BadRequest, bad.StatusCode); // a door needs its inside mark
        var zones = new object[]
        {
            new { name = "till", label = "Касса", kind = "station", queue = "queue", points = new[] { new[] { 0d, 0d }, new[] { 50d, 0d }, new[] { 50d, 50d } } },
            new { name = "queue", kind = "queue", points = new[] { new[] { 0d, 60d }, new[] { 100d, 60d }, new[] { 100d, 100d } } },
            new { name = "entrance", kind = "door", points = new[] { new[] { 0d, 90d }, new[] { 100d, 90d } }, inside = new[] { 50d, 50d } },
        };
        var saved = await admin.PutAsJsonAsync($"/api/v1/admin/watch/cameras/{counterId}", new { locationId = loc, name = "counter", zones });
        Assert.Equal(HttpStatusCode.OK, saved.StatusCode);

        var shifts = await admin.PutAsJsonAsync("/api/v1/admin/watch/shifts", new
        {
            locationId = loc, rows = new[] { new { day = "daily", name = "Айгерим", station = "till", start = "08:00", end = "16:00" } },
        });
        Assert.Equal(HttpStatusCode.OK, shifts.StatusCode);
        Assert.Equal(HttpStatusCode.BadRequest, (await admin.PutAsJsonAsync("/api/v1/admin/watch/shifts", new
        {
            locationId = loc, rows = new[] { new { day = "someday", name = "X", station = "till", start = "08:00", end = "16:00" } },
        })).StatusCode);

        var settings = await admin.PutAsJsonAsync("/api/v1/admin/watch/settings", new
        {
            locationId = loc, open = "09:00", close = "21:00",
            alerts = new { emptyWhileQueueMinutes = 3, queueMinGuests = 2, emptyOnShiftMinutes = 10, emptyMinutes = 15, lateGraceMinutes = 10, phoneMinutes = 4, stillMinutes = 12 },
            detect = new { phones = true, still = false }, telegramChatId = (string?)null,
        });
        Assert.Equal(HttpStatusCode.OK, settings.StatusCode);
        Assert.Equal(HttpStatusCode.OK, (await admin.PutAsJsonAsync("/api/v1/admin/watch/telegram-token",
            new { token = "123456789:AAEexampleexampleexampleexample-_12345" })).StatusCode);
        var chat = await admin.PostAsync($"/api/v1/admin/watch/telegram/find-chat?locationId={loc}", null);
        Assert.Equal(HttpStatusCode.OK, chat.StatusCode);
        Assert.Equal("424242", (await chat.Content.ReadFromJsonAsync<JsonElement>()).GetProperty("telegramChatId").GetString());

        var second = await (await box.SendAsync(Box(HttpMethod.Get, "config?cameras=counter,door", key))).Content.ReadFromJsonAsync<JsonElement>();
        Assert.Equal("09:00", second.GetProperty("open").GetString());
        Assert.Equal(4, second.GetProperty("alerts").GetProperty("phone_minutes").GetInt32());
        Assert.False(second.GetProperty("detect").GetProperty("still").GetBoolean());
        Assert.Equal(1, second.GetProperty("snapshotRequests").GetArrayLength()); // door camera still waits for its picture
        var counter = second.GetProperty("cameras").EnumerateArray().Single(c => c.GetProperty("name").GetString() == "counter");
        Assert.Equal(3, counter.GetProperty("zones").GetArrayLength());
        Assert.Equal("Айгерим", second.GetProperty("shifts")[0].GetProperty("name").GetString());

        // counts (sent twice: the second copy replaces the first), an alert, the daily report
        var today = StaffWatchClock.Today();
        object Row(string minute, string zone, int occ, int phone = 0, int entered = 0)
            => new { minute = $"{today}T{minute}", zone, seconds = 60, occupied = occ, peopleSum = occ, peopleMax = occ > 0 ? 1 : 0, phone, still = 0, entered, left = 0 };
        var rows = new { minutes = new[] { Row("10:00", "till", 60, phone: 50), Row("10:01", "till", 0), Row("10:00", "entrance", 0, entered: 2), Row("11:00", "entrance", 0, entered: 1) } };
        Assert.Equal(HttpStatusCode.OK, (await box.SendAsync(Box(HttpMethod.Post, "minutes", key, rows))).StatusCode);
        Assert.Equal(HttpStatusCode.OK, (await box.SendAsync(Box(HttpMethod.Post, "minutes", key, rows))).StatusCode);
        await using (var scope = app.Services.CreateAsyncScope())
        {
            var db = scope.ServiceProvider.GetRequiredService<ApplicationDbContext>();
            Assert.Equal(4, await db.WatchMinutes.IgnoreQueryFilters().CountAsync(m => m.LocationId == loc));
        }

        var alert = await box.SendAsync(Box(HttpMethod.Post, "alerts", key, new { alerts = new[] { new { at = $"{today}T10:05", kind = "phone", zone = "till", text = "📱 Касса: телефон в руках уже 4 мин" } } }));
        Assert.Equal(HttpStatusCode.OK, alert.StatusCode);
        Assert.Contains(fake.Sent, m => m.Chat == "424242" && m.Text.Contains("телефон"));

        var report = await box.SendAsync(Box(HttpMethod.Post, "reports", key, new { day = today, text = "📊 report", data = new { visitorsByHour = new { } } }));
        Assert.Equal(HttpStatusCode.OK, report.StatusCode);
        Assert.Contains(fake.Sent, m => m.Text == "📊 report");

        var day = await admin.GetFromJsonAsync<JsonElement>($"/api/v1/admin/watch/day?locationId={loc}&day={today}");
        Assert.Equal("📊 report", day.GetProperty("reportText").GetString());
        Assert.Equal(3, day.GetProperty("visitors").GetInt32());
        Assert.Single(day.GetProperty("alerts").EnumerateArray());
        var till = day.GetProperty("stations").EnumerateArray().Single();
        Assert.Equal(2, till.GetProperty("cameraMinutes").GetInt32());
        Assert.Equal(1, till.GetProperty("coveredMinutes").GetInt32());
        Assert.Equal(1, till.GetProperty("phoneMinutes").GetInt32());

        // a new key stops the old one
        await admin.PostAsync($"/api/v1/admin/watch/boxes/{loc}", null);
        Assert.Equal(HttpStatusCode.Unauthorized, (await box.SendAsync(Box(HttpMethod.Get, "config", key))).StatusCode);
    }

    [Fact]
    public async Task StaffWatch_SwitchedOff_Returns403_ForAdminAndBox()
    {
        var admin = await AdminAsync(factory);
        var super = factory.CreateClient();
        super.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", await ApiHelpers.CreateAdminTokenAsync(factory.Services, super,
            username: "platform-owner", password: "PlatformOwner@123", role: Yurt.Domain.Enums.AdminRole.SuperAdmin));
        try
        {
            await super.PutAsJsonAsync($"/api/v1/platform/modules/{ModuleKeys.StaffWatch}", License(false));
            var blocked = await admin.GetAsync("/api/v1/admin/watch/boxes");
            Assert.Equal(HttpStatusCode.Forbidden, blocked.StatusCode);
            Assert.Equal(ModuleKeys.StaffWatch, (await blocked.Content.ReadFromJsonAsync<JsonElement>()).GetProperty("module").GetString());
            Assert.Equal(HttpStatusCode.Forbidden, (await factory.CreateClient().SendAsync(Box(HttpMethod.Get, "config", "default.x"))).StatusCode);
        }
        finally
        {
            await super.PutAsJsonAsync($"/api/v1/platform/modules/{ModuleKeys.StaffWatch}", License(true));
        }
        Assert.Equal(HttpStatusCode.OK, (await admin.GetAsync("/api/v1/admin/watch/boxes")).StatusCode);
    }
}

internal static class StaffWatchClock
{
    public static string Today()
        => Yurt.Application.Features.StaffWatch.StaffWatchService.ToLocal(DateTime.UtcNow).ToString("yyyy-MM-dd", System.Globalization.CultureInfo.InvariantCulture);
}
