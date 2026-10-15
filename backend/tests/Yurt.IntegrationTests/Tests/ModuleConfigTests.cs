using System.Net;
using System.Net.Http.Json;
using System.Text.Json;
using Yurt.Domain;

namespace Yurt.IntegrationTests.Tests;

[Collection("Integration")]
public class ModuleConfigTests(YurtWebAppFactory factory)
{
    [Fact]
    public async Task AppConfig_IsPublic_AndListsEveryModuleEnabledByDefault()
    {
        var client = factory.CreateClient();

        var resp = await client.GetAsync("/api/v1/app/config");

        Assert.Equal(HttpStatusCode.OK, resp.StatusCode);
        var json = await resp.Content.ReadFromJsonAsync<JsonElement>();
        var keys = json.GetProperty("modules").EnumerateArray().Select(e => e.GetString()).ToList();
        foreach (var key in ModuleKeys.All)
            Assert.Contains(key, keys);
    }

    [Fact]
    public async Task AdminSubscription_RequiresAuthentication()
    {
        var client = factory.CreateClient();
        var resp = await client.GetAsync("/api/v1/admin/subscription");
        Assert.Equal(HttpStatusCode.Unauthorized, resp.StatusCode);
    }
}
