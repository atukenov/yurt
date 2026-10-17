using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Yurt.Application.Common.Interfaces;
using Yurt.Domain;
using Yurt.Domain.Entities;
using Yurt.Domain.Enums;
using Yurt.Infrastructure.Persistence;

namespace Yurt.IntegrationTests.Tests;

/// <summary>One cafe can never read another cafe's menu or locations and orders and promotions (Phase 5, tables filtered so far).</summary>
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
    public async Task OrdersAndPromotions_AreStampedAndHiddenFromOtherCafes()
    {
        var slugA = $"a-{Guid.NewGuid():N}"[..12];
        var slugB = $"b-{Guid.NewGuid():N}"[..12];
        var a = await CreateTenantAsync(slugA);
        var b = await CreateTenantAsync(slugB);
        var title = $"Promo {Guid.NewGuid():N}";
        Guid orderId;

        using (var dbA = DbFor(a, slugA, out var scopeA))
        {
            using var _ = scopeA;
            var customer = new CustomerUser { MobileNumber = $"+7{Random.Shared.NextInt64(7000000000, 7999999999)}", PinHash = "x", FirstName = "A", LastName = "B" };
            var location = new Location { Name = title, Address = "x", WorkingHours = "x", ContactPhone = "x" };
            var order = new Order { CustomerUser = customer, Location = location, Subtotal = 10, Total = 10 };
            dbA.Promotions.Add(new Promotion { Title = title, Description = "d" });
            dbA.Orders.Add(order);
            await dbA.SaveChangesAsync();
            orderId = order.Id;
            Assert.Equal(a, order.TenantId);
            Assert.Equal(1, await dbA.Orders.CountAsync(o => o.Id == orderId));
            Assert.Equal(1, await dbA.Promotions.CountAsync(p => p.Title == title));
        }

        using (var dbB = DbFor(b, slugB, out var scopeB))
        {
            using var _ = scopeB;
            Assert.Equal(0, await dbB.Orders.CountAsync(o => o.Id == orderId));
            Assert.Equal(0, await dbB.Promotions.CountAsync(p => p.Title == title));
        }
    }

    [Fact]
    public async Task Customers_AreSeparatePerCafe_AndTheSamePhoneCanJoinTwoCafes()
    {
        var slugA = $"a-{Guid.NewGuid():N}"[..12];
        var slugB = $"b-{Guid.NewGuid():N}"[..12];
        var a = await CreateTenantAsync(slugA);
        var b = await CreateTenantAsync(slugB);
        var phone = $"+7{Random.Shared.NextInt64(7000000000, 7999999999)}";

        foreach (var (id, slug) in new[] { (a, slugA), (b, slugB) })
        {
            using var db = DbFor(id, slug, out var scope);
            using var _ = scope;
            db.CustomerUsers.Add(new CustomerUser { MobileNumber = phone, PinHash = "x", FirstName = "A", LastName = "B" });
            await db.SaveChangesAsync(); // would fail on a global unique index
            var own = await db.CustomerUsers.Where(c => c.MobileNumber == phone).ToListAsync();
            Assert.Equal(id, Assert.Single(own).TenantId);
        }
    }

    [Fact]
    public async Task PlatformOperators_AreVisibleFromEveryCafe_ButCafeAdminsAreNot()
    {
        var slugA = $"a-{Guid.NewGuid():N}"[..12];
        var slugB = $"b-{Guid.NewGuid():N}"[..12];
        var a = await CreateTenantAsync(slugA);
        var b = await CreateTenantAsync(slugB);
        var op = $"op-{Guid.NewGuid():N}"[..16];
        var admin = $"adm-{Guid.NewGuid():N}"[..16];

        using (var dbA = DbFor(a, slugA, out var scopeA))
        {
            using var _ = scopeA;
            dbA.AdminUsers.Add(new AdminUser { Username = op, PasswordHash = "x", Role = AdminRole.SuperAdmin });
            dbA.AdminUsers.Add(new AdminUser { Username = admin, PasswordHash = "x", Role = AdminRole.Admin });
            await dbA.SaveChangesAsync();
        }

        using var dbB = DbFor(b, slugB, out var scopeB);
        using var __ = scopeB;
        Assert.Equal(1, await dbB.AdminUsers.CountAsync(u => u.Username == op));
        Assert.Equal(0, await dbB.AdminUsers.CountAsync(u => u.Username == admin));
    }

    [Fact]
    public async Task ModuleLicenses_AreSwitchedPerCafe()
    {
        var slugA = $"a-{Guid.NewGuid():N}"[..12];
        var slugB = $"b-{Guid.NewGuid():N}"[..12];
        var a = await CreateTenantAsync(slugA);
        var b = await CreateTenantAsync(slugB);

        using (var dbA = DbFor(a, slugA, out var scopeA))
        {
            using var _ = scopeA;
            dbA.ModuleLicenses.Add(new ModuleLicense { ModuleKey = ModuleKeys.Marketing, Enabled = false });
            await dbA.SaveChangesAsync();
        }

        var gate = factory.Services.GetRequiredService<IModuleGate>();
        gate.Invalidate();

        using (DbFor(a, slugA, out var scopeA2))
        {
            using var _ = scopeA2;
            Assert.False(await gate.IsEnabledAsync(ModuleKeys.Marketing));
        }
        using (DbFor(b, slugB, out var scopeB))
        {
            using var _ = scopeB;
            Assert.True(await gate.IsEnabledAsync(ModuleKeys.Marketing));
        }
        using (DbFor(Tenant.DefaultId, Tenant.DefaultSlug, out var scopeD))
        {
            using var _ = scopeD;
            Assert.True(await gate.IsEnabledAsync(ModuleKeys.Marketing));
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

    [Fact]
    public void EveryTable_ExceptTenants_IsOwnedByACafe_AndFiltered()
    {
        using var scope = factory.Services.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<ApplicationDbContext>();

        var unguarded = db.Model.GetEntityTypes()
            .Where(t => t.ClrType != typeof(Tenant) && !t.IsOwned())
            .Where(t => !typeof(Yurt.Domain.Common.ITenantOwned).IsAssignableFrom(t.ClrType)
                        || t.GetQueryFilter() is null
                        || t.FindProperty(nameof(Yurt.Domain.Common.ITenantOwned.TenantId))!.IsNullable)
            .Select(t => t.ClrType.Name)
            .ToList();

        Assert.True(unguarded.Count == 0, "Tables with no cafe filter: " + string.Join(", ", unguarded));
    }
}
