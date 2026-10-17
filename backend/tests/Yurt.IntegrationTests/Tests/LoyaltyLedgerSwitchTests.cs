using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging.Abstractions;
using Yurt.Application.Common.Interfaces;
using Yurt.Application.Features.Loyalty;
using Yurt.Application.Features.Loyalty.Services;
using Yurt.Application.Features.Platform;
using Yurt.Domain.Entities;
using Yurt.Infrastructure.Persistence;
using Yurt.IntegrationTests.Helpers;

namespace Yurt.IntegrationTests.Tests;

/// <summary>Choosing a cafe's bonus wallet (iiko or own database) is safe, and the app works on the wallet chosen (Phase 6).</summary>
[Collection("Integration")]
public class LoyaltyLedgerSwitchTests(YurtWebAppFactory factory)
{
    private static string NewPhone() => "+7702" + Random.Shared.Next(1_000_000, 9_999_999);

    private IServiceScope ScopeFor(Guid tenantId, string slug)
    {
        var scope = factory.Services.CreateScope();
        scope.ServiceProvider.GetRequiredService<ITenantContext>().Set(tenantId, slug);
        return scope;
    }

    private static (LoyaltyLedgerSwitchService Switch, LoyaltyImportService Import) ServicesFor(
        IServiceScope scope, FakeIikoApiClient fake, bool iikoEnabled)
    {
        var sp = scope.ServiceProvider;
        var options = new IikoOptions { Enabled = iikoEnabled };
        var import = new LoyaltyImportService(
            sp.GetRequiredService<ApplicationDbContext>(), fake, options, sp.GetRequiredService<ITenantContext>(),
            sp.GetRequiredService<IAuditLogService>(), NullLogger<LoyaltyImportService>.Instance);
        var sw = new LoyaltyLedgerSwitchService(
            sp.GetRequiredService<ApplicationDbContext>(), options, sp.GetRequiredService<ITenantContext>(),
            sp.GetRequiredService<ITenantDirectory>(), sp.GetRequiredService<IAuditLogService>(), import);
        return (sw, import);
    }

    private async Task<(Guid Id, string Slug)> NewCafeAsync()
    {
        using var scope = factory.Services.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<ApplicationDbContext>();
        var slug = $"w-{Guid.NewGuid():N}"[..12];
        var cafe = new Tenant { Slug = slug, Name = slug };
        db.Tenants.Add(cafe);
        await db.SaveChangesAsync();
        scope.ServiceProvider.GetRequiredService<ITenantDirectory>().Invalidate();
        return (cafe.Id, slug);
    }

    [Fact]
    public async Task IikoCafe_CannotSwitchWhileBalancesDiffer_ThenCan()
    {
        var phone = NewPhone();
        var fake = new FakeIikoApiClient();
        fake.SetBalance(phone, 75m);

        using var scope = ScopeFor(Tenant.DefaultId, "default");
        var db = scope.ServiceProvider.GetRequiredService<ApplicationDbContext>();
        db.CustomerUsers.Add(new CustomerUser
        {
            MobileNumber = phone, PinHash = "x", FirstName = "Aida", LastName = "K",
            IikoCustomerId = FakeIikoApiClient.CustomerId, IikoWalletId = FakeIikoApiClient.WalletId,
        });
        await db.SaveChangesAsync();
        // The default cafe is shared: customers other tests linked to iiko would show up in the comparison.
        await db.CustomerUsers.Where(u => u.MobileNumber != phone && u.IikoCustomerId != null)
            .ExecuteUpdateAsync(u => u.SetProperty(x => x.IikoCustomerId, (Guid?)null));
        var (sw, import) = ServicesFor(scope, fake, iikoEnabled: true);

        try
        {
            var blocked = await sw.SetAsync(LoyaltyLedgerModes.Native);
            Assert.Equal(409, blocked.StatusCode);

            await import.ImportAsync();
            var ok = await sw.SetAsync(LoyaltyLedgerModes.Native);
            Assert.True(ok.Succeeded);
            Assert.Equal(LoyaltyLedgerModes.Native, (await sw.GetAsync()).Data!.Mode);
        }
        finally
        {
            await sw.SetAsync(LoyaltyLedgerModes.Iiko); // the default cafe is shared with the other tests
        }
        Assert.Equal(LoyaltyLedgerModes.Iiko, (await sw.GetAsync()).Data!.Mode);
    }

    [Fact]
    public async Task CafeWithoutIiko_CanUseTheOwnWallet_ButNotIiko()
    {
        var (id, slug) = await NewCafeAsync();
        using var scope = ScopeFor(id, slug);
        var (sw, _) = ServicesFor(scope, new FakeIikoApiClient(), iikoEnabled: true);

        Assert.Equal(400, (await sw.SetAsync(LoyaltyLedgerModes.Iiko)).StatusCode);
        Assert.Equal(400, (await sw.SetAsync("other")).StatusCode);
        Assert.True((await sw.SetAsync(LoyaltyLedgerModes.Native)).Succeeded);
    }

    [Fact]
    public async Task LoyaltyService_OnTheOwnWallet_WorksWithoutIiko_AndSeesImportedPoints()
    {
        var (id, slug) = await NewCafeAsync();
        var phone = NewPhone();
        using var scope = ScopeFor(id, slug);
        var sp = scope.ServiceProvider;
        var (sw, _) = ServicesFor(scope, new FakeIikoApiClient(), iikoEnabled: true);
        Assert.True((await sw.SetAsync(LoyaltyLedgerModes.Native)).Succeeded);

        // A customer who has points in the own wallet, with ids left over from iiko.
        var db = sp.GetRequiredService<ApplicationDbContext>();
        var account = db.LoyaltyAccounts.Add(new LoyaltyAccount { Phone = phone, WalletId = Guid.NewGuid() }).Entity;
        db.LoyaltyEntries.Add(new LoyaltyEntry { AccountId = account.Id, Kind = LoyaltyEntryKind.Import, Amount = 50m });
        var user = db.CustomerUsers.Add(new CustomerUser
        {
            MobileNumber = phone, PinHash = "x", FirstName = "Aida", LastName = "K",
            IikoCustomerId = FakeIikoApiClient.CustomerId, IikoWalletId = FakeIikoApiClient.WalletId,
        }).Entity;
        await db.SaveChangesAsync();

        var balance = await sp.GetRequiredService<LoyaltyService>().GetBalanceAsync(user.Id);

        Assert.True(balance.Available);
        Assert.Equal(50m, balance.Balance);
    }
}
