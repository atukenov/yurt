using Microsoft.Extensions.DependencyInjection;
using Yurt.Application.Common.Interfaces;
using Yurt.Application.Features.Loyalty;
using Yurt.Domain;
using Yurt.Domain.Entities;
using Yurt.Infrastructure.Persistence;

namespace Yurt.IntegrationTests.Tests;

/// <summary>The bonus wallet kept in our own database behaves like the iiko one and never crosses cafes (Phase 6).</summary>
[Collection("Integration")]
public class NativeLoyaltyLedgerTests(YurtWebAppFactory factory)
{
    private NativeLoyaltyLedger LedgerFor(Guid tenantId, string slug, out IServiceScope scope)
    {
        scope = factory.Services.CreateScope();
        scope.ServiceProvider.GetRequiredService<ITenantContext>().Set(tenantId, slug);
        return new NativeLoyaltyLedger(scope.ServiceProvider.GetRequiredService<ApplicationDbContext>());
    }

    private static string NewPhone() => "+7700" + Random.Shared.Next(1_000_000, 9_999_999);

    private static async Task<(Guid account, Guid wallet)> JoinAsync(NativeLoyaltyLedger ledger, string phone)
    {
        var account = await ledger.CreateOrUpdateAccountAsync(phone, "Aida", "K", default);
        return (account, await ledger.EnrollAsync(account));
    }

    private static async Task<decimal> BalanceAsync(NativeLoyaltyLedger ledger, string phone)
        => (await ledger.GetAccountByPhoneAsync(phone))!.Balances.Single().Balance;

    [Fact]
    public async Task UnknownPhone_HasNoAccount()
    {
        var ledger = LedgerFor(Tenant.DefaultId, "default", out var scope);
        using (scope) Assert.Null(await ledger.GetAccountByPhoneAsync(NewPhone()));
    }

    [Fact]
    public async Task TopupChargeoffAndHold_MoveTheBalance()
    {
        var ledger = LedgerFor(Tenant.DefaultId, "default", out var scope);
        using var _ = scope;
        var phone = NewPhone();
        var (account, wallet) = await JoinAsync(ledger, phone);

        await ledger.TopupAsync(account, wallet, 100m, "earn");
        await ledger.ChargeoffAsync(account, wallet, 30m, "spend");
        var hold = await ledger.HoldAsync(account, wallet, 20m, "hold");
        Assert.Equal(50m, await BalanceAsync(ledger, phone));

        await ledger.CancelHoldAsync(hold);
        Assert.Equal(70m, await BalanceAsync(ledger, phone));

        var history = await ledger.GetTransactionsAsync(account, DateTime.UtcNow.AddDays(-1), DateTime.UtcNow.AddDays(1));
        Assert.Equal(4, history.Count);
        Assert.Equal(70m, history.Sum(t => t.Sum));
    }

    [Fact]
    public async Task ReleasingAHoldTwice_ReturnsThePointsOnce()
    {
        var ledger = LedgerFor(Tenant.DefaultId, "default", out var scope);
        using var _ = scope;
        var phone = NewPhone();
        var (account, wallet) = await JoinAsync(ledger, phone);
        await ledger.TopupAsync(account, wallet, 50m, null);

        var hold = await ledger.HoldAsync(account, wallet, 40m, null);
        await ledger.CancelHoldAsync(hold);
        await ledger.CancelHoldAsync(hold);

        Assert.Equal(50m, await BalanceAsync(ledger, phone));
    }

    [Fact]
    public async Task CannotSpendOrHoldMoreThanTheBalance()
    {
        var ledger = LedgerFor(Tenant.DefaultId, "default", out var scope);
        using var _ = scope;
        var phone = NewPhone();
        var (account, wallet) = await JoinAsync(ledger, phone);
        await ledger.TopupAsync(account, wallet, 10m, null);

        await Assert.ThrowsAsync<InvalidOperationException>(() => ledger.ChargeoffAsync(account, wallet, 11m, null));
        await Assert.ThrowsAsync<InvalidOperationException>(() => ledger.HoldAsync(account, wallet, 11m, null));
        await Assert.ThrowsAsync<ArgumentOutOfRangeException>(() => ledger.TopupAsync(account, wallet, 0m, null));
        Assert.Equal(10m, await BalanceAsync(ledger, phone));
    }

    [Fact]
    public async Task TheSamePhone_HasSeparateWalletsInTwoCafes()
    {
        Guid other;
        using (var s = factory.Services.CreateScope())
        {
            var db = s.ServiceProvider.GetRequiredService<ApplicationDbContext>();
            var tenant = new Tenant { Slug = "loyalty-" + Guid.NewGuid().ToString("N")[..8], Name = "Other" };
            db.Tenants.Add(tenant);
            await db.SaveChangesAsync();
            other = tenant.Id;
        }

        var phone = NewPhone();
        var first = LedgerFor(Tenant.DefaultId, "default", out var s1);
        var second = LedgerFor(other, "other", out var s2);
        using (s1) using (s2)
        {
            var (a1, w1) = await JoinAsync(first, phone);
            await first.TopupAsync(a1, w1, 80m, null);
            await JoinAsync(second, phone);

            Assert.Equal(80m, await BalanceAsync(first, phone));
            Assert.Equal(0m, await BalanceAsync(second, phone));
        }
    }

    [Fact]
    public async Task SimultaneousSpends_NeverTakeTheBalanceBelowZero()
    {
        var ledger = LedgerFor(Tenant.DefaultId, "default", out var scope);
        using var _ = scope;
        var phone = NewPhone();
        var (account, wallet) = await JoinAsync(ledger, phone);
        await ledger.TopupAsync(account, wallet, 100m, "earn");

        async Task<bool> TrySpendAsync()
        {
            var other = LedgerFor(Tenant.DefaultId, "default", out var s);
            using (s)
            {
                try { await other.ChargeoffAsync(account, wallet, 80m, "race"); return true; }
                catch (InvalidOperationException) { return false; }
            }
        }

        var results = await Task.WhenAll(Enumerable.Range(0, 6).Select(_ => Task.Run(TrySpendAsync)));

        Assert.Equal(1, results.Count(r => r));
        Assert.Equal(20m, await BalanceAsync(ledger, phone));
    }

    [Fact]
    public async Task SimultaneousReleasesOfOneHold_GiveThePointsBackOnce()
    {
        var ledger = LedgerFor(Tenant.DefaultId, "default", out var scope);
        using var _ = scope;
        var phone = NewPhone();
        var (account, wallet) = await JoinAsync(ledger, phone);
        await ledger.TopupAsync(account, wallet, 100m, "earn");
        var hold = await ledger.HoldAsync(account, wallet, 40m, "hold");

        await Task.WhenAll(Enumerable.Range(0, 6).Select(_ => Task.Run(async () =>
        {
            var other = LedgerFor(Tenant.DefaultId, "default", out var s);
            using (s) await other.CancelHoldAsync(hold);
        })));

        Assert.Equal(100m, await BalanceAsync(ledger, phone));
    }
}
