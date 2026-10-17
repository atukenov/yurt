using System.Net;
using System.Net.Http.Json;
using Yurt.Domain.Enums;
using Yurt.IntegrationTests.Helpers;

namespace Yurt.IntegrationTests.Tests;

/// <summary>The iiko settings belong to the first cafe; another cafe can neither use them nor receive their webhooks.</summary>
[Collection("Integration")]
public class IikoTenantTests(YurtWebAppFactory factory)
{
    [Fact]
    public async Task AnotherCafe_CannotUseTheIikoAdminTools_AndCannotPassTheIikoWebhook()
    {
        var token = await ApiHelpers.CreateAdminTokenAsync(factory.Services, factory.CreateClient(),
            username: "platform-owner", password: "PlatformOwner@123", role: AdminRole.SuperAdmin);
        var op = factory.CreateClient();
        ApiHelpers.Authorize(op, token);
        var id = Guid.NewGuid().ToString("N")[..8];
        await op.PostAsJsonAsync("/api/v1/platform/cafes",
            new { slug = $"iik-{id}", name = $"Iiko {id}", domain = (string?)null, adminUsername = $"owner-{id}", adminPassword = "FirstLogin@123" });

        var other = factory.CreateClient();
        ApiHelpers.Authorize(other, token);
        other.DefaultRequestHeaders.Add("X-Tenant", $"iik-{id}");

        Assert.Equal(HttpStatusCode.NotFound, (await other.GetAsync("/api/v1/admin/iiko/terminal-groups")).StatusCode);

        var hook = factory.CreateClient();
        hook.DefaultRequestHeaders.Add("X-Tenant", $"iik-{id}");
        hook.DefaultRequestHeaders.TryAddWithoutValidation("Authorization", "anything");
        Assert.Equal(HttpStatusCode.Unauthorized, (await hook.PostAsJsonAsync("/api/v1/webhooks/iiko", new object[0])).StatusCode);

        // The first cafe is not blocked by the new check (iiko is switched off in the test server, so the call itself may fail, but it is not a 404).
        Assert.NotEqual(HttpStatusCode.NotFound, (await op.GetAsync("/api/v1/admin/iiko/terminal-groups")).StatusCode);
    }
}
