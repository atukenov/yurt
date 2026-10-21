using Microsoft.EntityFrameworkCore;
using Yurt.Application.Common.Interfaces;
using Yurt.Application.Common.Models;
using Yurt.Application.Features.Orders.DTOs;
using Yurt.Application.Features.Orders.Services;
using Yurt.Domain.Entities;
using Yurt.Domain.Enums;

namespace Yurt.Application.Features.Stations;

/// <summary>
/// Each station marks its own part of an order ready (Phase 6, <c>own_pos</c>). When every station that has items in
/// the order is ready, the order itself moves to Ready, the same as pressing Ready on the whole order. An order with
/// items whose category has no station never moves on its own: nobody owns those items, so staff press Ready as before.
/// </summary>
public class StationReadyService(IApplicationDbContext db, OrderService orders, IOrdersHubService hub, IAuditLogService audit)
{
    public async Task<Result<OrderDto>> SetReadyAsync(Guid orderId, Guid stationId, bool ready, CancellationToken ct = default)
    {
        var order = await orders.LoadOrderAsync(orderId, ct);
        if (order is null) return Result<OrderDto>.NotFound();
        if (order.Status is not (OrderStatus.Accepted or OrderStatus.Preparing))
            return Result<OrderDto>.Failure("Only an order that is being made can have a part marked ready.", 409);
        var station = await db.Stations.FirstOrDefaultAsync(s => s.Id == stationId, ct);
        if (station is null) return Result<OrderDto>.Failure("Station not found.", 404);

        var menuItemIds = order.Items.Select(i => i.MenuItemId).Distinct().ToList();
        var itemStations = await db.MenuItems.Where(m => menuItemIds.Contains(m.Id))
            .Select(m => new { m.Id, m.Category.StationId }).ToListAsync(ct);
        var needed = itemStations.Where(m => m.StationId != null).Select(m => m.StationId!.Value).ToHashSet();
        if (!needed.Contains(stationId))
            return Result<OrderDto>.Failure("This order has nothing made at this station.", 400);

        var existing = order.ReadyStations.FirstOrDefault(r => r.StationId == stationId);
        if (ready == (existing is not null)) return Result<OrderDto>.Success(OrderService.MapToDto(order)); // Nothing to change.
        if (ready)
            // Added through the set, not the collection: the id is set up front, so the collection alone would look like an
            // update. EF puts the new row into order.ReadyStations itself.
            db.OrderStationReadies.Add(new OrderStationReady { OrderId = order.Id, StationId = stationId });
        else
        {
            order.ReadyStations.Remove(existing!);
            db.OrderStationReadies.Remove(existing!);
        }

        // Items deleted from the menu since the order was placed have no station today; they do not block.
        var everyItemRouted = itemStations.Count == menuItemIds.Count && itemStations.All(m => m.StationId != null);
        var allReady = needed.All(id => order.ReadyStations.Any(r => r.StationId == id));
        var orderReady = ready && everyItemRouted && allReady;
        if (orderReady) order.Status = OrderStatus.Ready;
        order.UpdatedAt = DateTime.UtcNow;
        await db.SaveChangesAsync(ct);

        await hub.NotifyOrderUpdatedAsync(order, ct);
        await audit.LogAsync(ready ? "OrderStationReady" : "OrderStationNotReady", "Order", orderId.ToString(), station.Name, ct);
        if (orderReady) await audit.LogAsync("OrderStatusReady", "Order", orderId.ToString(), "All stations ready", ct);
        return Result<OrderDto>.Success(OrderService.MapToDto(order));
    }
}
