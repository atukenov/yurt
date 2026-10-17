using Microsoft.Extensions.Logging;
using NSubstitute;
using Yurt.Application.Common.Interfaces;
using Yurt.Application.Features.Loyalty;
using Yurt.Application.Features.Loyalty.Services;
using Yurt.Domain;
using Yurt.Domain.Entities;

namespace Yurt.UnitTests;

public class LoyaltyModuleGateTests
{
    private static (LoyaltyService svc, IIikoApiClient iiko, IModuleGate gate) Build(bool moduleOn, Guid? tenantId = null)
    {
        var tenant = Substitute.For<ITenantContext>();
        tenant.TenantId.Returns(tenantId ?? Yurt.Domain.Entities.Tenant.DefaultId);
        var db = Substitute.For<IApplicationDbContext>();
        var iiko = Substitute.For<IIikoApiClient>();
        var gate = Substitute.For<IModuleGate>();
        gate.IsEnabledAsync(ModuleKeys.Loyalty, Arg.Any<CancellationToken>()).Returns(moduleOn);
        var svc = new LoyaltyService(db, iiko, new IikoOptions { Enabled = true, EarnPercent = 5m },
            Substitute.For<IAuditLogService>(), Substitute.For<ILogger<LoyaltyService>>(), gate, tenant);
        return (svc, iiko, gate);
    }

    [Fact]
    public async Task Balance_ModuleOff_ReportsDisabled_AndNeverCallsIiko()
    {
        var (svc, iiko, _) = Build(moduleOn: false);

        var balance = await svc.GetBalanceAsync(Guid.NewGuid());

        Assert.False(balance.Enabled);
        Assert.Null(balance.Balance);
        Assert.Empty(iiko.ReceivedCalls());
    }

    [Fact]
    public async Task History_ModuleOff_ReportsDisabled_AndNeverCallsIiko()
    {
        var (svc, iiko, _) = Build(moduleOn: false);

        var history = await svc.GetTransactionHistoryAsync(Guid.NewGuid());

        Assert.False(history.Enabled);
        Assert.Empty(iiko.ReceivedCalls());
    }

    [Fact]
    public async Task Hold_ModuleOff_AppliesNothing()
    {
        var (svc, iiko, _) = Build(moduleOn: false);

        var applied = await svc.TryHoldForOrderAsync(new Order { Total = 10m }, 5m);

        Assert.Equal(0m, applied);
        Assert.Empty(iiko.ReceivedCalls());
    }

    [Fact]
    public async Task Credit_ModuleOff_EarnsNothing()
    {
        var (svc, iiko, _) = Build(moduleOn: false);
        var order = new Order { Total = 10m };

        await svc.CreditForOrderAsync(order);

        Assert.Null(order.LoyaltyPointsEarned);
        Assert.Empty(iiko.ReceivedCalls());
    }

    [Fact]
    public async Task Balance_AnotherCafe_NeverCallsIiko_EvenWithModuleOn()
    {
        var (svc, iiko, _) = Build(moduleOn: true, tenantId: Guid.NewGuid());

        var balance = await svc.GetBalanceAsync(Guid.NewGuid());

        Assert.False(balance.Available);
        Assert.Empty(iiko.ReceivedCalls());
    }
}
