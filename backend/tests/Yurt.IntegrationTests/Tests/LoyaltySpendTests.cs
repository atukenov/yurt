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

/// <summary>Phase 2 of the iiko loyalty integration: paying with points at checkout.</summary>
[Collection("Integration")]
public class LoyaltySpendTests(YurtWebAppFactory factory)
{
    private static readonly Guid LocationId = Guid.Parse("11111111-0000-0000-0000-000000000001");

    private record OrderWithLoyalty(
        Guid Id, decimal Total, string Status, string PaymentStatus,
        decimal? LoyaltyPointsSpent, decimal? LoyaltyPointsEarned);

    private static IikoOptions EnabledOptions() => new()
    {
        Enabled        = true,
        EarnPercent    = 5m,
        OrganizationId = Guid.NewGuid(),
        ProgramId      = Guid.NewGuid(),
    };

    private WebApplicationFactory<Program> EnabledFactory(IIikoApiClient client) =>
        factory.WithWebHostBuilder(builder =>
            builder.ConfigureServices(services =>
            {
                services.AddSingleton(EnabledOptions());
                services.AddSingleton(client);
            }));

    private static async Task<(OrderWithLoyalty Order, string CustomerToken)> PlaceOrderAsync(
        WebApplicationFactory<Program> appFactory, HttpClient client, string phone, decimal? pointsToSpend,
        string? paymentMethod = "Cash")
    {
        var (customerToken, _) = await ApiHelpers.CreateCustomerAsync(client, phone);
        ApiHelpers.Authorize(client, customerToken);

        var (itemId, _, _) = await ApiHelpers.GetMenuItemAsync(appFactory.Services, "Cappuccino");
        var resp = await client.PostAsJsonAsync("/api/v1/orders", new
        {
            locationId          = LocationId,
            paymentMethod,
            loyaltyPointsToSpend = pointsToSpend,
            items               = new[] { new { menuItemId = itemId, quantity = 2 } }
        });
        resp.EnsureSuccessStatusCode();
        var order = await resp.Content.ReadFromJsonAsync<OrderWithLoyalty>(ApiHelpers.JsonOpts);
        return (order!, customerToken);
    }

    private static async Task AdvanceToCompletedAsync(
        WebApplicationFactory<Program> appFactory, HttpClient client, Guid orderId, bool markPaid = true)
    {
        var adminToken = await ApiHelpers.CreateAdminTokenAsync(appFactory.Services, client);
        ApiHelpers.Authorize(client, adminToken);

        if (markPaid)
            (await client.PostAsJsonAsync($"/api/v1/admin/orders/{orderId}/payment",
                    new { paymentStatus = "Paid", paymentMethod = "Cash" }))
                .EnsureSuccessStatusCode();

        (await client.PostAsJsonAsync($"/api/v1/admin/orders/{orderId}/accept", new { etaMinutes = 10 }))
            .EnsureSuccessStatusCode();
        foreach (var status in new[] { "Preparing", "Ready", "Completed" })
            (await client.PostAsJsonAsync($"/api/v1/admin/orders/{orderId}/status", new { status }))
                .EnsureSuccessStatusCode();
    }

    // ── Placement ────────────────────────────────────────────────────────────

    [Fact]
    public async Task PartialSpend_HoldsRequestedPoints()
    {
        var fake = new FakeIikoApiClient();
        fake.SetBalance("+77001000901", 100m);
        using var f = EnabledFactory(fake);
        var client = f.CreateClient();

        var (order, _) = await PlaceOrderAsync(f, client, "+77001000901", 5m);

        Assert.Equal(5m, order.LoyaltyPointsSpent);
        Assert.Equal("Unpaid", order.PaymentStatus); // partial — remainder still due
        var hold = Assert.Single(fake.HoldCalls);
        Assert.Equal(5m, hold.Sum);
        Assert.Contains(order.Id.ToString(), hold.Comment);
        Assert.Equal(1, fake.ActiveHoldCount);
    }

    [Fact]
    public async Task SpendMoreThanBalance_ClampsToAvailableBalance()
    {
        var fake = new FakeIikoApiClient();
        fake.SetBalance("+77001000902", 2m);
        using var f = EnabledFactory(fake);
        var client = f.CreateClient();

        var (order, _) = await PlaceOrderAsync(f, client, "+77001000902", 999m);

        Assert.Equal(2m, order.LoyaltyPointsSpent);
        Assert.Equal(2m, Assert.Single(fake.HoldCalls).Sum);
    }

