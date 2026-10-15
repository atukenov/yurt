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

    public OrdersHubService(IHubContext<OrdersHub> hubContext, PushNotificationService push)
    {
        _hubContext = hubContext;
        _push = push;
    }

    public async Task NotifyOrderCreatedAsync(Order order, CancellationToken ct = default)
    {
        var dto = OrderService.MapToDto(order);
        // Notify all admins
        await _hubContext.Clients
            .Group("admins")
            .SendAsync("OrderCreated", dto, ct);
        // Notify customer
        await _hubContext.Clients
            .Group($"customer:{order.CustomerUserId}")
            .SendAsync("OrderUpdated", dto, ct);
        // No push here — the customer is already looking at the app when they place an
        // order, so a push on creation would just be noise.
    }

    public async Task NotifyOrderUpdatedAsync(Order order, CancellationToken ct = default)
    {
        var dto = OrderService.MapToDto(order);
        await _hubContext.Clients
            .Group("admins")
            .SendAsync("OrderUpdated", dto, ct);
        await _hubContext.Clients
            .Group($"customer:{order.CustomerUserId}")
            .SendAsync("OrderUpdated", dto, ct);

        var copy = StatusPushCopy(order.Status);
        if (copy != null)
        {
            await _push.SendOrderStatusPushAsync(
                order.CustomerUserId, copy.Value.Title, copy.Value.Body,
                new Dictionary<string, string> { ["orderId"] = order.Id.ToString() }, ct);
        }
    }

    public async Task NotifyOrderDeclinedAsync(Order order, CancellationToken ct = default)
    {
        var dto = OrderService.MapToDto(order);
        await _hubContext.Clients
            .Group("admins")
            .SendAsync("OrderDeclined", dto, ct);
        await _hubContext.Clients
            .Group($"customer:{order.CustomerUserId}")
            .SendAsync("OrderDeclined", dto, ct);

        var body = string.IsNullOrWhiteSpace(order.DeclineReason)
            ? "Ваш заказ отклонён"
            : $"Ваш заказ отклонён: {order.DeclineReason}";
        await _push.SendOrderStatusPushAsync(
            order.CustomerUserId, "Заказ отклонён", body,
            new Dictionary<string, string> { ["orderId"] = order.Id.ToString() }, ct);
    }

    /// <summary>Short fixed Russian copy per status — the backend has no persisted
    /// per-customer language preference today, matching the frontend's own
    /// default-language fallback. Null (Created and any other value) sends no push.</summary>
    private static (string Title, string Body)? StatusPushCopy(OrderStatus status) => status switch
    {
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
            .Group("admins")
            .SendAsync("PaymentUpdated", dto, ct);
        await _hubContext.Clients
            .Group($"customer:{order.CustomerUserId}")
            .SendAsync("PaymentUpdated", dto, ct);
    }

    public async Task NotifyPaymentPendingAsync(Order order, CancellationToken ct = default)
    {
        var dto = OrderService.MapToDto(order);
        await _hubContext.Clients
            .Group("admins")
            .SendAsync("PaymentPending", dto, ct);
        await _hubContext.Clients
            .Group($"customer:{order.CustomerUserId}")
            .SendAsync("PaymentPending", dto, ct);
    }

    public async Task NotifyPaymentSucceededAsync(Order order, CancellationToken ct = default)
    {
        var dto = OrderService.MapToDto(order);
        await _hubContext.Clients
            .Group("admins")
            .SendAsync("PaymentSucceeded", dto, ct);
        await _hubContext.Clients
            .Group($"customer:{order.CustomerUserId}")
            .SendAsync("PaymentSucceeded", dto, ct);
    }

    public async Task NotifyPaymentFailedAsync(Order order, CancellationToken ct = default)
    {
        var dto = OrderService.MapToDto(order);
        await _hubContext.Clients
            .Group("admins")
            .SendAsync("PaymentFailed", dto, ct);
        await _hubContext.Clients
            .Group($"customer:{order.CustomerUserId}")
            .SendAsync("PaymentFailed", dto, ct);
    }

    public async Task NotifyGroupCartUpdatedAsync(Guid groupCartId, GroupCartDto dto, CancellationToken ct = default)
        => await _hubContext.Clients
            .Group($"group-{groupCartId}")
            .SendAsync("GroupCartUpdated", dto, ct);

    public async Task NotifyEtaReminderAsync(Guid orderId, Guid customerUserId, int minutesLeft, CancellationToken ct = default)
        => await _hubContext.Clients
            .Group($"customer:{customerUserId}")
            .SendAsync("EtaReminder", new { orderId, minutesLeft }, ct);
}
