using System.Net.Http.Json;
using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Yurt.Application.Common.Interfaces;
using Yurt.Application.Features.Loyalty;
using Yurt.Application.Features.Loyalty.Services;
using Yurt.Domain.Enums;
using Yurt.IntegrationTests.Helpers;

namespace Yurt.IntegrationTests.Tests;

[Collection("Integration")]
public class LoyaltyTests(YurtWebAppFactory factory)
{
    private static readonly Guid LocationId = Guid.Parse("11111111-0000-0000-0000-000000000001");

    private record LoyaltyBalanceResult(
        bool Enabled, bool Available, bool Linked, decimal? Balance, decimal EarnPercent);

    private record LoyaltyTransactionResult(
        DateTime WhenCreated, int? OrderNumber, decimal? OrderTotal, decimal? KztPaid,
        decimal BonusSpent, decimal BonusEarned, decimal? BalanceAfter);

    private record LoyaltyHistoryResult(
        bool Enabled, bool Available, bool Linked, List<LoyaltyTransactionResult> Transactions);

    private record CompletedOrder(Guid Id, decimal Total, string CustomerToken);

    // ── Feature flag off (default config) ────────────────────────────────────

    [Fact]
    public async Task LoyaltyDisabled_BalanceEndpoint_ReturnsDisabled()
    {
        var client = factory.CreateClient();
        var (token, _) = await ApiHelpers.CreateCustomerAsync(client, "+77001000801");
        ApiHelpers.Authorize(client, token);

        var result = await client.GetFromJsonAsync<LoyaltyBalanceResult>(
            "/api/v1/loyalty/me", ApiHelpers.JsonOpts);

        Assert.NotNull(result);
        Assert.False(result.Enabled);
        Assert.Null(result.Balance);
    }

    [Fact]
    public async Task LoyaltyDisabled_CompletedOrder_DoesNotCreditPoints()
    {
        var client    = factory.CreateClient();
        var completed = await PlaceAndCompleteOrderAsync(factory, client, "+77001000802");

        await using var scope = factory.Services.CreateAsyncScope();
        var db    = scope.ServiceProvider.GetRequiredService<IApplicationDbContext>();
        var order = await db.Orders.FirstAsync(o => o.Id == completed.Id);
        Assert.Null(order.LoyaltyPointsEarned);
    }

    // ── Feature flag on, iiko faked ──────────────────────────────────────────

    [Fact]
    public async Task LoyaltyEnabled_CompletedOrder_CreditsPointsOnceAndShowsBalance()
    {
        var fake = new FakeIikoApiClient();
        using var enabledFactory = factory.WithWebHostBuilder(builder =>
            builder.ConfigureServices(services =>
            {
                // Last registration wins for both the options and the client
                services.AddSingleton(EnabledOptions());
                services.AddSingleton<IIikoApiClient>(fake);
            }));

        var client    = enabledFactory.CreateClient();
        var completed = await PlaceAndCompleteOrderAsync(enabledFactory, client, "+77001000803");

        // Exactly one topup, 5% of the total, tagged with the order id
        var call = Assert.Single(fake.TopupCalls);
        Assert.Equal(FakeIikoApiClient.CustomerId, call.CustomerId);
        Assert.Equal(FakeIikoApiClient.WalletId, call.WalletId);
        Assert.Equal(Math.Round(completed.Total * 0.05m, 2, MidpointRounding.ToZero), call.Sum);
        Assert.Contains(completed.Id.ToString(), call.Comment);

        // Points recorded on the order, link ids persisted on the customer
        await using (var scope = enabledFactory.Services.CreateAsyncScope())
        {
            var db    = scope.ServiceProvider.GetRequiredService<IApplicationDbContext>();
            var order = await db.Orders.Include(o => o.CustomerUser).FirstAsync(o => o.Id == completed.Id);
            Assert.Equal(call.Sum, order.LoyaltyPointsEarned);
            Assert.Equal(FakeIikoApiClient.CustomerId, order.CustomerUser.IikoCustomerId);
            Assert.Equal(FakeIikoApiClient.WalletId, order.CustomerUser.IikoWalletId);
        }

        // Balance endpoint reflects the credited points
        ApiHelpers.Authorize(client, completed.CustomerToken);
        var balance = await client.GetFromJsonAsync<LoyaltyBalanceResult>(
            "/api/v1/loyalty/me", ApiHelpers.JsonOpts);
        Assert.NotNull(balance);
        Assert.True(balance.Enabled);
        Assert.True(balance.Available);
        Assert.True(balance.Linked);
        Assert.Equal(call.Sum, balance.Balance);
        Assert.Equal(5m, balance.EarnPercent);
    }

