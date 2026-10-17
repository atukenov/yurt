using System.Net;
using System.Net.Http.Headers;
using Microsoft.Extensions.DependencyInjection;
using Yurt.Application.Common.Interfaces;
using Yurt.Domain.Entities;
using Yurt.Domain.Enums;
using Yurt.Infrastructure.Persistence;
using Yurt.IntegrationTests.Helpers;

namespace Yurt.IntegrationTests.Tests;

[Collection("Integration")]
public class TenantTokenTests(YurtWebAppFactory factory)
{
    private async Task<string> CreateCafeWithDomainAsync()
    {
        var domain = $"cafe-{Guid.NewGuid():N}"[..14] + ".test";
        using (var scope = factory.Services.CreateScope())
        {
            var db = scope.ServiceProvider.GetRequiredService<ApplicationDbContext>();
            db.Tenants.Add(new Tenant { Slug = domain[..10], Name = domain, Domain = domain });
            await db.SaveChangesAsync();
        }
        factory.Services.GetRequiredService<ITenantDirectory>().Invalidate();
        return domain;
    }

    private HttpClient ClientOn(string? host, string token)
    {
        var client = factory.CreateClient();
        if (host is not null) client.DefaultRequestHeaders.Host = host;
        client.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", token);
        return client;
    }

    [Fact]
    public async Task CustomerToken_WorksOnItsOwnCafe_ButNotOnAnother()
    {
        var phone = $"+7{Random.Shared.NextInt64(7000000000, 7999999999)}";
        var (token, _) = await ApiHelpers.CreateCustomerAsync(factory.CreateClient(), phone);
        var other = await CreateCafeWithDomainAsync();

        Assert.Equal(HttpStatusCode.OK, (await ClientOn(null, token).GetAsync("/api/v1/orders/active")).StatusCode);
        Assert.Equal(HttpStatusCode.Unauthorized, (await ClientOn(other, token).GetAsync("/api/v1/orders/active")).StatusCode);
    }

    [Fact]
    public async Task AdminToken_IsAlsoBoundToItsCafe()
    {
        var token = await ApiHelpers.CreateAdminTokenAsync(factory.Services, factory.CreateClient());
        var other = await CreateCafeWithDomainAsync();

        Assert.Equal(HttpStatusCode.Unauthorized, (await ClientOn(other, token).GetAsync("/api/v1/admin/orders")).StatusCode);
    }

    [Fact]
    public async Task PlatformOperator_IsNotBlockedOnAnotherCafe()
    {
        var token = await ApiHelpers.CreateAdminTokenAsync(factory.Services, factory.CreateClient(),
            username: "platform-owner", password: "PlatformOwner@123", role: AdminRole.SuperAdmin);
        var other = await CreateCafeWithDomainAsync();

        var response = await ClientOn(other, token).GetAsync("/api/v1/platform/modules");

        Assert.NotEqual(HttpStatusCode.Unauthorized, response.StatusCode);
        Assert.NotEqual(HttpStatusCode.Forbidden, response.StatusCode);
    }
}
