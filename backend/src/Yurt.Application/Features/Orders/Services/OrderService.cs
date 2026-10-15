using Microsoft.EntityFrameworkCore;
using Yurt.Application.Common.Interfaces;
using Yurt.Application.Common.Models;
using Yurt.Application.Features.DiscountCodes.Services;
using Yurt.Application.Features.IikoIntegration.Services;
using Yurt.Application.Features.Loyalty.Services;
using Yurt.Application.Features.Orders.DTOs;
using Yurt.Domain.Entities;
using Yurt.Domain.Enums;

namespace Yurt.Application.Features.Orders.Services;

public class OrderService
{
    private readonly IApplicationDbContext _db;
    private readonly IOrdersHubService _hub;
    private readonly IAuditLogService _audit;
    private readonly DiscountCodeService _discountCodes;
    private readonly LoyaltyService _loyalty;
    private readonly IikoOrderSyncService _iikoSync;

    public OrderService(
        IApplicationDbContext db, IOrdersHubService hub, IAuditLogService audit,
        DiscountCodeService discountCodes, LoyaltyService loyalty, IikoOrderSyncService iikoSync)
    {
        _db = db;
        _hub = hub;
        _audit = audit;
        _discountCodes = discountCodes;
        _loyalty = loyalty;
        _iikoSync = iikoSync;
    }

    public const string CustomerCancelReason = "Cancelled by customer";

