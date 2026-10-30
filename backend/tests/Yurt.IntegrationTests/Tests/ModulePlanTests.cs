using System.Net;
using System.Net.Http.Json;
using System.Text.Json;
using Yurt.Domain;
using Yurt.Domain.Enums;
using Yurt.IntegrationTests.Helpers;

namespace Yurt.IntegrationTests.Tests;

/// <summary>The Modules section of the Platform screen: a new cafe starts with only core, and switching modules on or off
/// with their prices sets both what the cafe can use and what its monthly payment covers.
/// Each test opens its own cafe so the first cafe's modules are never touched.</summary>
[Collection("Integration")]
public class ModulePlanTests(YurtWebAppFactory factory)
{
    private async Task<Func<HttpClient>> NewCafeAsync()
    {
        var token = await ApiHelpers.CreateAdminTokenAsync(factory.Services, factory.CreateClient(),
            username: "platform-owner", password: "PlatformOwner@123", role: AdminRole.SuperAdmin);
        var id = Guid.NewGuid().ToString("N")[..8];
        var slug = $"plan-{id}";
        HttpClient Client(string? tenant)
        {
            var c = factory.CreateClient();
            ApiHelpers.Authorize(c, token);
            if (tenant is not null) c.DefaultRequestHeaders.Add("X-Tenant", tenant);
            return c;
        }
        var created = await Client(null).PostAsJsonAsync("/api/v1/platform/cafes",
            new { slug, name = $"Plan {id}", domain = (string?)null, adminUsername = $"plan-{id}", adminPassword = "FirstLogin@123" });
        Assert.True(created.IsSuccessStatusCode, await created.Content.ReadAsStringAsync());
        return () => Client(slug);
    }

    private static Dictionary<string, JsonElement> ByKey(JsonElement plan)
        => plan.GetProperty("modules").EnumerateArray().ToDictionary(m => m.GetProperty("key").GetString()!);

    private static async Task<JsonElement> SaveAsync(HttpClient op, object body)
    {
        var res = await op.PutAsJsonAsync("/api/v1/platform/module-plan", body);
        Assert.True(res.IsSuccessStatusCode, await res.Content.ReadAsStringAsync());
        return await res.Content.ReadFromJsonAsync<JsonElement>();
    }

    [Fact]
    public async Task NewCafe_StartsWithOnlyTheCoreModule()
    {
        var op = await NewCafeAsync();

        var modules = await op().GetFromJsonAsync<List<JsonElement>>("/api/v1/platform/modules");
        foreach (var m in modules!)
        {
            var key = m.GetProperty("key").GetString();
            Assert.Equal(key == ModuleKeys.Core ? "active" : "disabled", m.GetProperty("state").GetString());
        }

        var plan = await op().GetFromJsonAsync<JsonElement>("/api/v1/platform/module-plan");
        Assert.Equal([ModuleKeys.Core], ByKey(plan).Where(m => m.Value.GetProperty("on").GetBoolean()).Select(m => m.Key));
        Assert.Equal(0m, plan.GetProperty("total").GetDecimal());

        // A switched-off module is closed for the new cafe.
        Assert.Equal(HttpStatusCode.Forbidden, (await op().GetAsync("/api/v1/admin/dashboard")).StatusCode);
    }

