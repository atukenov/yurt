using System.Net;
using System.Net.Http.Json;
using System.Text.Json;
using Yurt.Domain.Enums;
using Yurt.IntegrationTests.Helpers;

namespace Yurt.IntegrationTests.Tests;

/// <summary>Each cafe checks payment webhooks against its own secret (stored per cafe), never another cafe's.</summary>
[Collection("Integration")]
public class PaymentWebhookTenantTests(YurtWebAppFactory factory)
{
    private async Task<string> NewCafeAsync(string? webhookSecret)
    {
        var token = await ApiHelpers.CreateAdminTokenAsync(factory.Services, factory.CreateClient(),
            username: "platform-owner", password: "PlatformOwner@123", role: AdminRole.SuperAdmin);
        var op = factory.CreateClient();
        ApiHelpers.Authorize(op, token);
        var id = Guid.NewGuid().ToString("N")[..8];
        var res = await op.PostAsJsonAsync("/api/v1/platform/cafes",
            new { slug = $"pay-{id}", name = $"Pay {id}", domain = (string?)null, adminUsername = $"owner-{id}", adminPassword = "FirstLogin@123" });
        var cafeId = (await res.Content.ReadFromJsonAsync<JsonElement>()).GetProperty("id").GetGuid();
        if (webhookSecret is not null)
            await op.PutAsJsonAsync($"/api/v1/platform/cafes/{cafeId}/secrets/Payment:WebhookSecret", new { value = webhookSecret });
        return $"pay-{id}";
    }

    private async Task<HttpStatusCode> PostAsync(string slug, string signedWith)
    {
        var msg = WebhookHelpers.BuildMessage($"KS-{Guid.NewGuid():N}", "paid", 1000m, signedWith);
        msg.Headers.Add("X-Tenant", slug);
        return (await factory.CreateClient().SendAsync(msg)).StatusCode;
    }

    [Fact]
    public async Task CafeWithItsOwnSecret_AcceptsOnlyThatSecret()
    {
        var slug = await NewCafeAsync("cafe-specific-webhook-secret-123456");

        // A webhook that passes the signature check gets past validation (the invoice is unknown, so it is not a 400).
        Assert.NotEqual(HttpStatusCode.BadRequest, await PostAsync(slug, "cafe-specific-webhook-secret-123456"));
        Assert.Equal(HttpStatusCode.BadRequest, await PostAsync(slug, YurtWebAppFactory.WebhookSecret));
    }

    [Fact]
    public async Task CafeWithoutASecret_AcceptsNoWebhook_AndDoesNotUseTheServerWideOne()
    {
        var slug = await NewCafeAsync(null);

        Assert.Equal(HttpStatusCode.BadRequest, await PostAsync(slug, YurtWebAppFactory.WebhookSecret));
    }

    [Fact]
    public async Task DefaultCafe_KeepsUsingTheServerWideSecret()
    {
        var msg = WebhookHelpers.BuildMessage($"KS-{Guid.NewGuid():N}", "paid", 1000m, YurtWebAppFactory.WebhookSecret);
        var status = (await factory.CreateClient().SendAsync(msg)).StatusCode;
        Assert.NotEqual(HttpStatusCode.BadRequest, status);
    }
}