    [Fact]
    public async Task FullCover_MarksOrderPaid()
    {
        var fake = new FakeIikoApiClient();
        fake.SetBalance("+77001000903", 1000m);
        using var f = EnabledFactory(fake);
        var client = f.CreateClient();

        var (order, _) = await PlaceOrderAsync(f, client, "+77001000903", 1000m);

        Assert.Equal(order.Total, order.LoyaltyPointsSpent);
        Assert.Equal("Paid", order.PaymentStatus);

        // Fully points-paid order can be accepted without a manual payment step
        await AdvanceToCompletedAsync(f, client, order.Id, markPaid: false);

        Assert.Equal(order.Total, Assert.Single(fake.ChargeoffCalls).Sum);
        Assert.Empty(fake.TopupCalls); // earn base is zero — no points on points
    }

    [Fact]
    public async Task FullCoverWithNoPaymentMethod_PlacesOrderWithoutRequiringABank()
    {
        var fake = new FakeIikoApiClient();
        fake.SetBalance("+77001000910", 1000m);
        using var f = EnabledFactory(fake);
        var client = f.CreateClient();

        // paymentMethod omitted entirely — matches the customer app when bonuses fully
        // cover the total and the bank-selection step is skipped
        var (order, _) = await PlaceOrderAsync(f, client, "+77001000910", 1000m, paymentMethod: null);

        Assert.Equal(order.Total, order.LoyaltyPointsSpent);
        Assert.Equal("Paid", order.PaymentStatus);
    }

    [Fact]
    public async Task NoPaymentMethodAndInsufficientPoints_Returns400()
    {
        var fake = new FakeIikoApiClient();
        fake.SetBalance("+77001000911", 1m); // far short of the order total
        using var f = EnabledFactory(fake);
        var client = f.CreateClient();

        var (customerToken, _) = await ApiHelpers.CreateCustomerAsync(client, "+77001000911");
        ApiHelpers.Authorize(client, customerToken);
        var (itemId, _, _) = await ApiHelpers.GetMenuItemAsync(f.Services, "Cappuccino");

        var resp = await client.PostAsJsonAsync("/api/v1/orders", new
        {
            locationId          = LocationId,
            paymentMethod       = (string?)null,
            loyaltyPointsToSpend = 1m,
            items               = new[] { new { menuItemId = itemId, quantity = 2 } }
        });

        Assert.Equal(System.Net.HttpStatusCode.BadRequest, resp.StatusCode);
    }

    [Fact]
    public async Task NoPaymentMethod_BalanceDropsBetweenClientCheckAndHold_FallsBackToCash()
    {
        // Client believed 1000 points were available and asked to spend all of it with no
        // bank selected; by the time the hold runs the real balance is much lower — the
        // order must not be left without a settled payment method for the remainder.
        var fake = new FakeIikoApiClient();
        fake.SetBalance("+77001000912", 1m);
        using var f = EnabledFactory(fake);
        var client = f.CreateClient();

        var (customerToken, _) = await ApiHelpers.CreateCustomerAsync(client, "+77001000912");
        ApiHelpers.Authorize(client, customerToken);
        var (itemId, _, _) = await ApiHelpers.GetMenuItemAsync(f.Services, "Cappuccino");

        var resp = await client.PostAsJsonAsync("/api/v1/orders", new
        {
            locationId          = LocationId,
            paymentMethod       = (string?)null,
            loyaltyPointsToSpend = 1000m, // client's stale belief, exceeds real balance
            items               = new[] { new { menuItemId = itemId, quantity = 2 } }
        });
        resp.EnsureSuccessStatusCode(); // guard passed (declared amount covers total); order proceeds

        var order = await resp.Content.ReadFromJsonAsync<OrderWithLoyalty>(ApiHelpers.JsonOpts);
        Assert.Equal(1m, order!.LoyaltyPointsSpent); // clamped to the real balance
        Assert.NotEqual(order.Total, order.LoyaltyPointsSpent);

        await using var scope = f.Services.CreateAsyncScope();
        var db = scope.ServiceProvider.GetRequiredService<IApplicationDbContext>();
        var dbOrder = await db.Orders.FirstAsync(o => o.Id == order.Id);
        Assert.Equal(Yurt.Domain.Enums.PaymentMethod.Cash, dbOrder.PaymentMethod);
    }

    [Fact]
    public async Task IikoDownAtPlacement_OrderStillCreatedWithoutPoints()
    {
        using var f = factory.WithWebHostBuilder(builder =>
            builder.ConfigureServices(services =>
            {
                services.AddSingleton(EnabledOptions());
                services.AddSingleton<IIikoApiClient>(new AlwaysThrowIikoClient());
            }));
        var client = f.CreateClient();

        var (order, _) = await PlaceOrderAsync(f, client, "+77001000904", 50m);

        Assert.Null(order.LoyaltyPointsSpent);
        Assert.Equal("Unpaid", order.PaymentStatus);
    }

