using Yurt.Application.Features.GroupOrders;
using Yurt.Domain.Entities;

namespace Yurt.Application.Common.Interfaces;

public interface IOrdersHubService
{
    Task NotifyOrderCreatedAsync(Order order, CancellationToken cancellationToken = default);
    Task NotifyOrderUpdatedAsync(Order order, CancellationToken cancellationToken = default);
    Task NotifyOrderDeclinedAsync(Order order, CancellationToken cancellationToken = default);
    Task NotifyPaymentUpdatedAsync(Order order, CancellationToken cancellationToken = default);
    Task NotifyPaymentPendingAsync(Order order, CancellationToken cancellationToken = default);
    Task NotifyPaymentSucceededAsync(Order order, CancellationToken cancellationToken = default);
    Task NotifyPaymentFailedAsync(Order order, CancellationToken cancellationToken = default);
    Task NotifyGroupCartUpdatedAsync(Guid groupCartId, GroupCartDto dto, CancellationToken cancellationToken = default);
    Task NotifyEtaReminderAsync(Guid orderId, Guid customerUserId, int minutesLeft, CancellationToken cancellationToken = default);
}
