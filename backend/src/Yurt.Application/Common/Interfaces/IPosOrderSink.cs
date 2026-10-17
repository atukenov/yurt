using Yurt.Domain.Entities;

namespace Yurt.Application.Common.Interfaces;

/// <summary>
/// Where an accepted order goes next, whatever runs the cafe's kitchen and till (Phase 6). Today the only
/// adapter is iiko (<c>IikoOrderSyncService</c>); the self-developed <c>own_pos</c> module will add its own.
/// Both calls are fail-open: they must never throw, so a POS outage can never block taking or finishing an order.
/// </summary>
public interface IPosOrderSink
{
    /// <summary>Hand a just-accepted order to the POS.</summary>
    Task PushOrderAsync(Order order, CancellationToken ct = default);

    /// <summary>Tell the POS an order is complete.</summary>
    Task CloseOrderAsync(Order order, CancellationToken ct = default);
}
