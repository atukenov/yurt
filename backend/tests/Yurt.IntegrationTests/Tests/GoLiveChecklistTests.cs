using System.Net;
using System.Net.Http.Json;
using System.Text.Json;
using Yurt.Domain.Enums;
using Yurt.IntegrationTests.Helpers;

namespace Yurt.IntegrationTests.Tests;

/// <summary>The go-live checklist says what a cafe still needs, follows its modules, and never shows a secret value.</summary>
[Collection("Integration")]
public class GoLiveChecklistTests(YurtWebAppFactory factory)
{
    private static JsonElement ItemOf(JsonElement list, string key)
        => list.EnumerateArray().Single(i => i.GetProperty("key").GetString() == key);

    private static string StateOf(JsonElement list, string key) => ItemOf(list, key).GetProperty("state").GetString()!;

    private static List<string> MissingOf(JsonElement list, string key)
        => ItemOf(list, key).GetProperty("missing").EnumerateArray().Select(e => e.GetString()!).ToList();

    [Fact]
    public async Task NewCafe_ShowsWhatIsMissing_AndFollowsItsModulesAndSecrets()
    {
        var token = await ApiHelpers.CreateAdminTokenAsync(factory.Services, factory.CreateClient(),
            username: "golive-owner", password: "GoLiveOwner@123", role: AdminRole.SuperAdmin);
        var id = Guid.NewGuid().ToString("N")[..8];
        var slug = $"gl-{id}";
        var op = factory.CreateClient();
        ApiHelpers.Authorize(op, token);
        var created = await op.PostAsJsonAsync("/api/v1/platform/cafes",
            new { slug, name = $"Go live {id}", domain = (string?)null, adminUsername = $"gl-{id}", adminPassword = "FirstLogin@123" });
        created.EnsureSuccessStatusCode();
        var cafeId = (await created.Content.ReadFromJsonAsync<JsonElement>()).GetProperty("id").GetString();
        op.DefaultRequestHeaders.Add("X-Tenant", slug);

        var body = await op.GetFromJsonAsync<JsonElement>("/api/v1/platform/go-live");
        Assert.Equal(slug, body.GetProperty("slug").GetString());
        var server = body.GetProperty("server");
        Assert.Equal("done", StateOf(server, "master_key"));
        Assert.Equal("done", StateOf(server, "payment_return_url"));
        Assert.Equal("missing", StateOf(server, "telegram_alerts"));

        var cafe = body.GetProperty("cafe");
        Assert.Equal("missing", StateOf(cafe, "domain"));
        Assert.Equal("missing", StateOf(cafe, "menu"));
        Assert.Equal("missing", StateOf(cafe, "branches"));
        Assert.Equal(["logo", "icon"], MissingOf(cafe, "brand"));
        Assert.Equal(["price", "subscription"], MissingOf(cafe, "billing"));
        // A new cafe starts with only the core module, so payments, push, codes and tax receipts are not needed yet.
        Assert.Equal("off", StateOf(cafe, "stripe"));
        Assert.Equal("off", StateOf(cafe, "firebase"));
        Assert.Equal("off", StateOf(cafe, "whatsapp"));
        Assert.Equal("off", StateOf(cafe, "fiscal"));

        // Turning online payments on makes Stripe a missing item until the provider and both keys are set.
        (await op.PutAsJsonAsync("/api/v1/platform/modules/online_payments",
            new { enabled = true, validUntil = (DateTime?)null, source = "Manual", notes = (string?)null })).EnsureSuccessStatusCode();
        cafe = (await op.GetFromJsonAsync<JsonElement>("/api/v1/platform/go-live")).GetProperty("cafe");
        Assert.Equal(["provider", "Stripe:SecretKey", "Stripe:WebhookSecret"], MissingOf(cafe, "stripe"));

        const string secretValue = "sk_test_golive_do_not_show";
        (await op.PutAsJsonAsync("/api/v1/platform/payments", new { provider = "stripe" })).EnsureSuccessStatusCode();
        (await op.PutAsJsonAsync($"/api/v1/platform/cafes/{cafeId}/secrets/Stripe:SecretKey", new { value = secretValue })).EnsureSuccessStatusCode();
        (await op.PutAsJsonAsync($"/api/v1/platform/cafes/{cafeId}/secrets/Stripe:WebhookSecret", new { value = "whsec_golive" })).EnsureSuccessStatusCode();

        var raw = await op.GetStringAsync("/api/v1/platform/go-live");
        Assert.DoesNotContain(secretValue, raw);
        Assert.DoesNotContain("whsec_golive", raw);
        cafe = JsonDocument.Parse(raw).RootElement.GetProperty("cafe");
        // Test keys work but take no real money.
        Assert.Equal("warn", StateOf(cafe, "stripe"));
        Assert.Equal(["test_mode"], MissingOf(cafe, "stripe"));
    }

    [Fact]
    public async Task CafeAdmin_CannotOpenTheChecklist()
    {
        var token = await ApiHelpers.CreateAdminTokenAsync(factory.Services, factory.CreateClient(),
            username: "golive-cafe-admin", password: "GoLiveAdmin@123", role: AdminRole.Admin);
        var client = factory.CreateClient();
        ApiHelpers.Authorize(client, token);
        Assert.Equal(HttpStatusCode.Forbidden, (await client.GetAsync("/api/v1/platform/go-live")).StatusCode);
    }
}