    [Fact]
    public async Task AdminLoyaltyEndpoint_UnlinkedCustomer_ReportsNotLinkedWithoutCreatingIikoCustomer()
    {
        var fake = new FakeIikoApiClient();
        using var enabledFactory = factory.WithWebHostBuilder(builder =>
            builder.ConfigureServices(services =>
            {
                services.AddSingleton(EnabledOptions());
                services.AddSingleton<IIikoApiClient>(fake);
            }));

        var client = enabledFactory.CreateClient();
        var (_, customerId) = await ApiHelpers.CreateCustomerAsync(client, "+77001000805");

        var adminToken = await ApiHelpers.CreateAdminTokenAsync(enabledFactory.Services, client);
        ApiHelpers.Authorize(client, adminToken);

        var balance = await client.GetFromJsonAsync<LoyaltyBalanceResult>(
            $"/api/v1/admin/customers/{customerId}/loyalty", ApiHelpers.JsonOpts);

        // Admin viewing must not create the customer in iiko as a side effect
        Assert.NotNull(balance);
        Assert.True(balance.Enabled);
        Assert.True(balance.Available);
        Assert.False(balance.Linked);
        Assert.Null(balance.Balance);

        await using var scope = enabledFactory.Services.CreateAsyncScope();
        var db   = scope.ServiceProvider.GetRequiredService<IApplicationDbContext>();
        var user = await db.CustomerUsers.FirstAsync(u => u.Id == customerId);
        Assert.Null(user.IikoCustomerId);
    }

    [Fact]
    public async Task AdminLoyaltyEndpoint_UnlinkedCustomerAlreadyInIiko_SelfHealsLinkAndReturnsBalance()
    {
        // Simulates a customer enrolled in iiko's loyalty program through another channel
        // (or whose local link ids were lost) — iiko already knows their phone number, our
        // CustomerUser row just doesn't have IikoCustomerId/IikoWalletId recorded yet.
        var fake = new FakeIikoApiClient();
        const string phone = "+77001000808";
        fake.SetBalance(phone, 42m);

        using var enabledFactory = factory.WithWebHostBuilder(builder =>
            builder.ConfigureServices(services =>
            {
                services.AddSingleton(EnabledOptions());
                services.AddSingleton<IIikoApiClient>(fake);
            }));

        var client = enabledFactory.CreateClient();
        var (_, customerId) = await ApiHelpers.CreateCustomerAsync(client, phone);

        var adminToken = await ApiHelpers.CreateAdminTokenAsync(enabledFactory.Services, client);
        ApiHelpers.Authorize(client, adminToken);

        var balance = await client.GetFromJsonAsync<LoyaltyBalanceResult>(
            $"/api/v1/admin/customers/{customerId}/loyalty", ApiHelpers.JsonOpts);

        // Viewing the customer's admin page discovers and adopts the existing iiko link
        Assert.NotNull(balance);
        Assert.True(balance.Linked);
        Assert.Equal(42m, balance.Balance);

        await using var scope = enabledFactory.Services.CreateAsyncScope();
        var db   = scope.ServiceProvider.GetRequiredService<IApplicationDbContext>();
        var user = await db.CustomerUsers.FirstAsync(u => u.Id == customerId);
        Assert.Equal(FakeIikoApiClient.CustomerId, user.IikoCustomerId);
        Assert.Equal(FakeIikoApiClient.WalletId, user.IikoWalletId);
    }

