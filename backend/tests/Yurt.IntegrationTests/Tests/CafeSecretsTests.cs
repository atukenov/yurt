using System.Net;
using System.Net.Http.Json;
using System.Text.Json;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Yurt.Application.Common.Interfaces;
using Yurt.Domain.Entities;
using Yurt.Domain.Enums;
using Yurt.Infrastructure.Persistence;
using Yurt.IntegrationTests.Helpers;

namespace Yurt.IntegrationTests.Tests;

[Collection("Integration")]
public class CafeSecretsTests(YurtWebAppFactory factory)
{
    private async Task<HttpClient> OperatorAsync()
    {
        var token = await ApiHelpers.CreateAdminTokenAsync(factory.Services, factory.CreateClient(),
            username: "platform-owner", password: "PlatformOwner@123", role: AdminRole.SuperAdmin);
        var client = factory.CreateClient();
        ApiHelpers.Authorize(client, token);
        return client;
    }

    private async Task<Guid> NewCafeAsync(HttpClient op)
    {
        var id = Guid.NewGuid().ToString("N")[..8];
        var res = await op.PostAsJsonAsync("/api/v1/platform/cafes",
            new { slug = $"sec-{id}", name = $"Sec {id}", domain = (string?)null, adminUsername = $"owner-{id}", adminPassword = "FirstLogin@123" });
        return (await res.Content.ReadFromJsonAsync<JsonElement>()).GetProperty("id").GetGuid();
    }

    private ITenantSecrets SecretsFor(Guid tenantId, string slug, out IServiceScope scope)
    {
        scope = factory.Services.CreateScope();
        scope.ServiceProvider.GetRequiredService<ITenantContext>().Set(tenantId, slug);
        return scope.ServiceProvider.GetRequiredService<ITenantSecrets>();
    }

    [Fact]
    public async Task SetSecret_IsEncryptedAtRest_NeverReturned_AndReadableByThatCafeOnly()
    {
        var op = await OperatorAsync();
        var cafe = await NewCafeAsync(op);
        const string value = "iiko-key-ABC123";

        Assert.Equal(HttpStatusCode.OK,
            (await op.PutAsJsonAsync($"/api/v1/platform/cafes/{cafe}/secrets/Iiko:ApiKey", new { value })).StatusCode);

        var listJson = await op.GetStringAsync($"/api/v1/platform/cafes/{cafe}/secrets");
        Assert.DoesNotContain(value, listJson);
        var list = JsonSerializer.Deserialize<List<JsonElement>>(listJson)!;
        Assert.True(list.Single(s => s.GetProperty("key").GetString() == "Iiko:ApiKey").GetProperty("isSet").GetBoolean());
        Assert.False(list.Single(s => s.GetProperty("key").GetString() == "Iiko:ClientSecret").GetProperty("isSet").GetBoolean());

        using (var scope = factory.Services.CreateScope())
        {
            var db = scope.ServiceProvider.GetRequiredService<ApplicationDbContext>();
            var row = await db.TenantSecrets.IgnoreQueryFilters().SingleAsync(s => s.TenantId == cafe && s.Key == "Iiko:ApiKey");
            Assert.DoesNotContain(value, row.Ciphertext);
        }

        Assert.Equal(value, await SecretsFor(cafe, "x", out var s1).GetAsync("Iiko:ApiKey"));
        s1.Dispose();
        // The default cafe has no stored value for this key and the test server has no Iiko:ApiKey setting.
        Assert.Null(await SecretsFor(Tenant.DefaultId, "default", out var s2).GetAsync("Iiko:ApiKey"));
        s2.Dispose();
    }

    [Fact]
    public async Task OnlyTheDefaultCafe_FallsBackToServerWideSettings()
    {
        var op = await OperatorAsync();
        var cafe = await NewCafeAsync(op);

        // The test server sets Payment:SandboxSecret for the whole deployment.
        Assert.Equal("test-sandbox", await SecretsFor(Tenant.DefaultId, "default", out var s1).GetAsync("Payment:SandboxSecret"));
        s1.Dispose();
        Assert.Null(await SecretsFor(cafe, "x", out var s2).GetAsync("Payment:SandboxSecret"));
        s2.Dispose();
    }

    [Fact]
    public async Task ClearSecret_RemovesIt_AndUnknownKeyIsRejected()
    {
        var op = await OperatorAsync();
        var cafe = await NewCafeAsync(op);
        await op.PutAsJsonAsync($"/api/v1/platform/cafes/{cafe}/secrets/Otp:IdInstance", new { value = "123" });

        Assert.Equal(HttpStatusCode.OK, (await op.DeleteAsync($"/api/v1/platform/cafes/{cafe}/secrets/Otp:IdInstance")).StatusCode);
        Assert.Null(await SecretsFor(cafe, "x", out var s).GetAsync("Otp:IdInstance"));
        s.Dispose();

        Assert.Equal(HttpStatusCode.NotFound,
            (await op.PutAsJsonAsync($"/api/v1/platform/cafes/{cafe}/secrets/Nope:Key", new { value = "x" })).StatusCode);
        Assert.Equal(HttpStatusCode.BadRequest,
            (await op.PutAsJsonAsync($"/api/v1/platform/cafes/{cafe}/secrets/Iiko:ApiKey", new { value = "  " })).StatusCode);
        Assert.Equal(HttpStatusCode.NotFound,
            (await op.PutAsJsonAsync($"/api/v1/platform/cafes/{Guid.NewGuid()}/secrets/Iiko:ApiKey", new { value = "x" })).StatusCode);
    }

    [Fact]
    public async Task OnlyTheOperatorCanTouchSecrets()
    {
        var token = await ApiHelpers.CreateAdminTokenAsync(factory.Services, factory.CreateClient());
        var client = factory.CreateClient();
        ApiHelpers.Authorize(client, token);
        var res = await client.PutAsJsonAsync($"/api/v1/platform/cafes/{Tenant.DefaultId}/secrets/Iiko:ApiKey", new { value = "x" });
        Assert.Equal(HttpStatusCode.Forbidden, res.StatusCode);
    }
}
