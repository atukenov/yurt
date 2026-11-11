using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging.Abstractions;
using Yurt.Application.Common;
using Yurt.Application.Common.Interfaces;
using Yurt.Domain.Common;
using Yurt.Domain.Entities;
using Yurt.Domain.Enums;
using Yurt.Infrastructure.Persistence;

namespace Yurt.IntegrationTests.Tests;

/// <summary>
/// The demo seed fills every cafe table, adds nothing the second time, and keeps its numbers consistent enough for the
/// reports. It runs on a fresh cafe of its own so the other tests' default cafe stays as it is.
/// </summary>
[Collection("Integration")]
public class DemoDataSeederTests(YurtWebAppFactory factory)
{
    /// <summary>Cafe tables the demo leaves empty on purpose: secrets, sign-in codes and sessions, billing, uploads,
    /// and module switches (DbSeeder and the operator own those).</summary>
    private static readonly HashSet<string> LeftEmpty =
    [
        nameof(TenantSecret), nameof(RefreshToken), nameof(PhoneVerification), nameof(CafeBilling), nameof(Picture),
        nameof(ModuleLicense), nameof(PlatformSetting),
    ];

    private async Task<Tenant> NewCafeAsync()
    {
        var slug = $"demo-{Guid.NewGuid():N}"[..12];
        using (var scope = factory.Services.CreateScope())
        {
            var db = scope.ServiceProvider.GetRequiredService<ApplicationDbContext>();
            db.Tenants.Add(new Tenant { Slug = slug, Name = "Demo test cafe" });
            await db.SaveChangesAsync();
        }
        factory.Services.GetRequiredService<ITenantDirectory>().Invalidate();
        using (var scope = factory.Services.CreateScope())
            return await scope.ServiceProvider.GetRequiredService<ApplicationDbContext>().Tenants.SingleAsync(t => t.Slug == slug);
    }

    private async Task SeedAsync(Tenant cafe)
    {
        using var scope = factory.Services.CreateScope();
        scope.ServiceProvider.GetRequiredService<ITenantContext>().Set(cafe.Id, cafe.Slug);
        var db = scope.ServiceProvider.GetRequiredService<ApplicationDbContext>();
        await DemoDataSeeder.SeedAsync(db, cafe, NullLogger.Instance);
    }

    private async Task<Dictionary<string, int>> CountRowsAsync(Guid tenantId)
    {
        using var scope = factory.Services.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<ApplicationDbContext>();
        var counts = new Dictionary<string, int>();
        var conn = db.Database.GetDbConnection();
        await conn.OpenAsync();
        foreach (var type in db.Model.GetEntityTypes().Where(t => typeof(ITenantOwned).IsAssignableFrom(t.ClrType)))
        {
            await using var cmd = conn.CreateCommand();
            cmd.CommandText = $"SELECT COUNT(*) FROM \"{type.GetTableName()}\" WHERE \"TenantId\" = @t";
            var p = cmd.CreateParameter();
            p.ParameterName = "t";
            p.Value = tenantId;
            cmd.Parameters.Add(p);
            counts[type.ClrType.Name] = Convert.ToInt32(await cmd.ExecuteScalarAsync());
        }
        return counts;
    }

    [Fact]
    public async Task FillsEveryCafeTable_AndAddsNothingTheSecondTime()
    {
        var cafe = await NewCafeAsync();

        await SeedAsync(cafe);
        var first = await CountRowsAsync(cafe.Id);

        var empty = first.Where(c => c.Value == 0 && !LeftEmpty.Contains(c.Key)).Select(c => c.Key).ToList();
        Assert.True(empty.Count == 0, "The demo seed leaves these tables empty: " + string.Join(", ", empty)
            + ". Add demo rows for them in DemoDataSeeder, or list them in LeftEmpty with a reason.");
        Assert.True(first[nameof(Order)] > 500, $"Expected a couple of months of orders, got {first[nameof(Order)]}.");

        await SeedAsync(cafe);
        var second = await CountRowsAsync(cafe.Id);
        var grew = second.Where(c => c.Value != first[c.Key]).Select(c => $"{c.Key} {first[c.Key]} → {c.Value}").ToList();
        Assert.True(grew.Count == 0, "Running the demo seed again changed: " + string.Join(", ", grew));
    }

    [Fact]
    public async Task DemoNumbers_AddUp()
    {
        var cafe = await NewCafeAsync();
        await SeedAsync(cafe);

        using var scope = factory.Services.CreateScope();
        scope.ServiceProvider.GetRequiredService<ITenantContext>().Set(cafe.Id, cafe.Slug);
        var db = scope.ServiceProvider.GetRequiredService<ApplicationDbContext>();

        // Every order's total is its lines less the discount, and every completed one has a cost for the profit report.
        var orders = await db.Orders.Include(o => o.Items).ThenInclude(i => i.Toppings).ToListAsync();
        Assert.All(orders, o =>
        {
            Assert.Equal(o.Items.Sum(i => i.LineTotal), o.Subtotal);
            Assert.Equal(o.Subtotal - o.DiscountAmount, o.Total);
            Assert.All(o.Items, i => Assert.Equal((i.UnitPrice) * i.Quantity, i.LineTotal));
        });
        Assert.All(orders.Where(o => o.Status == OrderStatus.Completed).SelectMany(o => o.Items), i => Assert.NotNull(i.UnitCost));
        Assert.Contains(orders, o => o.Status is OrderStatus.Created or OrderStatus.Preparing);

        // Split payments add up to the order total; only one shift per branch is open.
        var splits = await db.OrderPayments.GroupBy(p => p.OrderId).Select(g => new { g.Key, Sum = g.Sum(p => p.Amount) }).ToListAsync();
        Assert.NotEmpty(splits);
        Assert.All(splits, s => Assert.Equal(orders.Single(o => o.Id == s.Key).Total, s.Sum));
        Assert.True(await db.Shifts.CountAsync(s => s.ClosedAt == null) <= await db.Locations.CountAsync());

        // Demo staff can sign in with the documented password.
        var staff = await db.AdminUsers.Where(u => u.Username.StartsWith(cafe.Slug + ".") && u.IsActive).ToListAsync();
        Assert.Contains(staff, u => u.Role == AdminRole.Admin);
        Assert.All(staff, u => Assert.True(BCrypt.Net.BCrypt.Verify(DemoDataSeeder.StaffPassword, u.PasswordHash)));
    }
}