    [Fact]
    public async Task StaleCachedWalletId_ProgramAddReturnsWrongId_SelfHealStillResolvesCorrectBalance()
    {
        // Reproduces a real bug report: customer/info correctly returns one wallet with a
        // real balance, but the cached IikoWalletId is stale (e.g. from before the
        // userWalletId-priority fix). The old self-heal "repaired" this by re-querying
        // program/add — but for an already-enrolled customer, iiko's program/add response
        // isn't reliably the same shape as for a fresh enrollment, and can hand back the
        // shared program walletId instead of the customer's own userWalletId. That
        // "repair" then points at a wallet that isn't in walletBalances, so every future
        // balance check still silently sums to zero. Self-heal must trust the unambiguous
        // single-wallet customer/info response instead of program/add's answer.
        var fake = new FakeIikoApiClient();
        const string phone = "+77001000810";
        fake.SetBalance(phone, 272.5m);
        var wrongIdFromProgramAdd = Guid.NewGuid();
        fake.AddToProgramReturnsWrongWalletId = wrongIdFromProgramAdd;

        using var enabledFactory = factory.WithWebHostBuilder(builder =>
            builder.ConfigureServices(services =>
            {
                services.AddSingleton(EnabledOptions());
                services.AddSingleton<IIikoApiClient>(fake);
            }));

        var client = enabledFactory.CreateClient();
        var (_, customerId) = await ApiHelpers.CreateCustomerAsync(client, phone);

        await using (var scope = enabledFactory.Services.CreateAsyncScope())
        {
            var db = scope.ServiceProvider.GetRequiredService<IApplicationDbContext>();
            var user = await db.CustomerUsers.FirstAsync(u => u.Id == customerId);
            user.IikoCustomerId = FakeIikoApiClient.CustomerId;
            user.IikoWalletId = Guid.NewGuid(); // stale — matches nothing in walletBalances
            await db.SaveChangesAsync();
        }

        var adminToken = await ApiHelpers.CreateAdminTokenAsync(enabledFactory.Services, client);
        ApiHelpers.Authorize(client, adminToken);

        var balance = await client.GetFromJsonAsync<LoyaltyBalanceResult>(
            $"/api/v1/admin/customers/{customerId}/loyalty", ApiHelpers.JsonOpts);

        Assert.NotNull(balance);
        Assert.True(balance.Linked);
        Assert.Equal(272.5m, balance.Balance);

        await using var scopeAfter = enabledFactory.Services.CreateAsyncScope();
        var dbAfter = scopeAfter.ServiceProvider.GetRequiredService<IApplicationDbContext>();
        var userAfter = await dbAfter.CustomerUsers.FirstAsync(u => u.Id == customerId);
        Assert.Equal(FakeIikoApiClient.WalletId, userAfter.IikoWalletId);
        Assert.NotEqual(wrongIdFromProgramAdd, userAfter.IikoWalletId);
    }

    [Fact]
    public async Task AdminLoyaltyEndpoint_LinkedCustomer_ReturnsBalance()
    {
        var fake = new FakeIikoApiClient();
        using var enabledFactory = factory.WithWebHostBuilder(builder =>
            builder.ConfigureServices(services =>
            {
                services.AddSingleton(EnabledOptions());
                services.AddSingleton<IIikoApiClient>(fake);
            }));

        var client    = enabledFactory.CreateClient();
        var completed = await PlaceAndCompleteOrderAsync(enabledFactory, client, "+77001000806");
        var call      = Assert.Single(fake.TopupCalls);

        await using var scope = enabledFactory.Services.CreateAsyncScope();
        var db      = scope.ServiceProvider.GetRequiredService<IApplicationDbContext>();
        var order   = await db.Orders.FirstAsync(o => o.Id == completed.Id);

        var adminToken = await ApiHelpers.CreateAdminTokenAsync(enabledFactory.Services, client);
        ApiHelpers.Authorize(client, adminToken);

        var balance = await client.GetFromJsonAsync<LoyaltyBalanceResult>(
            $"/api/v1/admin/customers/{order.CustomerUserId}/loyalty", ApiHelpers.JsonOpts);

        Assert.NotNull(balance);
        Assert.True(balance.Linked);
        Assert.Equal(call.Sum, balance.Balance);
    }

    [Fact]
    public async Task AdminLoyaltyEndpoint_RejectsCustomerToken()
    {
        var client = factory.CreateClient();
        var (token, customerId) = await ApiHelpers.CreateCustomerAsync(client, "+77001000807");
        ApiHelpers.Authorize(client, token);

        var resp = await client.GetAsync($"/api/v1/admin/customers/{customerId}/loyalty");
        Assert.Equal(System.Net.HttpStatusCode.Forbidden, resp.StatusCode);
    }

