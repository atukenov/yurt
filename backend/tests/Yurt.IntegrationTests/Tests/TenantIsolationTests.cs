using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Yurt.Application.Common.Interfaces;
using Yurt.Domain.Entities;
using Yurt.Infrastructure.Persistence;

namespace Yurt.IntegrationTests.Tests;

/// <summary>One cafe can never read another cafe's menu or locations (Phase 5, tables filtered so far).</summary>
[Collection("Integration")]
public class TenantIsolationTests(YurtWebAppFactory factory)
{
    private ApplicationDbContext DbFor(Guid tenantId, string slug, out IServiceScope scope)
    {
        scope = factory.Services.CreateScope();
        scope.ServiceProvider.GetRequiredService<ITenantContext>().Set(tenantId, slug);
        return scope.ServiceProvider.GetRequiredService<ApplicationDbContext>();
    }

    private async Task<Guid> CreateTenantAsync(string slug)
    {
        using var scope = factory.Services.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<ApplicationDbContext>();
        var tenant = new Tenant { Slug = slug, Name = slug };
        db.Tenants.Add(tenant);
        await db.SaveChangesAsync();
        return tenant.Id;
    }

    [Fact]
    public async Task NewRows_AreStampedWithTheCurrentTenant_AndHiddenFromOthers()
    {
        var slugA = $"a-{Guid.NewGuid():N}"[..12];
        var slugB = $"b-{Guid.NewGuid():N}"[..12];
        var a = await CreateTenantAsync(slugA);
        var b = await CreateTenantAsync(slugB);
        var name = $"Cafe {Guid.NewGuid():N}";

        using (var dbA = DbFor(a, slugA, out var scopeA))
        {
            using var _ = scopeA;
            var category = new MenuCategory { Name = name };
            dbA.MenuCategories.Add(category);
            dbA.Locations.Add(new Location { Name = name, Address = "x", WorkingHours = "x", ContactPhone = "x" });
            await dbA.SaveChangesAsync();
            Assert.Equal(a, category.TenantId);
            Assert.Equal(1, await dbA.MenuCategories.CountAsync(c => c.Name == name));
        }

        using (var dbB = DbFor(b, slugB, out var scopeB))
        {
            using var _ = scopeB;
            Assert.Equal(0, await dbB.MenuCategories.CountAsync(c => c.Name == name));
            Assert.Equal(0, await dbB.Locations.CountAsync(l => l.Name == name));
        }

        using (var dbDefault = DbFor(Tenant.DefaultId, Tenant.DefaultSlug, out var scopeD))
        {
            using var _ = scopeD;
            Assert.Equal(0, await dbDefault.MenuCategories.CountAsync(c => c.Name == name));
            Assert.Equal(0, await dbDefault.Locations.CountAsync(l => l.Name == name));
        }
    }

    [Fact]
    public async Task SeededMenuAndLocations_BelongToTheDefaultTenant()
    {
        using var db = DbFor(Tenant.DefaultId, Tenant.DefaultSlug, out var scope);
        using var _ = scope;

        Assert.True(await db.Locations.AnyAsync());
        Assert.True(await db.MenuItems.AnyAsync());
        Assert.Equal(await db.Locations.IgnoreQueryFilters().CountAsync(), await db.Locations.CountAsync());
    }
}
