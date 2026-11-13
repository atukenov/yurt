using Yurt.Domain.Enums;

namespace Yurt.Application.Features.Orders.DTOs;

public record CreateOrderDto(Guid LocationId, List<OrderItemInputDto> Items, PaymentMethod? PaymentMethod = null, string? DiscountCode = null, string? IdempotencyKey = null, decimal? LoyaltyPointsToSpend = null, DateTime? PickupAt = null, string? TableCode = null, OrderType? OrderType = null);
public record OrderItemInputDto(Guid MenuItemId, int Quantity, List<OrderItemToppingInputDto>? Toppings = null, string? Notes = null, Guid? VariantId = null);
public record OrderItemToppingInputDto(Guid ToppingId, string ToppingName, decimal Price);

public record AcceptOrderDto(int EtaMinutes);
public record DeclineOrderDto(string Reason, string? Pin = null);

/// <summary>
/// Money taken off a bill by hand, with a manager's PIN and the reason. Give either an Amount or a Percent (1 to 100).
/// </summary>
public record DiscountInputDto(decimal? Amount = null, decimal? Percent = null, string? Pin = null, string? Reason = null);
/// <summary>Change or remove the tip of an order (Amount 0 removes it). After the sale this needs a manager PIN and a reason.</summary>
public record SetTipDto(decimal Amount, PaymentMethod? Method = null, string? Pin = null, string? Reason = null, Guid? StaffId = null);
public record UpdateOrderStatusDto(OrderStatus Status);
public record UpdatePaymentDto(PaymentStatus PaymentStatus, string? Pin = null, string? Reason = null);
public record RateOrderDto(int Rating, string? Comment = null);

public record OrderDto(
    Guid Id,
    Guid? CustomerUserId,
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
    string? LocationTwoGisUrl = null,
    List<Guid>? ReadyStationIds = null,
    // Links to the official tax receipts once the cash register service sent them (own_pos); null otherwise.
    string? FiscalReceiptUrl = null,
    string? FiscalReturnUrl = null,
    // When the guest wants to pick the order up (UTC); null = as soon as possible.
    DateTime? PickupAt = null,
    // The table the guest ordered to by its QR code (table_ordering); null = pick up at the counter.
    string? TableLabel = null,
    // Where the order came from: "App" (the guest's app) or "Counter" (rung up by a cashier for a walk-in guest).
    OrderSource Source = OrderSource.App,
    // Here or to go.
    OrderType OrderType = OrderType.TakeAway,
    // A tip on top of the bill (own_pos). Not part of Total. TipMethod null = paid the same way as the bill.
    decimal TipAmount = 0,
    PaymentMethod? TipMethod = null);

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

/// <summary>
/// A sale rung up at the counter (Yurt POS). <c>IdempotencyKey</c> is made by the till for each sale and sent again on
/// every retry. <c>SoldAt</c> is when the sale really happened (a sale made while the till was offline); empty = now.
/// <c>HandedOver</c> = the guest already has everything (nothing for the kitchen). <c>EtaMinutes</c> = when it should be
/// ready, for the kitchen screen.
/// </summary>
public record CreateCounterSaleDto(
    Guid LocationId,
    List<CounterSaleItemDto> Items,
    PaymentMethod PaymentMethod,
    string IdempotencyKey,
    DateTime? SoldAt = null,
    bool HandedOver = false,
    int? EtaMinutes = null,
    OrderType OrderType = OrderType.TakeAway,
    DiscountInputDto? Discount = null,
    decimal? Tip = null,
    PaymentMethod? TipMethod = null);

public record CounterSaleItemDto(Guid MenuItemId, int Quantity, Guid? VariantId = null, List<Guid>? ToppingIds = null, string? Notes = null);