    [Fact]
    public async Task StaleCachedWalletId_SelfHealsOnNextBalanceCheck()
    {
        // Simulates a customer linked before the wallet-id priority fix: their cached
        // IikoWalletId is some other guid that doesn't match any real wallet iiko
        // returns for them — this is exactly the "bonuses not showing" bug report,
        // since the balance filter matches nothing and silently sums to zero.
        var fake = new FakeIikoApiClient();
        var phone = "+77001000809";
        fake.SetBalance(phone, 42m);

        using var enabledFactory = factory.WithWebHostBuilder(builder =>
            builder.ConfigureServices(services =>
            {
                services.AddSingleton(EnabledOptions());
                services.AddSingleton<IIikoApiClient>(fake);
            }));

        var client = enabledFactory.CreateClient();
        var (token, customerId) = await ApiHelpers.CreateCustomerAsync(client, phone);

        var staleWalletId = Guid.NewGuid();
        await using (var scope = enabledFactory.Services.CreateAsyncScope())
        {
            var db = scope.ServiceProvider.GetRequiredService<IApplicationDbContext>();
            var user = await db.CustomerUsers.FirstAsync(u => u.Id == customerId);
            user.IikoCustomerId = FakeIikoApiClient.CustomerId;
            user.IikoWalletId = staleWalletId; // wrong on purpose
            await db.SaveChangesAsync();
        }

        ApiHelpers.Authorize(client, token);
        var balance = await client.GetFromJsonAsync<LoyaltyBalanceResult>(
            "/api/v1/loyalty/me", ApiHelpers.JsonOpts);

        // Self-healed: real balance is now visible, not masked by the stale id
        Assert.NotNull(balance);
        Assert.Equal(42m, balance.Balance);

        await using var verifyScope = enabledFactory.Services.CreateAsyncScope();
        var verifyDb = verifyScope.ServiceProvider.GetRequiredService<IApplicationDbContext>();
        var healed = await verifyDb.CustomerUsers.FirstAsync(u => u.Id == customerId);
        Assert.Equal(FakeIikoApiClient.WalletId, healed.IikoWalletId);
        Assert.NotEqual(staleWalletId, healed.IikoWalletId);
    }

    [Fact]
    public async Task LoyaltyEnabled_IikoDown_OrderCompletionStillSucceeds()
    {
        using var brokenFactory = factory.WithWebHostBuilder(builder =>
            builder.ConfigureServices(services =>
            {
                services.AddSingleton(EnabledOptions());
                services.AddSingleton<IIikoApiClient>(new ThrowingIikoApiClient());
            }));

        var client    = brokenFactory.CreateClient();
        var completed = await PlaceAndCompleteOrderAsync(brokenFactory, client, "+77001000804");

        await using var scope = brokenFactory.Services.CreateAsyncScope();
        var db    = scope.ServiceProvider.GetRequiredService<IApplicationDbContext>();
        var order = await db.Orders.FirstAsync(o => o.Id == completed.Id);
        Assert.Equal("Completed", order.Status.ToString());
        Assert.Null(order.LoyaltyPointsEarned); // not credited, but completion succeeded
    }

    // ── Earn-credit retry / safety-net sweep ────────────────────────────────

    [Fact]
    public async Task TopupFailsAtCompletion_MarksPendingCredit_RetrySweepCreditsPoints()
    {
        var fake = new FakeIikoApiClient();
        fake.FailTopup = true;
        using var enabledFactory = factory.WithWebHostBuilder(builder =>
            builder.ConfigureServices(services =>
            {
                services.AddSingleton(EnabledOptions());
                services.AddSingleton<IIikoApiClient>(fake);
            }));

        var client    = enabledFactory.CreateClient();
        var completed = await PlaceAndCompleteOrderAsync(enabledFactory, client, "+77001000920");

        await using (var scope = enabledFactory.Services.CreateAsyncScope())
        {
            var db    = scope.ServiceProvider.GetRequiredService<IApplicationDbContext>();
            var order = await db.Orders.FirstAsync(o => o.Id == completed.Id);
            Assert.Null(order.LoyaltyPointsEarned);
            Assert.Equal(LoyaltyPendingAction.Credit, order.LoyaltyPendingAction);
        }

        fake.FailTopup = false;
        await using (var scope = enabledFactory.Services.CreateAsyncScope())
        {
            var loyalty = scope.ServiceProvider.GetRequiredService<LoyaltyService>();
            await loyalty.RetryPendingAsync();
        }

        Assert.Single(fake.TopupCalls);
        await using (var scopeAfter = enabledFactory.Services.CreateAsyncScope())
        {
            var db    = scopeAfter.ServiceProvider.GetRequiredService<IApplicationDbContext>();
            var order = await db.Orders.FirstAsync(o => o.Id == completed.Id);
            Assert.NotNull(order.LoyaltyPointsEarned);
            Assert.Equal(LoyaltyPendingAction.None, order.LoyaltyPendingAction);
        }
    }

