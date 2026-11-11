using Microsoft.AspNetCore.SignalR;
using Yurt.Application.Common.Interfaces;

namespace Yurt.Infrastructure.Hubs;

/// <summary>Sends "WatchLive" to the branch's staff watch group (managers only; see <see cref="OrdersHub.SubscribeToWatch"/>).</summary>
public class WatchLiveNotifier(IHubContext<OrdersHub> hub) : IWatchLiveNotifier
{
    public Task SendAsync(Guid tenantId, Guid locationId, object live, CancellationToken ct = default)
        => hub.Clients.Group(OrdersHub.WatchGroup(tenantId, locationId.ToString())).SendAsync("WatchLive", live, ct);
}