    [Fact]
    public async Task SwitchingModulesOn_OpensThem_AndTheMonthlyPaymentCoversExactlyThose()
    {
        var op = await NewCafeAsync();

        var plan = await SaveAsync(op(), new
        {
            currency = "eur",
            priceByHand = false,
            modules = new object[]
            {
                new { key = ModuleKeys.Core, on = true, price = 20m },
                new { key = ModuleKeys.Analytics, on = true, price = 10m },
                new { key = ModuleKeys.Marketing, on = true, price = 15m },
                new { key = ModuleKeys.Loyalty, on = false, price = 12.5m },
            },
        });
        Assert.Equal(45m, plan.GetProperty("total").GetDecimal());
        Assert.Equal(45m, plan.GetProperty("monthlyPrice").GetDecimal());
        Assert.Equal(12.5m, ByKey(plan)[ModuleKeys.Loyalty].GetProperty("price").GetDecimal());
        Assert.False(ByKey(plan)[ModuleKeys.Loyalty].GetProperty("on").GetBoolean());

        Assert.Equal(HttpStatusCode.OK, (await op().GetAsync("/api/v1/admin/dashboard")).StatusCode);

        var billing = await op().GetFromJsonAsync<JsonElement>("/api/v1/platform/billing");
        Assert.Equal(45m, billing.GetProperty("monthlyPrice").GetDecimal());
        Assert.Equal("eur", billing.GetProperty("currency").GetString());
        Assert.Equal([ModuleKeys.Marketing, ModuleKeys.Analytics],
            billing.GetProperty("modules").EnumerateArray().Select(m => m.GetString()));

        // Switching one off takes it out of the price and out of the payment; modules not sent keep their state.
        plan = await SaveAsync(op(), new
        {
            currency = "eur",
            priceByHand = false,
            modules = new object[] { new { key = ModuleKeys.Analytics, on = false, price = 10m } },
        });
        Assert.Equal(35m, plan.GetProperty("monthlyPrice").GetDecimal());
        Assert.True(ByKey(plan)[ModuleKeys.Marketing].GetProperty("on").GetBoolean());
        Assert.Equal(HttpStatusCode.Forbidden, (await op().GetAsync("/api/v1/admin/dashboard")).StatusCode);
        billing = await op().GetFromJsonAsync<JsonElement>("/api/v1/platform/billing");
        Assert.Equal([ModuleKeys.Marketing], billing.GetProperty("modules").EnumerateArray().Select(m => m.GetString()));
    }

    [Fact]
    public async Task PriceByHand_OverridesTheTotal()
    {
        var op = await NewCafeAsync();
        var plan = await SaveAsync(op(), new
        {
            currency = "usd",
            priceByHand = true,
            monthlyPrice = 99m,
            modules = new object[]
            {
                new { key = ModuleKeys.Core, on = true, price = 20m },
                new { key = ModuleKeys.Push, on = true, price = 5m },
            },
        });
        Assert.Equal(25m, plan.GetProperty("total").GetDecimal());
        Assert.Equal(99m, plan.GetProperty("monthlyPrice").GetDecimal());
        Assert.True(plan.GetProperty("priceByHand").GetBoolean());

        // Back to automatic: the price follows the modules again.
        plan = await SaveAsync(op(), new { currency = "usd", priceByHand = false, modules = Array.Empty<object>() });
        Assert.Equal(25m, plan.GetProperty("monthlyPrice").GetDecimal());
    }

    [Fact]
    public async Task Plan_RejectsUnknownModulesAndBadPrices()
    {
        var op = await NewCafeAsync();
        async Task<HttpStatusCode> Put(object body) => (await op().PutAsJsonAsync("/api/v1/platform/module-plan", body)).StatusCode;

        Assert.Equal(HttpStatusCode.BadRequest, await Put(new { currency = "usd", modules = new[] { new { key = "teleport", on = true, price = 1m } } }));
        Assert.Equal(HttpStatusCode.BadRequest, await Put(new { currency = "usd", modules = new[] { new { key = ModuleKeys.Push, on = true, price = -1m } } }));
        Assert.Equal(HttpStatusCode.BadRequest, await Put(new { currency = "usd", modules = new[] { new { key = ModuleKeys.Push, on = true, price = 1.234m } } }));
        Assert.Equal(HttpStatusCode.BadRequest, await Put(new { currency = "rub", modules = Array.Empty<object>() }));
        Assert.Equal(HttpStatusCode.BadRequest, await Put(new { currency = "usd", priceByHand = true, monthlyPrice = 0m, modules = Array.Empty<object>() }));
    }

    [Fact]
    public async Task OnlyTheOperatorCanChangeThePlan()
    {
        var token = await ApiHelpers.CreateAdminTokenAsync(factory.Services, factory.CreateClient());
        var client = factory.CreateClient();
        ApiHelpers.Authorize(client, token);
        var res = await client.PutAsJsonAsync("/api/v1/platform/module-plan", new { currency = "usd", modules = Array.Empty<object>() });
        Assert.Equal(HttpStatusCode.Forbidden, res.StatusCode);
    }
}