    [Fact]
    public async Task CompletedOrderWithNoCreditRecord_SweepRecoversTheMissedCredit()
    {
        var fake = new FakeIikoApiClient();
        using var enabledFactory = factory.WithWebHostBuilder(builder =>
            builder.ConfigureServices(services =>
            {
                services.AddSingleton(EnabledOptions());
                services.AddSingleton<IIikoApiClient>(fake);
            }));

        var client    = enabledFactory.CreateClient();
        var completed = await PlaceAndCompleteOrderAsync(enabledFactory, client, "+77001000921");
        fake.TopupCalls.Clear();

        // Simulate the gap: pretend the earn-credit attempt never happened and the
        // order completed well past the sweep's grace period.
        await using (var scope = enabledFactory.Services.CreateAsyncScope())
        {
            var db    = scope.ServiceProvider.GetRequiredService<IApplicationDbContext>();
            var order = await db.Orders.FirstAsync(o => o.Id == completed.Id);
            order.LoyaltyPointsEarned = null;
            order.CompletedAt = DateTime.UtcNow.AddMinutes(-20);
            await db.SaveChangesAsync();
        }

        await using (var scope = enabledFactory.Services.CreateAsyncScope())
        {
            var loyalty = scope.ServiceProvider.GetRequiredService<LoyaltyService>();
            await loyalty.SweepMissedCreditsAsync();
        }

        Assert.Single(fake.TopupCalls);
        await using var verifyScope = enabledFactory.Services.CreateAsyncScope();
        var verifyDb = verifyScope.ServiceProvider.GetRequiredService<IApplicationDbContext>();
        var verified = await verifyDb.Orders.FirstAsync(o => o.Id == completed.Id);
        Assert.NotNull(verified.LoyaltyPointsEarned);
        Assert.True(await verifyDb.AuditLogs.AnyAsync(a =>
            a.Action == "LoyaltySweepCreditRecovered" && a.EntityId == completed.Id.ToString()));
    }

    [Fact]
    public async Task RecentlyCompletedOrder_WithinGracePeriod_SweepDoesNotTouchIt()
    {
        var fake = new FakeIikoApiClient();
        using var enabledFactory = factory.WithWebHostBuilder(builder =>
            builder.ConfigureServices(services =>
            {
                services.AddSingleton(EnabledOptions());
                services.AddSingleton<IIikoApiClient>(fake);
            }));

        var client    = enabledFactory.CreateClient();
        var completed = await PlaceAndCompleteOrderAsync(enabledFactory, client, "+77001000922");

        // Real earn already happened during completion; reset it to null to simulate
        // "no credit yet" while CompletedAt stays fresh (just now).
        await using (var scope = enabledFactory.Services.CreateAsyncScope())
        {
            var db    = scope.ServiceProvider.GetRequiredService<IApplicationDbContext>();
            var order = await db.Orders.FirstAsync(o => o.Id == completed.Id);
            order.LoyaltyPointsEarned = null;
            await db.SaveChangesAsync();
        }
        fake.TopupCalls.Clear();

        await using (var scope = enabledFactory.Services.CreateAsyncScope())
        {
            var loyalty = scope.ServiceProvider.GetRequiredService<LoyaltyService>();
            await loyalty.SweepMissedCreditsAsync(); // default 10-min grace period
        }

        Assert.Empty(fake.TopupCalls); // too recent — sweep must not touch it
    }

    [Fact]
    public async Task RevalidateWalletLinksAsync_RepairsStaleWalletIdWithoutALiveBalanceCall()
    {
        var fake = new FakeIikoApiClient();
        const string phone = "+77001000923";
        fake.SetBalance(phone, 42m);

        using var enabledFactory = factory.WithWebHostBuilder(builder =>
            builder.ConfigureServices(services =>
            {
                services.AddSingleton(EnabledOptions());
                services.AddSingleton<IIikoApiClient>(fake);
            }));

        var client = enabledFactory.CreateClient();
        var (_, customerId) = await ApiHelpers.CreateCustomerAsync(client, phone);

        var staleWalletId = Guid.NewGuid();
        await using (var scope = enabledFactory.Services.CreateAsyncScope())
        {
            var db   = scope.ServiceProvider.GetRequiredService<IApplicationDbContext>();
            var user = await db.CustomerUsers.FirstAsync(u => u.Id == customerId);
            user.IikoCustomerId = FakeIikoApiClient.CustomerId;
            user.IikoWalletId = staleWalletId;
            await db.SaveChangesAsync();
        }

        await using (var scope = enabledFactory.Services.CreateAsyncScope())
        {
            var loyalty = scope.ServiceProvider.GetRequiredService<LoyaltyService>();
            await loyalty.RevalidateWalletLinksAsync();
        }

        await using var verifyScope = enabledFactory.Services.CreateAsyncScope();
        var verifyDb = verifyScope.ServiceProvider.GetRequiredService<IApplicationDbContext>();
        var healed   = await verifyDb.CustomerUsers.FirstAsync(u => u.Id == customerId);
        Assert.Equal(FakeIikoApiClient.WalletId, healed.IikoWalletId);
        Assert.NotEqual(staleWalletId, healed.IikoWalletId);
    }

