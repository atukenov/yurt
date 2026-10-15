using Yurt.Domain.Enums;

namespace Yurt.Application.Features.Orders.DTOs;

public record CreateOrderDto(Guid LocationId, List<OrderItemInputDto> Items, PaymentMethod? PaymentMethod = null, string? DiscountCode = null, string? IdempotencyKey = null, decimal? LoyaltyPointsToSpend = null);
public record OrderItemInputDto(Guid MenuItemId, int Quantity, List<OrderItemToppingInputDto>? Toppings = null, string? Notes = null, Guid? VariantId = null);
public record OrderItemToppingInputDto(Guid ToppingId, string ToppingName, decimal Price);

public record AcceptOrderDto(int EtaMinutes);
public record DeclineOrderDto(string Reason);
public record UpdateOrderStatusDto(OrderStatus Status);
public record UpdatePaymentDto(PaymentStatus PaymentStatus);
public record RateOrderDto(int Rating, string? Comment = null);

public record OrderDto(
    Guid Id,
    Guid CustomerUserId,
    string CustomerName,
    string CustomerPhone,
    Guid LocationId,
    string LocationName,
    OrderStatus Status,
    string? DeclineReason,
    int? EtaMinutes,
    DateTime CreatedAt,
    DateTime? AcceptedAt,
    DateTime? CompletedAt,
    PaymentStatus PaymentStatus,
    PaymentMethod? PaymentMethod,
    decimal Subtotal,
    decimal DiscountAmount,
    decimal Total,
    string? DiscountCode,
    List<OrderItemDto> Items,
    decimal? LoyaltyPointsSpent = null,
    decimal? LoyaltyPointsEarned = null,
    int? Rating = null,
    string? RatingComment = null,
    IikoOrderSyncStatus? IikoOrderSyncStatus = null,
    string? LocationTwoGisUrl = null);

public record OrderItemDto(
    Guid Id,
    Guid MenuItemId,
    string MenuItemName,
    int Quantity,
    decimal UnitPrice,
    decimal LineTotal,
    List<OrderItemToppingDto> Toppings,
    string? Notes = null,
    string? VariantLabel = null);

public record OrderItemToppingDto(Guid ToppingId, string ToppingName, decimal Price);
