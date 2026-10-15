namespace Yurt.Application.Features.Analytics.DTOs;

public record AnalyticsResponse(
    KpiSummary Kpis,
    KpiTrends Trends,
    List<RevenueDataPoint> RevenueOverTime,
    List<TopItemDto> TopItems,
    List<LocationPerformanceDto> LocationPerformance,
    List<HourlyDistributionDto> HourlyDistribution,
    List<PaymentBreakdownDto> PaymentBreakdown,
    List<OrderStatusBreakdownDto> StatusBreakdown);

public record KpiSummary(
    decimal TotalRevenue,
    int TotalOrders,
    decimal AvgOrderValue,
    int CompletedOrders,
    int DeclinedOrders,
    double AvgPrepTimeMinutes,
    int UniqueCustomers,
    int NewCustomers,
    int ReturningCustomers,
    double DeclineRatePercent,
    decimal DeclinedRevenue);

// Percent change vs. the immediately preceding period of equal length. Null when there's
// no meaningful baseline to compare against (e.g. the "all" period, or a previous value of 0).
public record KpiTrends(
    double? RevenueChangePercent,
    double? OrdersChangePercent,
    double? AvgOrderValueChangePercent,
    double? AvgPrepTimeChangePercent);

public record RevenueDataPoint(string Label, decimal Revenue, int Orders);

public record TopItemDto(string Name, int QuantitySold, decimal Revenue);

public record LocationPerformanceDto(string LocationName, decimal Revenue, int Orders);

public record HourlyDistributionDto(int Hour, int Orders);

public record PaymentBreakdownDto(string Method, int Count, decimal Total);

public record OrderStatusBreakdownDto(string Status, int Count);

public record DashboardDto(
    int OrdersToday,
    decimal RevenueToday,
    decimal AvgOrderValue,
    int PendingCount,
    List<HourlyOrderCount> HourlyOrders);

public record HourlyOrderCount(int Hour, int Count);
