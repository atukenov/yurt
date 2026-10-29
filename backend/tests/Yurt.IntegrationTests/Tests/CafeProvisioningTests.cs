using System.Net;
using System.Net.Http.Json;
using Yurt.Domain.Enums;
using Yurt.IntegrationTests.Helpers;

namespace Yurt.IntegrationTests.Tests;

[Collection("Integration")]
public class CafeProvisioningTests(YurtWebAppFactory factory)
{
    private async Task<HttpClient> OperatorAsync()
    {
        var token = await ApiHelpers.CreateAdminTokenAsync(factory.Services, factory.CreateClient(),
            username: "platform-owner", password: "PlatformOwner@123", role: AdminRole.SuperAdmin);
        var client = factory.CreateClient();
        ApiHelpers.Authorize(client, token);
        return client;
    }

    private static object NewCafe(string id, string? domain = null, string password = "FirstLogin@123") => new
    {
        slug = $"new-{id}", name = $"New Cafe {id}", domain, adminUsername = $"owner-{id}", adminPassword = password,
    };

    [Fact]
    public async Task CreatedCafe_HasItsOwnAdmin_WhoCanLogInOnlyThere()
    {
        var op = await OperatorAsync();
        var id = Guid.NewGuid().ToString("N")[..8];
        var domain = $"cafe-{id}.example.com";

        var created = await op.PostAsJsonAsync("/api/v1/platform/cafes", NewCafe(id, domain));
        Assert.Equal(HttpStatusCode.OK, created.StatusCode);

        var login = new { username = $"owner-{id}", password = "FirstLogin@123" };
        var onNewCafe = factory.CreateClient();
        onNewCafe.DefaultRequestHeaders.Add("X-Tenant", $"new-{id}");
        Assert.Equal(HttpStatusCode.OK, (await onNewCafe.PostAsJsonAsync("/api/v1/admin/auth/login", login)).StatusCode);

        // The same login does not exist on the default cafe.
        Assert.NotEqual(HttpStatusCode.OK, (await factory.CreateClient().PostAsJsonAsync("/api/v1/admin/auth/login", login)).StatusCode);

        var list = await op.GetFromJsonAsync<List<System.Text.Json.JsonElement>>("/api/v1/platform/cafes");
        Assert.Contains(list!, c => c.GetProperty("slug").GetString() == $"new-{id}");
    }

    [Fact]
    public async Task DuplicateKeyOrDomain_IsRejected()
    {
        var op = await OperatorAsync();
        var id = Guid.NewGuid().ToString("N")[..8];
        var domain = $"dup-{id}.example.com";
        Assert.Equal(HttpStatusCode.OK, (await op.PostAsJsonAsync("/api/v1/platform/cafes", NewCafe(id, domain))).StatusCode);

        Assert.Equal(HttpStatusCode.Conflict, (await op.PostAsJsonAsync("/api/v1/platform/cafes", NewCafe(id))).StatusCode);
        var other = Guid.NewGuid().ToString("N")[..8];
        Assert.Equal(HttpStatusCode.Conflict, (await op.PostAsJsonAsync("/api/v1/platform/cafes", NewCafe(other, domain))).StatusCode);
    }

    [Theory]
    [InlineData("Bad Slug!", "short1234")]
    [InlineData("good-slug", "short")]
    public async Task InvalidInput_IsRejected(string slug, string password)
    {
        var op = await OperatorAsync();
        var res = await op.PostAsJsonAsync("/api/v1/platform/cafes",
            new { slug, name = "X", domain = (string?)null, adminUsername = "owner1", adminPassword = password });
        Assert.Equal(HttpStatusCode.BadRequest, res.StatusCode);
    }