    // ── Transaction history (issue #13) ──────────────────────────────────────

    [Fact]
    public async Task LoyaltyDisabled_TransactionsEndpoint_ReturnsDisabled()
    {
        var client = factory.CreateClient();
        var (token, _) = await ApiHelpers.CreateCustomerAsync(client, "+77001000930");
        ApiHelpers.Authorize(client, token);

        var result = await client.GetFromJsonAsync<LoyaltyHistoryResult>(
            "/api/v1/loyalty/transactions", ApiHelpers.JsonOpts);

        Assert.NotNull(result);
        Assert.False(result.Enabled);
        Assert.Empty(result.Transactions);
    }

    [Fact]
    public async Task LoyaltyEnabled_UnlinkedCustomer_TransactionsEndpoint_ReportsNotLinked()
    {
        var fake = new FakeIikoApiClient();
        using var enabledFactory = factory.WithWebHostBuilder(builder =>
            builder.ConfigureServices(services =>
            {
                services.AddSingleton(EnabledOptions());
                services.AddSingleton<IIikoApiClient>(fake);
            }));

        var client = enabledFactory.CreateClient();
        var (token, _) = await ApiHelpers.CreateCustomerAsync(client, "+77001000931");
        ApiHelpers.Authorize(client, token);

        var result = await client.GetFromJsonAsync<LoyaltyHistoryResult>(
            "/api/v1/loyalty/transactions", ApiHelpers.JsonOpts);

        Assert.NotNull(result);
        Assert.True(result.Enabled);
        Assert.False(result.Linked);
        Assert.Empty(result.Transactions);
    }

    [Fact]
    public async Task LoyaltyEnabled_TransactionsEndpoint_ExcludesAppOrderTransactions_ReturnsOnlyOffsite()
    {
        var fake = new FakeIikoApiClient();
        using var enabledFactory = factory.WithWebHostBuilder(builder =>
            builder.ConfigureServices(services =>
            {
                services.AddSingleton(EnabledOptions());
                services.AddSingleton<IIikoApiClient>(fake);
            }));

        var client = enabledFactory.CreateClient();
        var completed = await PlaceAndCompleteOrderAsync(enabledFactory, client, "+77001000932");

        // iiko's own ledger also carries the app-driven earn (tagged with our order
        // comment) — it must not be duplicated into the offsite history view, since it's
        // already visible via the order itself.
        var appOrderTx = new IikoTransaction(
            Guid.NewGuid(), DateTime.UtcNow.AddHours(-1), 10m, null, null, null,
            "Bonus accrual", false, 0m, 10m, $"Altyncup order {completed.Id}");
        var offsiteTx = new IikoTransaction(
            Guid.NewGuid(), DateTime.UtcNow.AddHours(-2), 25m, 300m, 7, Guid.NewGuid(),
            "Bonus accrual", false, 10m, 35m, null);
        fake.SetTransactions([appOrderTx, offsiteTx]);

        ApiHelpers.Authorize(client, completed.CustomerToken);
        var result = await client.GetFromJsonAsync<LoyaltyHistoryResult>(
            "/api/v1/loyalty/transactions", ApiHelpers.JsonOpts);

        Assert.NotNull(result);
        Assert.True(result.Enabled);
        Assert.True(result.Available);
        Assert.True(result.Linked);
        var tx = Assert.Single(result.Transactions);
        Assert.Equal(7, tx.OrderNumber);
        Assert.Equal(300m, tx.OrderTotal);
        Assert.Equal(300m, tx.KztPaid);
        Assert.Equal(0m, tx.BonusSpent);
        Assert.Equal(25m, tx.BonusEarned);
    }