    public async Task<Result<OrderDto>> CreateOrderAsync(
        Guid customerId, CreateOrderDto dto, CancellationToken ct = default)
    {
        // Idempotency: if this customer sent the same key within 24 h, return the original order
        if (!string.IsNullOrWhiteSpace(dto.IdempotencyKey))
        {
            var cutoff = DateTime.UtcNow.AddHours(-24);
            var existing = await _db.Orders
                .Include(o => o.Location)
                .Include(o => o.CustomerUser)
                .Include(o => o.Items).ThenInclude(i => i.Toppings)
                .Include(o => o.DiscountCode)
                .FirstOrDefaultAsync(o =>
                    o.CustomerUserId == customerId &&
                    o.IdempotencyKey == dto.IdempotencyKey &&
                    o.CreatedAt >= cutoff, ct);
            if (existing is not null)
                return Result<OrderDto>.Success(MapToDto(existing), 200);
        }

        // Validate location
        var location = await _db.Locations
            .FirstOrDefaultAsync(l => l.Id == dto.LocationId && l.IsActive, ct);
        if (location == null)
            return Result<OrderDto>.Failure("Location not found or inactive.", 400);

        if (dto.Items == null || dto.Items.Count == 0)
            return Result<OrderDto>.Failure("Order must have at least one item.", 400);

        // Load menu items
        var itemIds = dto.Items.Select(i => i.MenuItemId).ToList();
        var menuItems = await _db.MenuItems
            .Include(m => m.MenuItemLocations)
            .Where(m => itemIds.Contains(m.Id) && m.IsAvailable)
            .ToListAsync(ct);

        if (menuItems.Count != itemIds.Distinct().Count())
            return Result<OrderDto>.Failure("One or more menu items are unavailable.", 400);

        foreach (var item in menuItems)
            if (item.MenuItemLocations.Any() && !item.MenuItemLocations.Any(l => l.LocationId == dto.LocationId))
                return Result<OrderDto>.Failure($"'{item.Name}' is not available at this location.", 400);

        // Build order
        var order = new Order
        {
            CustomerUserId = customerId,
            LocationId = dto.LocationId,
            Status = OrderStatus.Created
        };

        var variantIds = dto.Items
            .Where(i => i.VariantId.HasValue)
            .Select(i => i.VariantId!.Value)
            .ToList();

        var variants = variantIds.Count > 0
            ? await _db.MenuItemVariants.Where(v => variantIds.Contains(v.Id)).ToListAsync(ct)
            : [];

        var orderItems = dto.Items.Select(i =>
        {
            var menuItem = menuItems.First(m => m.Id == i.MenuItemId);
            var variant = i.VariantId.HasValue ? variants.FirstOrDefault(v => v.Id == i.VariantId.Value && v.MenuItemId == i.MenuItemId) : null;
            var basePrice = variant?.Price ?? menuItem.Price;
            var toppingTotal = i.Toppings?.Sum(t => t.Price) ?? 0m;
            var lineTotal = (basePrice + toppingTotal) * i.Quantity;
            var orderItem = new OrderItem
            {
                OrderId = order.Id,
                MenuItemId = i.MenuItemId,
                MenuItemName = menuItem.Name,
                Quantity = i.Quantity,
                UnitPrice = basePrice + toppingTotal,
                LineTotal = lineTotal,
                VariantId = variant?.Id,
                VariantLabel = variant?.Label,
                Notes = i.Notes
            };
            if (i.Toppings is { Count: > 0 })
                orderItem.Toppings = i.Toppings.Select(t => new OrderItemTopping
                {
                    OrderItemId = orderItem.Id,
                    ToppingId = t.ToppingId,
                    ToppingName = t.ToppingName,
                    Price = t.Price
                }).ToList();
            return orderItem;
        }).ToList();

        order.PaymentMethod = dto.PaymentMethod;
        order.Items = orderItems;
        order.Subtotal = orderItems.Sum(i => i.LineTotal);
        order.IdempotencyKey = dto.IdempotencyKey;

        // Apply discount code if provided
        if (!string.IsNullOrWhiteSpace(dto.DiscountCode))
        {
            var normalizedCode = dto.DiscountCode.Trim().ToUpperInvariant();
            var codeEntity = await _db.DiscountCodes
                .FirstOrDefaultAsync(d => d.Code == normalizedCode && d.IsActive, ct);

            if (codeEntity != null)
            {
                var validation = await _discountCodes.ValidateAsync(dto.DiscountCode, order.Subtotal, customerId, ct);
                if (validation.IsValid)
                {
                    order.DiscountAmount = validation.DiscountAmount;
                    order.DiscountCodeId = codeEntity.Id;
                    codeEntity.UsedCount++;
                }
            }
        }

        order.Total = order.Subtotal - order.DiscountAmount;

        // A payment method is required unless the customer declared enough bonus points
        // to cover the full total — otherwise there'd be no way to settle the remainder.
        if (dto.PaymentMethod == null && (dto.LoyaltyPointsToSpend ?? 0m) < order.Total)
            return Result<OrderDto>.Failure(
                "Select a payment method, or apply enough bonus points to cover the full total.", 400);

        _db.Orders.Add(order);
        await _db.SaveChangesAsync(ct);

        // Attach navigation properties needed by MapToDto and the hub notification
        order.Location = location;
        order.CustomerUser = await _db.CustomerUsers.FindAsync([customerId], ct);

        // Reserve loyalty points as (partial) payment — fails open to normal payment
        if (dto.LoyaltyPointsToSpend is > 0)
            await _loyalty.TryHoldForOrderAsync(order, dto.LoyaltyPointsToSpend.Value, ct);

        // Safety net: the customer intended full bonus coverage, but the amount actually
        // applied (clamped to the live iiko balance) fell short of the total — fall back
        // to a payment method so the order isn't left without one; the remainder is
        // settled at the counter like any other partial-bonus order.
        if (order.PaymentMethod == null && (order.LoyaltyPointsSpent ?? 0m) < order.Total)
        {
            order.PaymentMethod = PaymentMethod.Cash;
            await _db.SaveChangesAsync(ct);
        }

        await _hub.NotifyOrderCreatedAsync(order, ct);

        return Result<OrderDto>.Success(MapToDto(order), 201);
    }

    public async Task<Result<OrderDto>> GetOrderAsync(
        Guid orderId, Guid customerId, CancellationToken ct = default)
    {
        var order = await LoadOrderAsync(orderId, ct);
        if (order == null) return Result<OrderDto>.NotFound();
        if (order.CustomerUserId != customerId) return Result<OrderDto>.Forbidden();
        return Result<OrderDto>.Success(MapToDto(order));
    }

