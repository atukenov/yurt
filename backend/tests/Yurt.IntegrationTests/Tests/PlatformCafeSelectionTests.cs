using System.Net;
using System.Net.Http.Json;
using System.Text.Json;
using Yurt.Domain.Enums;
using Yurt.IntegrationTests.Helpers;

namespace Yurt.IntegrationTests.Tests;

/// <summary>The operator manages another cafe by sending its slug in X-Tenant; the first cafe stays untouched.</summary>
[Collection("Integration")]
public class PlatformCafeSelectionTests(YurtWebAppFactory factory)
{
    [Fact]
    public async Task OperatorChangesBrandAndMaintenance_OfTheSelectedCafeOnly()
    {
        var token = await ApiHelpers.CreateAdminTokenAsync(factory.Services, factory.CreateClient(),
            username: "platform-owner", password: "PlatformOwner@123", role: AdminRole.SuperAdmin);
        HttpClient Op(string? slug)
        {
            var c = factory.CreateClient();
            ApiHelpers.Authorize(c, token);
            if (slug is not null) c.DefaultRequestHeaders.Add("X-Tenant", slug);
            return c;
        }

        var id = Guid.NewGuid().ToString("N")[..8];
        var slug = $"sel-{id}";
        await Op(null).PostAsJsonAsync("/api/v1/platform/cafes",
            new { slug, name = $"Sel {id}", domain = (string?)null, adminUsername = $"owner-{id}", adminPassword = "FirstLogin@123" });

        var before = (await Op(null).GetFromJsonAsync<JsonElement>("/api/v1/platform/brand")).GetProperty("name").GetString();

        var put = await Op(slug).PutAsJsonAsync("/api/v1/platform/brand",
            new { name = $"Brand {id}", primaryColor = "#112233", accentColor = "#445566" });
        Assert.Equal(HttpStatusCode.OK, put.StatusCode);
        Assert.Equal(HttpStatusCode.OK,
            (await Op(slug).PutAsJsonAsync("/api/v1/platform/maintenance", new { enabled = true, message = "Back soon" })).StatusCode);

        Assert.Equal($"Brand {id}", (await Op(slug).GetFromJsonAsync<JsonElement>("/api/v1/platform/brand")).GetProperty("name").GetString());
        Assert.True((await Op(slug).GetFromJsonAsync<JsonElement>("/api/v1/platform/maintenance")).GetProperty("enabled").GetBoolean());

        // The cafe of the default address is unchanged.
        Assert.Equal(before, (await Op(null).GetFromJsonAsync<JsonElement>("/api/v1/platform/brand")).GetProperty("name").GetString());
        Assert.False((await Op(null).GetFromJsonAsync<JsonElement>("/api/v1/platform/maintenance")).GetProperty("enabled").GetBoolean());
    }
}