    [Fact]
    public async Task LoyaltyEnabled_TransactionsEndpoint_GroupsMixedCashAndBonusPurchaseIntoOneEntry()
    {
        var fake = new FakeIikoApiClient();
        using var enabledFactory = factory.WithWebHostBuilder(builder =>
            builder.ConfigureServices(services =>
            {
                services.AddSingleton(EnabledOptions());
                services.AddSingleton<IIikoApiClient>(fake);
            }));

        var client = enabledFactory.CreateClient();
        var (token, _) = await ApiHelpers.CreateCustomerAsync(client, "+77001000934");

        // One POS checkout, split cash+bonus: iiko reports it as two ledger rows sharing
        // the same PosOrderId/OrderSum — a spend leg (bonus redeemed) and an earn leg
        // (bonus credited on the remaining cash-paid amount).
        var posOrderId = Guid.NewGuid();
        var spendLeg = new IikoTransaction(
            Guid.NewGuid(), DateTime.UtcNow.AddMinutes(-30), -200m, 1000m, 42, posOrderId,
            "PayFromWallet", false, 250m, 50m, null);
        var earnLeg = new IikoTransaction(
            Guid.NewGuid(), DateTime.UtcNow.AddMinutes(-29), 40m, 1000m, 42, posOrderId,
            "RefillWalletFromOrder", false, 50m, 90m, null);
        fake.SetTransactions([spendLeg, earnLeg]);

        ApiHelpers.Authorize(client, token);
        await LinkToIikoAsync(client);
        var result = await client.GetFromJsonAsync<LoyaltyHistoryResult>(
            "/api/v1/loyalty/transactions", ApiHelpers.JsonOpts);

        Assert.NotNull(result);
        var tx = Assert.Single(result.Transactions);
        Assert.Equal(42, tx.OrderNumber);
        Assert.Equal(1000m, tx.OrderTotal);
        Assert.Equal(200m, tx.BonusSpent);
        Assert.Equal(40m, tx.BonusEarned);
        Assert.Equal(800m, tx.KztPaid);
    }

    [Fact]
    public async Task LoyaltyEnabled_TransactionsEndpoint_FullBonusRedemption_KztPaidIsZero()
    {
        var fake = new FakeIikoApiClient();
        using var enabledFactory = factory.WithWebHostBuilder(builder =>
            builder.ConfigureServices(services =>
            {
                services.AddSingleton(EnabledOptions());
                services.AddSingleton<IIikoApiClient>(fake);
            }));

        var client = enabledFactory.CreateClient();
        var (token, _) = await ApiHelpers.CreateCustomerAsync(client, "+77001000935");

        var redeemOnly = new IikoTransaction(
            Guid.NewGuid(), DateTime.UtcNow.AddMinutes(-10), -500m, 500m, 43, Guid.NewGuid(),
            "PayFromWallet", false, 500m, 0m, null);
        fake.SetTransactions([redeemOnly]);

        ApiHelpers.Authorize(client, token);
        await LinkToIikoAsync(client);
        var result = await client.GetFromJsonAsync<LoyaltyHistoryResult>(
            "/api/v1/loyalty/transactions", ApiHelpers.JsonOpts);

        Assert.NotNull(result);
        var tx = Assert.Single(result.Transactions);
        Assert.Equal(500m, tx.OrderTotal);
        Assert.Equal(500m, tx.BonusSpent);
        Assert.Equal(0m, tx.BonusEarned);
        Assert.Equal(0m, tx.KztPaid);
    }

    [Fact]
    public async Task LoyaltyEnabled_IikoUnavailable_TransactionsEndpoint_DegradesToUnavailable()
    {
        var fake = new FakeIikoApiClient();
        using var enabledFactory = factory.WithWebHostBuilder(builder =>
            builder.ConfigureServices(services =>
            {
                services.AddSingleton(EnabledOptions());
                services.AddSingleton<IIikoApiClient>(fake);
            }));

        var client = enabledFactory.CreateClient();
        var completed = await PlaceAndCompleteOrderAsync(enabledFactory, client, "+77001000933");

        fake.FailGetTransactions = true;
        ApiHelpers.Authorize(client, completed.CustomerToken);
        var result = await client.GetFromJsonAsync<LoyaltyHistoryResult>(
            "/api/v1/loyalty/transactions", ApiHelpers.JsonOpts);

        Assert.NotNull(result);
        Assert.True(result.Enabled);
        Assert.False(result.Available);
        Assert.True(result.Linked);
        Assert.Empty(result.Transactions);
    }

    // ── Helpers ──────────────────────────────────────────────────────────────

    // A freshly registered customer isn't linked to iiko yet (that happens lazily on the first
    // balance fetch or order), and the history endpoint returns nothing for an unlinked customer.
    private static async Task LinkToIikoAsync(HttpClient client)
    {
        var resp = await client.GetAsync("/api/v1/loyalty/me");
        resp.EnsureSuccessStatusCode();
    }