    public async Task<Result<OrderDto>> RateOrderAsync(
        Guid orderId, Guid customerId, RateOrderDto dto, CancellationToken ct = default)
    {
        if (dto.Rating < 1 || dto.Rating > 5)
            return Result<OrderDto>.Failure("Rating must be between 1 and 5.", 422);

        var order = await LoadOrderAsync(orderId, ct);
        if (order == null) return Result<OrderDto>.NotFound();
        if (order.CustomerUserId != customerId) return Result<OrderDto>.Forbidden();

        if (order.Status != OrderStatus.Completed)
            return Result<OrderDto>.Failure("Only completed orders can be rated.", 422);

        if (order.Rating.HasValue)
            return Result<OrderDto>.Failure("This order has already been rated.", 422);

        order.Rating = dto.Rating;
        order.RatingComment = string.IsNullOrWhiteSpace(dto.Comment) ? null : dto.Comment.Trim();
        order.RatedAt = DateTime.UtcNow;
        order.UpdatedAt = DateTime.UtcNow;
        await _db.SaveChangesAsync(ct);

        await _audit.LogAsync("OrderRated", "Order", orderId.ToString(), $"{dto.Rating} stars", ct);
        return Result<OrderDto>.Success(MapToDto(order));
    }

    public async Task<Result<OrderDto>> GetOrderForAdminAsync(
        Guid orderId, CancellationToken ct = default)
    {
        var order = await LoadOrderAsync(orderId, ct);
        if (order == null) return Result<OrderDto>.NotFound();
        return Result<OrderDto>.Success(MapToDto(order));
    }

    public async Task<List<OrderDto>> GetActiveOrdersAsync(
        Guid customerId, CancellationToken ct = default)
    {
        var activeStatuses = new List<OrderStatus>
        {
            OrderStatus.Created,
            OrderStatus.Accepted,
            OrderStatus.Preparing,
            OrderStatus.Ready
        };

        return await _db.Orders
            .Include(o => o.Location)
            .Include(o => o.Items).ThenInclude(i => i.Toppings)
            .Include(o => o.DiscountCode)
            .Where(o => o.CustomerUserId == customerId && activeStatuses.Contains(o.Status) && !o.IsArchived)
            .OrderByDescending(o => o.CreatedAt)
            .Select(o => MapToDto(o))
            .ToListAsync(ct);
    }

    public async Task<List<OrderDto>> GetHistoryAsync(
        Guid customerId, CancellationToken ct = default)
        => await _db.Orders
            .Include(o => o.Location)
            .Include(o => o.Items).ThenInclude(i => i.Toppings)
            .Include(o => o.DiscountCode)
            .Where(o => o.CustomerUserId == customerId && o.Status == OrderStatus.Completed && !o.IsArchived)
            .OrderByDescending(o => o.CreatedAt)
            .Select(o => MapToDto(o))
            .ToListAsync(ct);

    public async Task<List<OrderDto>> GetDeclinedOrdersAsync(
        Guid customerId, CancellationToken ct = default)
        => await _db.Orders
            .Include(o => o.Location)
            .Include(o => o.Items).ThenInclude(i => i.Toppings)
            .Include(o => o.DiscountCode)
            .Where(o => o.CustomerUserId == customerId && o.Status == OrderStatus.Declined && !o.IsArchived)
            .OrderByDescending(o => o.CreatedAt)
            .Select(o => MapToDto(o))
            .ToListAsync(ct);

    public async Task<(int Total, List<OrderDto> Items)> GetAdminOrdersAsync(
        OrderStatus? status, Guid? locationId, string? search,
        int page, int pageSize, CancellationToken ct = default)
    {
        var query = _db.Orders
            .Include(o => o.Location)
            .Include(o => o.CustomerUser)
            .Include(o => o.Items).ThenInclude(i => i.Toppings)
            .Include(o => o.DiscountCode)
            .Where(o => !o.IsArchived)
            .AsQueryable();

        if (status.HasValue) query = query.Where(o => o.Status == status.Value);
        if (locationId.HasValue) query = query.Where(o => o.LocationId == locationId.Value);

        if (!string.IsNullOrWhiteSpace(search))
        {
            // Postgres UUIDs are lowercase text; normalize to match what users type
            var s = search.Trim().ToLowerInvariant();
            // Npgsql translates Guid.ToString() to uuid::text at runtime
            query = query.Where(o =>
                o.Id.ToString().EndsWith(s) ||
                o.CustomerUser!.MobileNumber.Contains(s));
        }

        var total = await query.CountAsync(ct);
        var items = await query
            .OrderByDescending(o => o.CreatedAt)
            .Skip((page - 1) * pageSize)
            .Take(pageSize)
            .Select(o => MapToDto(o))
            .ToListAsync(ct);

        return (total, items);
    }

