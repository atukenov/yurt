using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Yurt.Domain.Entities;
using Yurt.Infrastructure.Persistence;

namespace Yurt.IntegrationTests.Tests;

[Collection("Integration")]
public class TenantSeedTests(YurtWebAppFactory factory)
{
    [Fact]
    public async Task DefaultTenant_IsSeededOnce_WithFixedId()
    {
        using var scope = factory.Services.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<ApplicationDbContext>();

        var tenant = Assert.Single(await db.Tenants.Where(t => t.Id == Tenant.DefaultId).ToListAsync());
        Assert.Equal(Tenant.DefaultId, tenant.Id);
        Assert.Equal(Tenant.DefaultSlug, tenant.Slug);
        Assert.True(tenant.IsActive);
        Assert.Null(tenant.Domain);
    }
}

[Collection("Integration")]
public class TenantMiddlewareTests(YurtWebAppFactory factory)
{
    [Theory]
    [InlineData("order.unknown-cafe.kz")]
    [InlineData("localhost")]
    public async Task UnknownHost_StillServesRequests_OnTheDefaultTenant(string host)
    {
        var client = factory.CreateClient();
        client.DefaultRequestHeaders.Host = host;

        var response = await client.GetAsync("/api/v1/app/config");

        Assert.Equal(System.Net.HttpStatusCode.OK, response.StatusCode);
    }
}
