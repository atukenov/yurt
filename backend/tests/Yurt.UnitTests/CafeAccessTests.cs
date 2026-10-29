using Yurt.Application.Features.Tenants;
using Yurt.Domain.Entities;

namespace Yurt.UnitTests;

public class CafeAccessTests
{
    private static readonly DateTime Now = new(2026, 10, 28, 12, 0, 0, DateTimeKind.Utc);

    [Fact]
    public void NotTracked_IsFull()
    {
        var s = CafeAccess.Evaluate(new Tenant(), Now, 7);
        Assert.Equal(CafeAccessState.Full, s.State);
        Assert.Null(s.LimitedFrom);
    }

    [Fact]
    public void UnknownCafe_FailsOpen()
        => Assert.Equal(CafeAccessState.Full, CafeAccess.Evaluate(null, Now, 7).State);

    [Fact]
    public void PaidInFuture_IsFull()
        => Assert.Equal(CafeAccessState.Full, CafeAccess.Evaluate(new Tenant { PaidUntil = Now.AddDays(1) }, Now, 7).State);

    [Fact]
    public void InsideGraceDays_StillTakesOrders()
    {
        var s = CafeAccess.Evaluate(new Tenant { PaidUntil = Now.AddDays(-6) }, Now, 7);
        Assert.Equal(CafeAccessState.Grace, s.State);
        Assert.False(s.IsLimited);
        Assert.Equal(Now.AddDays(1), s.LimitedFrom);
    }

    [Fact]
    public void AfterGraceDays_IsLimited()
        => Assert.True(CafeAccess.Evaluate(new Tenant { PaidUntil = Now.AddDays(-7) }, Now, 7).IsLimited);

    [Fact]
    public void ZeroGraceDays_LimitsRightAtPaidUntil()
        => Assert.True(CafeAccess.Evaluate(new Tenant { PaidUntil = Now }, Now, 0).IsLimited);

    [Fact]
    public void OperatorSwitch_LimitsEvenWhenPaid()
    {
        var s = CafeAccess.Evaluate(new Tenant { PaidUntil = Now.AddYears(1), LimitedByOperator = true }, Now, 7);
        Assert.True(s.IsLimited);
        Assert.True(s.ByOperator);
    }
}
