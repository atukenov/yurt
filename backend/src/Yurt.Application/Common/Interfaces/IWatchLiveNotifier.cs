namespace Yurt.Application.Common.Interfaces;

/// <summary>Pushes a branch's live staff watch state to the managers who have its Staff watch page open (SignalR).</summary>
public interface IWatchLiveNotifier
{
    Task SendAsync(Guid tenantId, Guid locationId, object live, CancellationToken ct = default);
}
