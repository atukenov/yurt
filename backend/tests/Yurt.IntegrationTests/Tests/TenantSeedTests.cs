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

        var tenants = await db.Tenants.ToListAsync();

        var tenant = Assert.Single(tenants);
        Assert.Equal(Tenant.DefaultId, tenant.Id);
        Assert.Equal(Tenant.DefaultSlug, tenant.Slug);
        Assert.True(tenant.IsActive);
        Assert.Null(tenant.Domain);
    }
}