    public async Task<Result<OrderDto>> AcceptOrderAsync(
        Guid orderId, AcceptOrderDto dto, CancellationToken ct = default)
    {
        var order = await LoadOrderAsync(orderId, ct);
        if (order == null) return Result<OrderDto>.NotFound();

        if (order.Status != OrderStatus.Created)
            return Result<OrderDto>.Failure("Order cannot be accepted in its current status.", 422);

        if (order.PaymentStatus != PaymentStatus.Paid)
            return Result<OrderDto>.Failure("Order cannot be accepted until payment is confirmed.", 422);

        order.Status = OrderStatus.Accepted;
        order.EtaMinutes = dto.EtaMinutes;
        order.AcceptedAt = DateTime.UtcNow;
        order.UpdatedAt = DateTime.UtcNow;
        await _db.SaveChangesAsync(ct);

        await _hub.NotifyOrderUpdatedAsync(order, ct);
        await _audit.LogAsync("OrderAccepted", "Order", orderId.ToString(), $"ETA: {dto.EtaMinutes} min", ct);

        // iiko order push is a reporting/kitchen-routing side-channel only — fails open,
        // never blocks acceptance, and never affects the wallet loyalty flow above.
        await _iikoSync.PushOrderAsync(order, ct);

        return Result<OrderDto>.Success(MapToDto(order));
    }

    /// <summary>Customer-initiated cancellation. Only allowed while the order is still
    /// Created, i.e. before an admin/worker has accepted it.</summary>
    public async Task<Result<OrderDto>> CancelOrderAsync(
        Guid orderId, Guid customerId, CancellationToken ct = default)
    {
        var order = await LoadOrderAsync(orderId, ct);
        if (order == null) return Result<OrderDto>.NotFound();
        if (order.CustomerUserId != customerId) return Result<OrderDto>.Forbidden();

        if (order.Status != OrderStatus.Created)
            return Result<OrderDto>.Failure("Order can no longer be cancelled because it has already been accepted.", 422);

        order.Status = OrderStatus.Declined;
        order.DeclineReason = CustomerCancelReason;
        order.UpdatedAt = DateTime.UtcNow;
        await _db.SaveChangesAsync(ct);

        // Give reserved points back to the customer (retried in background on failure)
        await _loyalty.ReleaseHoldForOrderAsync(order, ct);

        // Plain "updated" event: refreshes the admin live list and the customer's view
        // without firing the "your order was declined" push meant for admin declines.
        await _hub.NotifyOrderUpdatedAsync(order, ct);
        await _audit.LogAsync("OrderCancelledByCustomer", "Order", orderId.ToString(), null, ct);
        return Result<OrderDto>.Success(MapToDto(order));
    }

    public async Task<Result<OrderDto>> DeclineOrderAsync(
        Guid orderId, DeclineOrderDto dto, CancellationToken ct = default)
    {
        var order = await LoadOrderAsync(orderId, ct);
        if (order == null) return Result<OrderDto>.NotFound();

        if (order.Status != OrderStatus.Created)
            return Result<OrderDto>.Failure("Order cannot be declined in its current status.", 422);

        order.Status = OrderStatus.Declined;
        order.DeclineReason = dto.Reason;
        order.UpdatedAt = DateTime.UtcNow;
        await _db.SaveChangesAsync(ct);

        // Give reserved points back to the customer (retried in background on failure)
        await _loyalty.ReleaseHoldForOrderAsync(order, ct);

        await _hub.NotifyOrderDeclinedAsync(order, ct);
        await _audit.LogAsync("OrderDeclined", "Order", orderId.ToString(), dto.Reason, ct);
        return Result<OrderDto>.Success(MapToDto(order));
    }

