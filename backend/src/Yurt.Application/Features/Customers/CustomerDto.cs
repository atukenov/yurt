using Yurt.Domain.Enums;

namespace Yurt.Application.Features.Customers;

public record SetActiveDto(bool IsActive);

public record CustomerSummaryDto(
    Guid Id,
    string Phone,
    string DisplayName,
    DateTime RegisteredAt,
    int OrderCount,
    decimal TotalSpent);

public record CustomerDetailDto(
    Guid Id,
    string Phone,
    string? DisplayName,
    DateTime RegisteredAt,
    bool IsActive,
    int TotalOrders,
    decimal TotalSpent,
    List<CustomerOrderSummaryDto> RecentOrders);

public record CustomerOrderSummaryDto(
    Guid Id,
    DateTime CreatedAt,
    OrderStatus Status,
    decimal Total,
    string LocationName,
    int ItemCount,
    decimal? LoyaltyPointsEarned = null,
    decimal? LoyaltyPointsSpent = null);