    // ── Completion ───────────────────────────────────────────────────────────

    [Fact]
    public async Task Completion_ReleasesHoldChargesOffAndEarnsOnMoneyPortionOnly()
    {
        var fake = new FakeIikoApiClient();
        fake.SetBalance("+77001000905", 100m);
        using var f = EnabledFactory(fake);
        var client = f.CreateClient();

        var (order, _) = await PlaceOrderAsync(f, client, "+77001000905", 5m);
        await AdvanceToCompletedAsync(f, client, order.Id);

        Assert.Single(fake.CancelHoldCalls);
        Assert.Equal(0, fake.ActiveHoldCount);
        Assert.Equal(5m, Assert.Single(fake.ChargeoffCalls).Sum);

        // Earn 5% of the money portion (total − points), truncated to 2 dp
        var expectedEarn = Math.Round((order.Total - 5m) * 0.05m, 2, MidpointRounding.ToZero);
        Assert.Equal(expectedEarn, Assert.Single(fake.TopupCalls).Sum);

        await using var scope = f.Services.CreateAsyncScope();
        var db = scope.ServiceProvider.GetRequiredService<IApplicationDbContext>();
        var dbOrder = await db.Orders.FirstAsync(o => o.Id == order.Id);
        Assert.Equal(5m, dbOrder.LoyaltyPointsSpent);
        Assert.Equal(expectedEarn, dbOrder.LoyaltyPointsEarned);
        Assert.Null(dbOrder.LoyaltyHoldTransactionId);
        Assert.Equal(LoyaltyPendingAction.None, dbOrder.LoyaltyPendingAction);
    }

    // ── Decline ──────────────────────────────────────────────────────────────

    [Fact]
    public async Task Decline_ReleasesHoldAndClearsSpentPoints()
    {
        var fake = new FakeIikoApiClient();
        fake.SetBalance("+77001000906", 100m);
        using var f = EnabledFactory(fake);
        var client = f.CreateClient();

        var (order, _) = await PlaceOrderAsync(f, client, "+77001000906", 5m);

        var adminToken = await ApiHelpers.CreateAdminTokenAsync(f.Services, client);
        ApiHelpers.Authorize(client, adminToken);
        (await client.PostAsJsonAsync($"/api/v1/admin/orders/{order.Id}/decline", new { reason = "Out of milk" }))
            .EnsureSuccessStatusCode();

        Assert.Single(fake.CancelHoldCalls);
        Assert.Equal(0, fake.ActiveHoldCount);
        Assert.Empty(fake.ChargeoffCalls);

        await using var scope = f.Services.CreateAsyncScope();
        var db = scope.ServiceProvider.GetRequiredService<IApplicationDbContext>();
        var dbOrder = await db.Orders.FirstAsync(o => o.Id == order.Id);
        Assert.Null(dbOrder.LoyaltyPointsSpent);
        Assert.Null(dbOrder.LoyaltyHoldTransactionId);
    }

    [Fact]
    public async Task DeclineWhileIikoDown_MarksPendingRelease_RetrySweepReleasesHold()
    {
        var fake = new FakeIikoApiClient();
        fake.SetBalance("+77001000907", 100m);
        using var f = EnabledFactory(fake);
        var client = f.CreateClient();

        var (order, _) = await PlaceOrderAsync(f, client, "+77001000907", 5m);

        // iiko goes down before the decline
        fake.FailCancelHold = true;
        var adminToken = await ApiHelpers.CreateAdminTokenAsync(f.Services, client);
        ApiHelpers.Authorize(client, adminToken);
        (await client.PostAsJsonAsync($"/api/v1/admin/orders/{order.Id}/decline", new { reason = "Closed" }))
            .EnsureSuccessStatusCode(); // decline itself must not fail

        await using (var scope = f.Services.CreateAsyncScope())
        {
            var db = scope.ServiceProvider.GetRequiredService<IApplicationDbContext>();
            var dbOrder = await db.Orders.FirstAsync(o => o.Id == order.Id);
            Assert.Equal(LoyaltyPendingAction.Release, dbOrder.LoyaltyPendingAction);
            Assert.NotNull(dbOrder.LoyaltyHoldTransactionId); // hold still active in iiko
        }

        // iiko comes back — the background sweep releases the hold
        fake.FailCancelHold = false;
        await using (var scope = f.Services.CreateAsyncScope())
        {
            var loyalty = scope.ServiceProvider.GetRequiredService<LoyaltyService>();
            await loyalty.RetryPendingAsync();
        }

        Assert.Equal(0, fake.ActiveHoldCount);
        await using (var scope = f.Services.CreateAsyncScope())
        {
            var db = scope.ServiceProvider.GetRequiredService<IApplicationDbContext>();
            var dbOrder = await db.Orders.FirstAsync(o => o.Id == order.Id);
            Assert.Equal(LoyaltyPendingAction.None, dbOrder.LoyaltyPendingAction);
            Assert.Null(dbOrder.LoyaltyHoldTransactionId);
        }
    }

