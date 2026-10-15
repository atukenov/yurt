using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;
using Yurt.Application.Common.Interfaces;
using Yurt.Application.Features.Loyalty;
using Yurt.Domain.Entities;
using Yurt.Domain.Enums;

namespace Yurt.Application.Features.IikoIntegration.Services;

/// <summary>
/// Pushes accepted orders into iiko as real delivery orders — a pure side-channel for
/// iiko-side sales/inventory reporting and kitchen routing. Runs alongside (never
/// replaces) <see cref="Yurt.Application.Features.Loyalty.Services.LoyaltyService"/>'s
/// wallet hold/chargeoff flow, which stays the sole source of bonus math; the pushed
/// order uses iiko's "one-time customer" so iiko's own loyalty engine never touches the
/// wallet. Never drives the customer-facing <see cref="Order.Status"/> — that stays
/// entirely owned by the admin panel's Accept/Preparing/Ready/Complete actions.
/// </summary>
public class IikoOrderSyncService
{
    private readonly IApplicationDbContext _db;
    private readonly IIikoApiClient _iiko;
    private readonly IikoOptions _options;
    private readonly IAuditLogService _audit;
    private readonly ILogger<IikoOrderSyncService> _logger;

    public IikoOrderSyncService(
        IApplicationDbContext db,
        IIikoApiClient iiko,
        IikoOptions options,
        IAuditLogService audit,
        ILogger<IikoOrderSyncService> logger)
    {
        _db = db;
        _iiko = iiko;
        _options = options;
        _audit = audit;
        _logger = logger;
    }

    /// <summary>
    /// Push a just-accepted order to iiko. Fail-open and idempotent — never throws, since
    /// this must not block order acceptance. Orders with an unmapped item or a location
    /// with no configured terminal group are skipped (need an admin action, not a retry).
    /// </summary>
    public async Task PushOrderAsync(Order order, CancellationToken ct = default)
    {
        if (!_options.Enabled || !_options.PushOrdersEnabled) return;
        if (order.IikoOrderSyncStatus != IikoOrderSyncStatus.NotPushed) return; // already handled

        var terminalGroupId = order.Location?.IikoTerminalGroupId;
        if (terminalGroupId == null)
        {
            await SkipUnmappedAsync(order,
                $"location {order.LocationId} has no iiko terminal group configured", ct);
            return;
        }

        var menuItemIds = order.Items.Select(i => i.MenuItemId).Distinct().ToList();
        var menuItems = await _db.MenuItems
            .Include(m => m.Variants)
            .Where(m => menuItemIds.Contains(m.Id))
            .ToDictionaryAsync(m => m.Id, ct);

        var items = new List<IikoOrderItemRequest>();
        foreach (var orderItem in order.Items)
        {
            if (!menuItems.TryGetValue(orderItem.MenuItemId, out var menuItem) || menuItem.IikoProductId == null)
            {
                await SkipUnmappedAsync(order,
                    $"menu item \"{orderItem.MenuItemName}\" ({orderItem.MenuItemId}) has no iiko product mapping", ct);
                return;
            }

            Guid? productSizeId = menuItem.IikoProductSizeId;
            if (orderItem.VariantId != null)
            {
                var variant = menuItem.Variants.FirstOrDefault(v => v.Id == orderItem.VariantId);
                if (variant?.IikoProductSizeId == null)
                {
                    await SkipUnmappedAsync(order,
                        $"variant \"{orderItem.VariantLabel}\" of \"{orderItem.MenuItemName}\" has no iiko size mapping", ct);
                    return;
                }
                productSizeId = variant.IikoProductSizeId;
            }

            // Toppings/modifiers are pushed as text, not real iiko modifiers (no
            // modifier-schema mapping exists) — still useful on a printed kitchen ticket.
            var commentParts = new List<string>();
            if (orderItem.Toppings.Count > 0)
                commentParts.Add(string.Join(", ", orderItem.Toppings.Select(t => t.ToppingName)));
            if (!string.IsNullOrWhiteSpace(orderItem.Notes))
                commentParts.Add(orderItem.Notes);

            items.Add(new IikoOrderItemRequest(
                menuItem.IikoProductId.Value, productSizeId, orderItem.Quantity, orderItem.UnitPrice,
                commentParts.Count > 0 ? string.Join(" — ", commentParts) : null));
        }

        var customer = order.CustomerUser;
        var request = new IikoCreateOrderRequest(
            terminalGroupId.Value,
            customer?.MobileNumber ?? "",
            $"{customer?.FirstName} {customer?.LastName}".Trim(),
            items,
            order.Total,
            $"Altyncup order #{order.Id}");

        try
        {
            var iikoOrderId = await _iiko.CreateDeliveryOrderAsync(request, ct);
            order.IikoDeliveryOrderId = iikoOrderId;
            order.IikoOrderSyncStatus = IikoOrderSyncStatus.Pushed;
            order.IikoOrderSyncPendingAction = IikoOrderSyncPendingAction.None;
            await _db.SaveChangesAsync(ct);
            await _audit.LogAsync("IikoOrderPushed", "Order", order.Id.ToString(), $"iiko order {iikoOrderId}", ct);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to push order {OrderId} to iiko; will retry", order.Id);
            order.IikoOrderSyncPendingAction = IikoOrderSyncPendingAction.Push;
            await _db.SaveChangesAsync(ct);
        }
    }