    [Fact]
    public async Task DefaultCafe_CannotBeSwitchedOff_ButAnotherCan()
    {
        var op = await OperatorAsync();
        var id = Guid.NewGuid().ToString("N")[..8];
        var created = await op.PostAsJsonAsync("/api/v1/platform/cafes", NewCafe(id));
        var cafeId = (await created.Content.ReadFromJsonAsync<System.Text.Json.JsonElement>()).GetProperty("id").GetGuid();

        Assert.Equal(HttpStatusCode.OK, (await op.PutAsJsonAsync($"/api/v1/platform/cafes/{cafeId}/active", new { isActive = false })).StatusCode);
        Assert.Equal(HttpStatusCode.BadRequest,
            (await op.PutAsJsonAsync($"/api/v1/platform/cafes/{Yurt.Domain.Entities.Tenant.DefaultId}/active", new { isActive = false })).StatusCode);
    }

    [Fact]
    public async Task OnlyTheOperatorCanOpenACafe()
    {
        var token = await ApiHelpers.CreateAdminTokenAsync(factory.Services, factory.CreateClient());
        var client = factory.CreateClient();
        ApiHelpers.Authorize(client, token);
        var res = await client.PostAsJsonAsync("/api/v1/platform/cafes", NewCafe(Guid.NewGuid().ToString("N")[..8]));
        Assert.Equal(HttpStatusCode.Forbidden, res.StatusCode);
    }
    [Fact]
    public async Task Operator_CanSetUpTheNewCafe_BranchAndMenuFromASpreadsheet()
    {
        var op = await OperatorAsync();
        var id = Guid.NewGuid().ToString("N")[..8];
        Assert.Equal(HttpStatusCode.OK, (await op.PostAsJsonAsync("/api/v1/platform/cafes", NewCafe(id))).StatusCode);
        HttpRequestMessage ForNewCafe(HttpMethod method, string url, HttpContent? body = null)
        {
            var req = new HttpRequestMessage(method, url) { Content = body };
            req.Headers.Add("X-Tenant", $"new-{id}");
            return req;
        }

        var branch = await op.SendAsync(ForNewCafe(HttpMethod.Post, "/api/v1/admin/locations",
            JsonContent.Create(new { name = "Main", address = "Abay 1", workingHours = "", contactPhone = "+77010000000" })));
        Assert.Equal(HttpStatusCode.Created, branch.StatusCode);

        var template = await op.GetAsync("/api/v1/admin/menu/import/template?lang=ru");
        Assert.Equal(HttpStatusCode.OK, template.StatusCode);
        var file = await template.Content.ReadAsByteArrayAsync();

        MultipartFormDataContent Upload() => new() { { new ByteArrayContent(file), "file", "menu.xlsx" } };
        var check = await op.SendAsync(ForNewCafe(HttpMethod.Post, "/api/v1/admin/menu/import", Upload()));
        var checkBody = await check.Content.ReadFromJsonAsync<System.Text.Json.JsonElement>();
        Assert.False(checkBody.GetProperty("applied").GetBoolean());
        Assert.Equal(2, checkBody.GetProperty("itemsNew").GetInt32());

        var saved = await op.SendAsync(ForNewCafe(HttpMethod.Post, "/api/v1/admin/menu/import?apply=true", Upload()));
        Assert.True((await saved.Content.ReadFromJsonAsync<System.Text.Json.JsonElement>()).GetProperty("applied").GetBoolean());

        var items = await (await op.SendAsync(ForNewCafe(HttpMethod.Get, "/api/v1/admin/menu/items")))
            .Content.ReadFromJsonAsync<List<System.Text.Json.JsonElement>>();
        Assert.Equal(["Капучино", "Круассан"], items!.Select(i => i.GetProperty("name").GetString()).Order());

        // The default cafe's menu did not get the new cafe's dishes.
        var defaultItems = await op.GetFromJsonAsync<List<System.Text.Json.JsonElement>>("/api/v1/admin/menu/items");
        Assert.DoesNotContain(defaultItems!, i => i.GetProperty("name").GetString() == "Круассан");
    }
}
