using Microsoft.AspNetCore.SignalR;
using Yurt.Application.Common.Interfaces;
using Yurt.Application.Features.GroupOrders;
using Yurt.Application.Features.Push.Services;
using Yurt.Domain.Entities;
using Yurt.Domain.Enums;
using Yurt.Application.Features.Orders.Services;

namespace Yurt.Infrastructure.Hubs;

public class OrdersHubService : IOrdersHubService
{
    private readonly IHubContext<OrdersHub> _hubContext;
    private readonly PushNotificationService _push;
    private readonly ITenantContext _tenant;

    public OrdersHubService(IHubContext<OrdersHub> hubContext, PushNotificationService push, ITenantContext tenant)
    {
        _hubContext = hubContext;
        _push = push;
        _tenant = tenant;
    }

    /// <summary>The order's own cafe; a row that is not saved yet has no cafe stamped, so the current one is used.</summary>
    private string Staff(Order order)
        => OrdersHub.AdminsGroup(order.TenantId == Guid.Empty ? _tenant.TenantId : order.TenantId);

    /// <summary>Sends to the guest's own app. A counter sale to a walk-in guest has no account, so nobody gets it.</summary>
    private Task ToGuestAsync(Order order, string method, object dto, CancellationToken ct)
        => order.CustomerUserId is { } guestId
            ? _hubContext.Clients.Group($"customer:{guestId}").SendAsync(method, dto, ct)
            : Task.CompletedTask;

    public async Task NotifyOrderCreatedAsync(Order order, CancellationToken ct = default)
    {
        var dto = OrderService.MapToDto(order);
        // Notify this cafe's staff only
        await _hubContext.Clients
            .Group(Staff(order))
            .SendAsync("OrderCreated", dto, ct);
        // Notify customer
        await ToGuestAsync(order, "OrderUpdated", dto, ct);
        // No push here — the customer is already looking at the app when they place an
        // order, so a push on creation would just be noise.
    }

    public async Task NotifyOrderUpdatedAsync(Order order, CancellationToken ct = default)
    {
        var dto = OrderService.MapToDto(order);
        await _hubContext.Clients
            .Group(Staff(order))
            .SendAsync("OrderUpdated", dto, ct);
        await ToGuestAsync(order, "OrderUpdated", dto, ct);

        var copy = StatusPushCopy(order.Status, order.TableLabel);
        if (copy != null && order.CustomerUserId is { } guestId)
        {
            await _push.SendOrderStatusPushAsync(
                guestId, copy.Value.Title, copy.Value.Body,
                new Dictionary<string, string> { ["orderId"] = order.Id.ToString() }, ct);
        }
    }

    public async Task NotifyOrderDeclinedAsync(Order order, CancellationToken ct = default)
    {
        var dto = OrderService.MapToDto(order);
        await _hubContext.Clients
            .Group(Staff(order))
            .SendAsync("OrderDeclined", dto, ct);
        await ToGuestAsync(order, "OrderDeclined", dto, ct);

        var body = string.IsNullOrWhiteSpace(order.DeclineReason)
            ? "Ваш заказ отклонён"
            : $"Ваш заказ отклонён: {order.DeclineReason}";
        if (order.CustomerUserId is { } guestId)
            await _push.SendOrderStatusPushAsync(
                guestId, "Заказ отклонён", body,
                new Dictionary<string, string> { ["orderId"] = order.Id.ToString() }, ct);
    }

    /// <summary>Short fixed Russian copy per status — the backend has no persisted
    /// per-customer language preference today, matching the frontend's own
    /// default-language fallback. Null (Created and any other value) sends no push.</summary>
    private static (string Title, string Body)? StatusPushCopy(OrderStatus status, string? table) => status switch
    {
        // A table order is carried over by staff, so the guest does not come to the counter.
        OrderStatus.Ready when !string.IsNullOrWhiteSpace(table) => ("Заказ готов", $"Несём ваш заказ к столу {table}"),
        OrderStatus.Accepted => ("Заказ принят", "Ваш заказ принят и готовится"),
        OrderStatus.Preparing => ("Заказ готовится", "Ваш заказ готовится"),
        OrderStatus.Ready => ("Заказ готов", "Ваш заказ готов к выдаче"),
        OrderStatus.Completed => ("Заказ выполнен", "Спасибо за заказ!"),
        _ => null,
    };

    public async Task NotifyPaymentUpdatedAsync(Order order, CancellationToken ct = default)
    {
        var dto = OrderService.MapToDto(order);
        await _hubContext.Clients
            .Group(Staff(order))
            .SendAsync("PaymentUpdated", dto, ct);
        await ToGuestAsync(order, "PaymentUpdated", dto, ct);
    }

    public async Task NotifyPaymentPendingAsync(Order order, CancellationToken ct = default)
    {
        var dto = OrderService.MapToDto(order);
        await _hubContext.Clients
            .Group(Staff(order))
            .SendAsync("PaymentPending", dto, ct);
        await ToGuestAsync(order, "PaymentPending", dto, ct);
    }

    public async Task NotifyPaymentSucceededAsync(Order order, CancellationToken ct = default)
    {
        var dto = OrderService.MapToDto(order);
        await _hubContext.Clients
            .Group(Staff(order))
            .SendAsync("PaymentSucceeded", dto, ct);
        await ToGuestAsync(order, "PaymentSucceeded", dto, ct);
    }

    public async Task NotifyPaymentFailedAsync(Order order, CancellationToken ct = default)
    {
        var dto = OrderService.MapToDto(order);
        await _hubContext.Clients
            .Group(Staff(order))
            .SendAsync("PaymentFailed", dto, ct);
        await ToGuestAsync(order, "PaymentFailed", dto, ct);
    }

    public async Task NotifyGroupCartUpdatedAsync(Guid groupCartId, GroupCartDto dto, CancellationToken ct = default)
        => await _hubContext.Clients
            .Group(OrdersHub.GroupCartGroup(_tenant.TenantId, groupCartId))
            .SendAsync("GroupCartUpdated", dto, ct);

    public async Task NotifyEtaReminderAsync(Guid orderId, Guid customerUserId, int minutesLeft, CancellationToken ct = default)
        => await _hubContext.Clients
            .Group($"customer:{customerUserId}")
            .SendAsync("EtaReminder", new { orderId, minutesLeft }, ct);
}