    /// <summary>Close a previously pushed order in iiko when it completes. Fail-open; never throws.</summary>
    public async Task CloseOrderAsync(Order order, CancellationToken ct = default)
    {
        if (!_options.Enabled || !_options.PushOrdersEnabled) return;
        if (order.IikoOrderSyncStatus != IikoOrderSyncStatus.Pushed || order.IikoDeliveryOrderId == null) return;

        try
        {
            await _iiko.CloseDeliveryOrderAsync(order.IikoDeliveryOrderId.Value, ct);
            order.IikoOrderSyncStatus = IikoOrderSyncStatus.Closed;
            order.IikoOrderSyncPendingAction = IikoOrderSyncPendingAction.None;
            await _db.SaveChangesAsync(ct);
            await _audit.LogAsync("IikoOrderClosed", "Order", order.Id.ToString(), null, ct);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to close iiko order for {OrderId}; will retry", order.Id);
            order.IikoOrderSyncPendingAction = IikoOrderSyncPendingAction.Close;
            await _db.SaveChangesAsync(ct);
        }
    }

    /// <summary>Background retry of push/close operations that failed while iiko was down.</summary>
    public async Task RetryPendingAsync(CancellationToken ct = default)
    {
        if (!_options.Enabled || !_options.PushOrdersEnabled) return;

        var pending = await _db.Orders
            .Include(o => o.Location)
            .Include(o => o.CustomerUser)
            .Include(o => o.Items).ThenInclude(i => i.Toppings)
            .Where(o => o.IikoOrderSyncPendingAction != IikoOrderSyncPendingAction.None)
            .OrderBy(o => o.UpdatedAt)
            .Take(50)
            .ToListAsync(ct);

        foreach (var order in pending)
        {
            switch (order.IikoOrderSyncPendingAction)
            {
                case IikoOrderSyncPendingAction.Push:
                    order.IikoOrderSyncStatus = IikoOrderSyncStatus.NotPushed; // allow PushOrderAsync's guard to re-run
                    await PushOrderAsync(order, ct);
                    break;
                case IikoOrderSyncPendingAction.Close:
                    await CloseOrderAsync(order, ct);
                    break;
            }
        }
    }

    private async Task SkipUnmappedAsync(Order order, string reason, CancellationToken ct)
    {
        _logger.LogWarning("Order {OrderId} not pushed to iiko: {Reason}", order.Id, reason);
        order.IikoOrderSyncStatus = IikoOrderSyncStatus.SkippedUnmapped;
        // Not a transient failure — needs an admin fix (map the item / configure the
        // location), not an endless retry loop, so clear any stale pending-action.
        order.IikoOrderSyncPendingAction = IikoOrderSyncPendingAction.None;
        await _db.SaveChangesAsync(ct);
        await _audit.LogAsync("IikoOrderSkippedUnmapped", "Order", order.Id.ToString(), reason, ct);
    }
}