    private static IikoOptions EnabledOptions() => new()
    {
        Enabled        = true,
        EarnPercent    = 5m,
        OrganizationId = Guid.NewGuid(),
        ProgramId      = Guid.NewGuid(),
    };

    private static async Task<CompletedOrder> PlaceAndCompleteOrderAsync(
        WebApplicationFactory<Program> appFactory, HttpClient client, string phone)
    {
        var (customerToken, _) = await ApiHelpers.CreateCustomerAsync(client, phone);
        ApiHelpers.Authorize(client, customerToken);

        var (itemId, _, _) = await ApiHelpers.GetMenuItemAsync(appFactory.Services, "Cappuccino");
        var resp = await client.PostAsJsonAsync("/api/v1/orders", new
        {
            locationId    = LocationId,
            paymentMethod = "Cash",
            items         = new[] { new { menuItemId = itemId, quantity = 2 } }
        });
        resp.EnsureSuccessStatusCode();
        var order = await resp.Content.ReadFromJsonAsync<ApiHelpers.OrderResult>(ApiHelpers.JsonOpts);

        var adminToken = await ApiHelpers.CreateAdminTokenAsync(appFactory.Services, client);
        ApiHelpers.Authorize(client, adminToken);

        // Orders can only be accepted after payment is confirmed
        (await client.PostAsJsonAsync($"/api/v1/admin/orders/{order!.Id}/payment",
                new { paymentStatus = "Paid", paymentMethod = "Cash" }))
            .EnsureSuccessStatusCode();
        (await client.PostAsJsonAsync($"/api/v1/admin/orders/{order.Id}/accept", new { etaMinutes = 10 }))
            .EnsureSuccessStatusCode();
        foreach (var status in new[] { "Preparing", "Ready", "Completed" })
            (await client.PostAsJsonAsync($"/api/v1/admin/orders/{order.Id}/status", new { status }))
                .EnsureSuccessStatusCode();

        ApiHelpers.ClearAuth(client);
        return new CompletedOrder(order.Id, order.Total, customerToken);
    }

    private sealed class ThrowingIikoApiClient : IIikoApiClient
    {
        public Task<IikoCustomerInfo?> GetCustomerByPhoneAsync(string phone, CancellationToken ct = default)
            => throw new IikoApiException("iiko is down");
        public Task<Guid> CreateOrUpdateCustomerAsync(string phone, string? name, string? surname, CancellationToken ct = default)
            => throw new IikoApiException("iiko is down");
        public Task<Guid> AddCustomerToProgramAsync(Guid iikoCustomerId, CancellationToken ct = default)
            => throw new IikoApiException("iiko is down");
        public Task TopupAsync(Guid iikoCustomerId, Guid walletId, decimal sum, string? comment, CancellationToken ct = default)
            => throw new IikoApiException("iiko is down");
        public Task ChargeoffAsync(Guid iikoCustomerId, Guid walletId, decimal sum, string? comment, CancellationToken ct = default)
            => throw new IikoApiException("iiko is down");
        public Task<Guid> HoldAsync(Guid iikoCustomerId, Guid walletId, decimal sum, string? comment, CancellationToken ct = default)
            => throw new IikoApiException("iiko is down");
        public Task CancelHoldAsync(Guid holdTransactionId, CancellationToken ct = default)
            => throw new IikoApiException("iiko is down");
        public Task<List<IikoNomenclatureProduct>> GetNomenclatureAsync(CancellationToken ct = default)
            => throw new IikoApiException("iiko is down");
        public Task<List<IikoPaymentType>> GetPaymentTypesAsync(CancellationToken ct = default)
            => throw new IikoApiException("iiko is down");
        public Task<List<IikoTerminalGroup>> GetTerminalGroupsAsync(CancellationToken ct = default)
            => throw new IikoApiException("iiko is down");
        public Task<Guid> CreateDeliveryOrderAsync(IikoCreateOrderRequest request, CancellationToken ct = default)
            => throw new IikoApiException("iiko is down");
        public Task CloseDeliveryOrderAsync(Guid iikoOrderId, CancellationToken ct = default)
            => throw new IikoApiException("iiko is down");
        public Task RegisterWebhookAsync(string webhookUrl, string authToken, CancellationToken ct = default)
            => throw new IikoApiException("iiko is down");
        public Task<List<IikoTransaction>> GetCustomerTransactionsAsync(
            Guid iikoCustomerId, DateTime dateFromUtc, DateTime dateToUtc,
            int pageSize = 200, CancellationToken ct = default)
            => throw new IikoApiException("iiko is down");
    }
}
