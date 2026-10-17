using System.Net;
using System.Net.Http.Headers;
using Microsoft.Extensions.DependencyInjection;
using Yurt.Application.Common.Interfaces;
using Yurt.Domain.Entities;
using Yurt.Infrastructure.Persistence;
using Yurt.IntegrationTests.Helpers;

namespace Yurt.IntegrationTests.Tests;

[Collection("Integration")]
public class TenantOriginTests(YurtWebAppFactory factory)
{
    private async Task<(string Slug, string Domain)> CreateCafeAsync()
    {
        var id = Guid.NewGuid().ToString("N")[..8];
        var cafe = (Slug: $"c{id}", Domain: $"web-{id}.test");
        using (var scope = factory.Services.CreateScope())
        {
            var db = scope.ServiceProvider.GetRequiredService<ApplicationDbContext>();
            db.Tenants.Add(new Tenant { Slug = cafe.Slug, Name = cafe.Slug, Domain = cafe.Domain });
            await db.SaveChangesAsync();
        }
        factory.Services.GetRequiredService<ITenantDirectory>().Invalidate();
        return cafe;
    }

    [Fact]
    public async Task SlugHeaderAndOrigin_SelectTheCafe_SoTheDefaultCafesTokenIsRejected()
    {
        var phone = $"+7{Random.Shared.NextInt64(7000000000, 7999999999)}";
        var (token, _) = await ApiHelpers.CreateCustomerAsync(factory.CreateClient(), phone);
        var cafe = await CreateCafeAsync();

        HttpClient Client(Action<HttpRequestHeaders> set)
        {
            var c = factory.CreateClient();
            c.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", token);
            set(c.DefaultRequestHeaders);
            return c;
        }

        Assert.Equal(HttpStatusCode.OK, (await Client(_ => { }).GetAsync("/api/v1/orders/active")).StatusCode);
        Assert.Equal(HttpStatusCode.Unauthorized,
            (await Client(h => h.Add("X-Tenant", cafe.Slug)).GetAsync("/api/v1/orders/active")).StatusCode);
        Assert.Equal(HttpStatusCode.Unauthorized,
            (await Client(h => h.Add("Origin", $"https://{cafe.Domain}")).GetAsync("/api/v1/orders/active")).StatusCode);
    }

    [Fact]
    public async Task Cors_AllowsACafeDomain_ButNotAStranger()
    {
        var cafe = await CreateCafeAsync();

        async Task<string?> AllowedOriginAsync(string origin)
        {
            var req = new HttpRequestMessage(HttpMethod.Options, "/api/v1/orders/active");
            req.Headers.Add("Origin", origin);
            req.Headers.Add("Access-Control-Request-Method", "GET");
            var res = await factory.CreateClient().SendAsync(req);
            return res.Headers.TryGetValues("Access-Control-Allow-Origin", out var v) ? v.First() : null;
        }

        Assert.Equal($"https://{cafe.Domain}", await AllowedOriginAsync($"https://{cafe.Domain}"));
        Assert.Null(await AllowedOriginAsync("https://stranger.example"));
    }
}