    public async Task<Result<OrderDto>> UpdateStatusAsync(
        Guid orderId, UpdateOrderStatusDto dto, CancellationToken ct = default)
    {
        var order = await LoadOrderAsync(orderId, ct);
        if (order == null) return Result<OrderDto>.NotFound();

        var allowed = new Dictionary<OrderStatus, OrderStatus[]>
        {
            [OrderStatus.Accepted] = [OrderStatus.Preparing],
            [OrderStatus.Preparing] = [OrderStatus.Ready],
            [OrderStatus.Ready] = [OrderStatus.Completed]
        };

        if (!allowed.TryGetValue(order.Status, out var next) || !next.Contains(dto.Status))
            return Result<OrderDto>.Failure(
                $"Cannot transition from {order.Status} to {dto.Status}.", 422);

        order.Status = dto.Status;
        if (dto.Status == OrderStatus.Completed) order.CompletedAt = DateTime.UtcNow;
        order.UpdatedAt = DateTime.UtcNow;
        await _db.SaveChangesAsync(ct);

        await _hub.NotifyOrderUpdatedAsync(order, ct);
        await _audit.LogAsync($"OrderStatus{dto.Status}", "Order", orderId.ToString(), null, ct);

        // Loyalty operations are best-effort: both methods log and swallow their own
        // failures (or defer to background retry) so iiko downtime can never fail
        // order completion. Spend must finalize before earning so the earn base
        // excludes the points-paid portion.
        if (dto.Status == OrderStatus.Completed)
        {
            await _loyalty.FinalizeSpendForOrderAsync(order, ct);
            await _loyalty.CreditForOrderAsync(order, ct);
            await _iikoSync.CloseOrderAsync(order, ct);
        }

        return Result<OrderDto>.Success(MapToDto(order));
    }

    public async Task<Result<OrderDto>> UpdatePaymentAsync(
        Guid orderId, UpdatePaymentDto dto, CancellationToken ct = default)
    {
        var order = await LoadOrderAsync(orderId, ct);
        if (order == null) return Result<OrderDto>.NotFound();

        order.PaymentStatus = dto.PaymentStatus;
        order.UpdatedAt = DateTime.UtcNow;
        await _db.SaveChangesAsync(ct);

        await _hub.NotifyPaymentUpdatedAsync(order, ct);
        await _audit.LogAsync("OrderPaymentUpdated", "Order", orderId.ToString(),
            $"{dto.PaymentStatus}", ct);
        return Result<OrderDto>.Success(MapToDto(order));
    }

    private async Task<Order?> LoadOrderAsync(Guid id, CancellationToken ct)
        => await _db.Orders
            .Include(o => o.Location)
            .Include(o => o.CustomerUser)
            .Include(o => o.Items).ThenInclude(i => i.Toppings)
            .Include(o => o.DiscountCode)
            .FirstOrDefaultAsync(o => o.Id == id, ct);

    public static OrderDto MapToDto(Order o)
    {
        var customer = o.CustomerUser;
        var customerName = customer == null
            ? ""
            : !string.IsNullOrWhiteSpace(customer.FirstName)
                ? $"{customer.FirstName} {customer.LastName}".Trim()
                : customer.MobileNumber;
        var customerPhone = customer?.MobileNumber ?? "";

        return new(
            o.Id,
            o.CustomerUserId,
            customerName,
            customerPhone,
            o.LocationId,
            o.Location?.Name ?? "",
            o.Status,
            o.DeclineReason,
            o.EtaMinutes,
            o.CreatedAt,
            o.AcceptedAt,
            o.CompletedAt,
            o.PaymentStatus,
            o.PaymentMethod,
            o.Subtotal,
            o.DiscountAmount,
            o.Total,
            o.DiscountCode?.Code,
            o.Items.Select(i => new OrderItemDto(
                i.Id, i.MenuItemId, i.MenuItemName, i.Quantity, i.UnitPrice, i.LineTotal,
                i.Toppings.Select(t => new OrderItemToppingDto(t.ToppingId, t.ToppingName, t.Price)).ToList(),
                i.Notes, i.VariantLabel
            )).ToList(),
            o.LoyaltyPointsSpent,
            o.LoyaltyPointsEarned,
            o.Rating,
            o.RatingComment,
            o.IikoOrderSyncStatus,
            o.Location?.TwoGisUrl);
    }
}
