using MockQueryable.NSubstitute;
using NSubstitute;
using Yurt.Application.Common.Interfaces;
using Yurt.Application.Features.Analytics.Services;
using Yurt.Domain.Entities;
using Yurt.Domain.Enums;

namespace Yurt.UnitTests;

/// <summary>
/// Tests for <see cref="AnalyticsService.GetAnalyticsAsync"/> using a mocked DB.
/// </summary>
public class AnalyticsServiceTests
{
    private static AnalyticsService Build(params Order[] orders)
    {
        var db = Substitute.For<IApplicationDbContext>();
        var mockSet = orders.ToList().BuildMockDbSet();
        db.Orders.Returns(mockSet);
        return new AnalyticsService(db);
    }

    private static Order MakeOrder(
        DateTime createdAt,
        OrderStatus status = OrderStatus.Completed,
        decimal total = 100m,
        Guid? customerId = null,
        PaymentMethod? paymentMethod = PaymentMethod.Cash,
        DateTime? acceptedAt = null,
        DateTime? completedAt = null)
        => new()
        {
            CreatedAt = createdAt,
            Status = status,
            Total = total,
            CustomerUserId = customerId ?? Guid.NewGuid(),
            LocationId = Guid.NewGuid(),
            PaymentMethod = paymentMethod,
            AcceptedAt = acceptedAt,
            CompletedAt = completedAt,
        };

    [Fact]
    public async Task GetAnalyticsAsync_TodayPeriod_BucketsRevenueByHourNotMonth()
    {
        var midnight = DateTime.UtcNow.Date;
        var nineAm = MakeOrder(midnight.AddHours(9), total: 1000m);
        var tenAm = MakeOrder(midnight.AddHours(10), total: 2000m);
        var svc = Build(nineAm, tenAm);

        var result = await svc.GetAnalyticsAsync("today");

        Assert.Equal(2, result.RevenueOverTime.Count);
        Assert.Equal("9 AM", result.RevenueOverTime[0].Label);
        Assert.Equal(1000m, result.RevenueOverTime[0].Revenue);
        Assert.Equal("10 AM", result.RevenueOverTime[1].Label);
        Assert.Equal(2000m, result.RevenueOverTime[1].Revenue);
    }

    [Fact]
    public async Task GetAnalyticsAsync_LoyaltyPointsOrder_LabeledCorrectlyNotUnknown()
    {
        var order = MakeOrder(DateTime.UtcNow, total: 500m, paymentMethod: null);
        var svc = Build(order);

        var result = await svc.GetAnalyticsAsync("month");

        var entry = Assert.Single(result.PaymentBreakdown);
        Assert.Equal("Loyalty Points", entry.Method);
        Assert.Equal(1, entry.Count);
        Assert.Equal(500m, entry.Total);
    }

    [Fact]
    public async Task GetAnalyticsAsync_DeclinedOrders_ComputesDeclineRateAndLostRevenue()
    {
        var completed = MakeOrder(DateTime.UtcNow, total: 1000m);
        var declined1 = MakeOrder(DateTime.UtcNow, status: OrderStatus.Declined, total: 300m);
        var declined2 = MakeOrder(DateTime.UtcNow, status: OrderStatus.Declined, total: 200m);
        var svc = Build(completed, declined1, declined2);

        var result = await svc.GetAnalyticsAsync("month");

        Assert.Equal(2, result.Kpis.DeclinedOrders);
        Assert.Equal(500m, result.Kpis.DeclinedRevenue);
        Assert.Equal(66.7, result.Kpis.DeclineRatePercent);
    }

    [Fact]
    public async Task GetAnalyticsAsync_NewVsReturningCustomers_ClassifiesCorrectly()
    {
        var customerA = Guid.NewGuid();
        var customerB = Guid.NewGuid();
        var priorOrderA = MakeOrder(DateTime.UtcNow.AddDays(-30), customerId: customerA);
        var recentOrderA = MakeOrder(DateTime.UtcNow.AddDays(-1), customerId: customerA);
        var recentOrderB = MakeOrder(DateTime.UtcNow.AddDays(-2), customerId: customerB);
        var svc = Build(priorOrderA, recentOrderA, recentOrderB);

        var result = await svc.GetAnalyticsAsync("week");

        Assert.Equal(2, result.Kpis.UniqueCustomers);
        Assert.Equal(1, result.Kpis.ReturningCustomers);
        Assert.Equal(1, result.Kpis.NewCustomers);
    }

    [Fact]
    public async Task GetAnalyticsAsync_RevenueDoubledVsPreviousWeek_ComputesPositiveTrend()
    {
        var previousWeekOrder = MakeOrder(DateTime.UtcNow.AddDays(-10), total: 500m);
        var thisWeekOrder = MakeOrder(DateTime.UtcNow.AddDays(-1), total: 1000m);
        var svc = Build(previousWeekOrder, thisWeekOrder);

        var result = await svc.GetAnalyticsAsync("week");

        Assert.Equal(100.0, result.Trends.RevenueChangePercent);
    }

    [Fact]
    public async Task GetAnalyticsAsync_AllPeriod_TrendsAreNull()
    {
        var order = MakeOrder(DateTime.UtcNow, total: 100m);
        var svc = Build(order);

        var result = await svc.GetAnalyticsAsync("all");

        Assert.Null(result.Trends.RevenueChangePercent);
        Assert.Null(result.Trends.OrdersChangePercent);
        Assert.Null(result.Trends.AvgOrderValueChangePercent);
        Assert.Null(result.Trends.AvgPrepTimeChangePercent);
    }

    [Fact]
    public async Task GetAnalyticsAsync_NoPreviousPeriodActivity_RevenueTrendIsNullNotInfinite()
    {
        // Only this week has any activity — the previous week window is completely empty.
        var thisWeekOrder = MakeOrder(DateTime.UtcNow.AddDays(-1), total: 1000m);
        var svc = Build(thisWeekOrder);

        var result = await svc.GetAnalyticsAsync("week");

        Assert.Null(result.Trends.RevenueChangePercent);
    }
}