    [Fact]
    public async Task ChargeoffFailsAtCompletion_RetrySweepChargesOff()
    {
        var fake = new FakeIikoApiClient();
        fake.SetBalance("+77001000908", 100m);
        using var f = EnabledFactory(fake);
        var client = f.CreateClient();

        var (order, _) = await PlaceOrderAsync(f, client, "+77001000908", 5m);

        fake.FailChargeoff = true;
        await AdvanceToCompletedAsync(f, client, order.Id); // completion must not fail

        await using (var scope = f.Services.CreateAsyncScope())
        {
            var db = scope.ServiceProvider.GetRequiredService<IApplicationDbContext>();
            var dbOrder = await db.Orders.FirstAsync(o => o.Id == order.Id);
            Assert.Equal(LoyaltyPendingAction.ChargeOff, dbOrder.LoyaltyPendingAction);
        }

        fake.FailChargeoff = false;
        await using (var scope = f.Services.CreateAsyncScope())
        {
            var loyalty = scope.ServiceProvider.GetRequiredService<LoyaltyService>();
            await loyalty.RetryPendingAsync();
        }

        Assert.Equal(5m, Assert.Single(fake.ChargeoffCalls).Sum);
        await using (var scopeAfter = f.Services.CreateAsyncScope())
        {
            var db = scopeAfter.ServiceProvider.GetRequiredService<IApplicationDbContext>();
            var dbOrder = await db.Orders.FirstAsync(o => o.Id == order.Id);
            Assert.Equal(LoyaltyPendingAction.None, dbOrder.LoyaltyPendingAction);
        }
    }

    private sealed class AlwaysThrowIikoClient : IIikoApiClient
    {
        public Task<IikoCustomerInfo?> GetCustomerByPhoneAsync(string phone, CancellationToken ct = default)
            => throw new IikoApiException("down");
        public Task<Guid> CreateOrUpdateCustomerAsync(string phone, string? name, string? surname, CancellationToken ct = default)
            => throw new IikoApiException("down");
        public Task<Guid> AddCustomerToProgramAsync(Guid iikoCustomerId, CancellationToken ct = default)
            => throw new IikoApiException("down");
        public Task TopupAsync(Guid iikoCustomerId, Guid walletId, decimal sum, string? comment, CancellationToken ct = default)
            => throw new IikoApiException("down");
        public Task ChargeoffAsync(Guid iikoCustomerId, Guid walletId, decimal sum, string? comment, CancellationToken ct = default)
            => throw new IikoApiException("down");
        public Task<Guid> HoldAsync(Guid iikoCustomerId, Guid walletId, decimal sum, string? comment, CancellationToken ct = default)
            => throw new IikoApiException("down");
        public Task CancelHoldAsync(Guid holdTransactionId, CancellationToken ct = default)
            => throw new IikoApiException("down");
        public Task<List<IikoNomenclatureProduct>> GetNomenclatureAsync(CancellationToken ct = default)
            => throw new IikoApiException("down");
        public Task<List<IikoPaymentType>> GetPaymentTypesAsync(CancellationToken ct = default)
            => throw new IikoApiException("down");
        public Task<List<IikoTerminalGroup>> GetTerminalGroupsAsync(CancellationToken ct = default)
            => throw new IikoApiException("down");
        public Task<Guid> CreateDeliveryOrderAsync(IikoCreateOrderRequest request, CancellationToken ct = default)
            => throw new IikoApiException("down");
        public Task CloseDeliveryOrderAsync(Guid iikoOrderId, CancellationToken ct = default)
            => throw new IikoApiException("down");
        public Task RegisterWebhookAsync(string webhookUrl, string authToken, CancellationToken ct = default)
            => throw new IikoApiException("down");
        public Task<List<IikoTransaction>> GetCustomerTransactionsAsync(
            Guid iikoCustomerId, DateTime dateFromUtc, DateTime dateToUtc,
            int pageSize = 200, CancellationToken ct = default)
            => throw new IikoApiException("down");
    }
}
