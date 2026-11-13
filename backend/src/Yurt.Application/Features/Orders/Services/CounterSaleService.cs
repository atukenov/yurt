using Microsoft.EntityFrameworkCore;
using Yurt.Application.Common.Interfaces;
using Yurt.Application.Common.Models;
using Yurt.Application.Features.Approvals;
using Yurt.Application.Features.Combos;
using Yurt.Application.Features.Menu.Services;
using Yurt.Application.Features.Orders.DTOs;
using Yurt.Application.Features.Printing;
using Yurt.Application.Features.Profit;
using Yurt.Application.Features.Stock;
using Yurt.Domain.Entities;
using Yurt.Domain.Enums;

namespace Yurt.Application.Features.Orders.Services;

/// <summary>
/// A sale rung up by a cashier at the counter (Yurt POS): a walk-in guest with no account, paid on the spot. Unlike an
/// app order it skips the "waiting for the cafe" step: it is taken on (Accepted) at once, or handed over (Completed) at
/// once for things given straight away. The till can send the same sale again with the same key (a lost answer, or a
/// sale made while the internet was down) and gets the first one back, never a second order.
/// </summary>
public class CounterSaleService(
    IApplicationDbContext db, IOrdersHubService hub, IAuditLogService audit, ICurrentUserService user,
    StockService? stock = null, PrintService? print = null, ManagerApprovalService? approvals = null)
{
    // A sale made on the till while it was offline keeps its own time, at most this long ago. Older sales are refused
    // so a forgotten tablet cannot drop last month's sales into today's numbers unseen.
    public static readonly TimeSpan MaxSaleAge = TimeSpan.FromDays(7);
    // The till's clock may run a little ahead of the server's.
    public static readonly TimeSpan MaxClockAhead = TimeSpan.FromMinutes(5);
    // A sale sent later than this after it was made happened while the till was offline: the till already printed its
    // receipt and opened its drawer itself, so the server does not print or open anything again.
    public static readonly TimeSpan LiveWindow = TimeSpan.FromMinutes(2);
    /// <summary>How long after a time menu closes a dish already on the till can still be sold.</summary>
    public static readonly TimeSpan TimeMenuGrace = TimeSpan.FromMinutes(10);

    public const int MaxEtaMinutes = 240;

    private static readonly PaymentMethod[] CounterMethods =
        [PaymentMethod.Cash, PaymentMethod.Card, PaymentMethod.KaspiBank, PaymentMethod.HalykBank, PaymentMethod.FreedomBank];

    public async Task<Result<OrderDto>> CreateAsync(CreateCounterSaleDto dto, CancellationToken ct = default)
    {
        if (string.IsNullOrWhiteSpace(dto.IdempotencyKey) || dto.IdempotencyKey.Length > 128)
            return Result<OrderDto>.Failure("Every sale needs its own key (up to 128 characters) so it is never counted twice.", 400);
        var key = dto.IdempotencyKey.Trim();

        var existing = await LoadAsync(o => o.LocationId == dto.LocationId && o.CustomerUserId == null && o.IdempotencyKey == key, ct);
        if (existing is not null) return Result<OrderDto>.Success(OrderService.MapToDto(existing), 200);

        if (dto.Items is not { Count: > 0 })
            return Result<OrderDto>.Failure("A sale must have at least one item.", 400);
        if (dto.Items.Any(i => i.Quantity is < 1 or > 999))
            return Result<OrderDto>.Failure("Each item's quantity must be between 1 and 999.", 400);
        if (!CounterMethods.Contains(dto.PaymentMethod))
            return Result<OrderDto>.Failure("Choose how the guest paid: cash, card, Kaspi, Halyk or Freedom.", 400);
        if (dto.Tip is < 0) return Result<OrderDto>.Failure("A tip can't be negative.", 400);
        if (dto.TipMethod is { } tm && !CounterMethods.Contains(tm))
            return Result<OrderDto>.Failure("Choose how the tip was paid: cash, card, Kaspi, Halyk or Freedom.", 400);
        if (dto.EtaMinutes is < 0 or > MaxEtaMinutes)
            return Result<OrderDto>.Failure($"Ready time must be between 0 and {MaxEtaMinutes} minutes.", 400);

        var now = DateTime.UtcNow;
        var soldAt = now;
        if (dto.SoldAt is { } given)
        {
            soldAt = given.Kind == DateTimeKind.Unspecified ? DateTime.SpecifyKind(given, DateTimeKind.Utc) : given.ToUniversalTime();
            if (soldAt > now + MaxClockAhead)
                return Result<OrderDto>.Failure("The sale time is in the future. Check the clock on the till.", 400);
            if (soldAt < now - MaxSaleAge)
                return Result<OrderDto>.Failure("This sale is more than 7 days old and was not added. Enter it by hand if it is real.", 400);
            if (soldAt > now) soldAt = now;
        }
        var live = now - soldAt <= LiveWindow;

        var location = await db.Locations.FirstOrDefaultAsync(l => l.Id == dto.LocationId, ct);
        // A live sale needs an open branch; a sale made earlier while offline is still taken in (the money is in the till).
        if (location is null || (live && !location.IsActive))
            return Result<OrderDto>.Failure("Branch not found or inactive.", 400);

        if (Locations.Services.LocationService.OfferedMethods(location.PaymentMethods) is { } offered
            && !offered.Contains(dto.PaymentMethod))
            return Result<OrderDto>.Failure("This branch does not accept that payment method.", 422);

        var itemIds = dto.Items.Select(i => i.MenuItemId).Distinct().ToList();
        var menuItems = await db.MenuItems.Include(m => m.MenuItemLocations)
            .Where(m => itemIds.Contains(m.Id)).ToDictionaryAsync(m => m.Id, ct);
        foreach (var id in itemIds)
        {
            if (!menuItems.TryGetValue(id, out var item))
                return Result<OrderDto>.Failure("One of the items is no longer on the menu.", 400);
            // The stop-list and branch menu are checked only for a live sale: an offline sale already happened.
            if (live && !item.IsAvailable)
                return Result<OrderDto>.Failure($"'{item.Name}' is on the stop-list.", 400);
            if (live && item.MenuItemLocations.Any() && item.MenuItemLocations.All(l => l.LocationId != dto.LocationId))
                return Result<OrderDto>.Failure($"'{item.Name}' is not sold at this branch.", 400);
        }

        // Time menus. Prices follow the clock at the moment of the sale (the till's own time for a sale made offline).
        // A live sale is allowed a few minutes after a window closes, so a dish rung up at 10:59 can still be paid at 11:01.
        var timed = await TimedMenu.LoadAsync(db, location.Id, soldAt, ct);
        if (live)
        {
            var recent = await TimedMenu.LoadAsync(db, location.Id, now - TimeMenuGrace, ct);
            foreach (var id in itemIds)
                if (!timed.IsOnMenu(menuItems[id]) && !recent.IsOnMenu(menuItems[id]))
                    return Result<OrderDto>.Failure($"'{menuItems[id].Name}' is not on the menu at this time.", 400);
        }

        // Combos: what is inside, and whether every dish in them can be sold here (a live sale only).
        var combos = await ComboSales.LoadAsync(db, menuItems.Values, ct);
        foreach (var combo in menuItems.Values.Where(m => m.IsCombo))
            if (combos.Problem(combo, dto.LocationId, live) is { } comboProblem)
                return Result<OrderDto>.Failure(comboProblem, 400);

        var variantIds = dto.Items.Where(i => i.VariantId.HasValue).Select(i => i.VariantId!.Value).Distinct().ToList();
        var variants = variantIds.Count > 0
            ? await db.MenuItemVariants.Where(v => variantIds.Contains(v.Id)).ToDictionaryAsync(v => v.Id, ct)
            : [];
        // Prices come from the menu, never from the till: a till with an old menu can't sell at a wrong price.
        var soldOutToppings = live && stock is not null ? await stock.SoldOutToppingIdsAsync(dto.LocationId, ct) : [];
        var offeredByItem = await DishOptions.LoadAsync(db, menuItems.Values.ToList(), dto.LocationId, soldOutToppings, !live, ct);
        var optionGroups = await DishOptions.LoadGroupsAsync(
            db, offeredByItem.Values.SelectMany(v => v).Select(o => o.Topping.Group), ct);

        var order = new Order
        {
            CustomerUserId = null,
            Source = OrderSource.Counter,
            OrderType = dto.OrderType,
            SoldByAdminId = user.UserId,
            LocationId = location.Id,
            Status = OrderStatus.Accepted,
            PaymentMethod = dto.PaymentMethod,
            PaymentStatus = PaymentStatus.Paid,
            IdempotencyKey = key,
            CreatedAt = soldAt,
            AcceptedAt = soldAt,
            EtaMinutes = dto.HandedOver ? null : dto.EtaMinutes ?? 0,
        };

        foreach (var line in dto.Items)
        {
            var item = menuItems[line.MenuItemId];
            MenuItemVariant? variant = null;
            if (line.VariantId is { } vid && !item.IsCombo && (!variants.TryGetValue(vid, out variant) || variant.MenuItemId != item.Id))
                return Result<OrderDto>.Failure($"That size of '{item.Name}' is no longer on the menu.", 400);

            var dishOpts = offeredByItem[item.Id];
            var pickedIds = (line.ToppingIds ?? []).Distinct().ToList();
            if (pickedIds.Any(id => dishOpts.All(o => o.Topping.Id != id)))
                return Result<OrderDto>.Failure($"One of the add-ons for '{item.Name}' is not available.", 400);
            // The choice rules apply to a live sale; a sale made earlier while offline already happened.
            if (live && DishOptions.Check(item.Name, dishOpts, pickedIds, optionGroups) is { } problem)
                return Result<OrderDto>.Failure(problem, 400);
            var lineToppings = dishOpts.Where(o => pickedIds.Contains(o.Topping.Id))
                .Select(o => new OrderItemTopping { ToppingId = o.Topping.Id, ToppingName = o.Topping.Name, Price = o.Price })
                .ToList();

            var unit = timed.PriceOf(item, variant?.Price ?? DishOptions.BranchPrice(item, location.Id), variant is not null) + lineToppings.Sum(t => t.Price);
            var orderItem = new OrderItem
            {
                OrderId = order.Id,
                MenuItemId = item.Id,
                MenuItemName = item.Name,
                Quantity = line.Quantity,
                UnitPrice = unit,
                LineTotal = unit * line.Quantity,
                VariantId = variant?.Id,
                VariantLabel = variant?.Label,
                Notes = string.IsNullOrWhiteSpace(line.Notes) ? null : line.Notes.Trim(),
            };
            foreach (var t in lineToppings) t.OrderItemId = orderItem.Id;
            orderItem.Toppings = lineToppings;
            if (item.IsCombo)
            {
                orderItem.ComboParts = combos.Snapshot(item, dto.LocationId);
                foreach (var part in orderItem.ComboParts) part.OrderItemId = orderItem.Id;
            }
            order.Items.Add(orderItem);
        }

        order.Subtotal = order.Items.Sum(i => i.LineTotal);
        order.Total = order.Subtotal;
        // A discount from the till needs a manager's PIN and a reason. It is checked here, after the retry check above, so
        // resending a sale that already went through never asks twice.
        ManagerApproval? approval = null;
        if (dto.Discount is { } discount)
        {
            var amount = ManualDiscount.Compute(order.Subtotal, discount);
            if (amount.Error is not null) return Result<OrderDto>.Failure(amount.Error, 400);
            if (approvals is not null)
            {
                var gate = await approvals.RequireAsync("Discount", new ManagerApprovalDto(discount.Pin, discount.Reason), ct);
                if (!gate.Succeeded) return Result<OrderDto>.Failure(gate.Error!, gate.StatusCode);
                approval = gate.Data;
            }
            order.DiscountAmount = amount.Value;
            order.Total = order.Subtotal - amount.Value;
        }
        // A tip is on top of the bill and not part of the total. It goes to the cashier who rang the sale up. No PIN is
        // asked to add it at the till; changing it afterwards does.
        if (dto.Tip is > 0)
        {
            var tipError = TipRules.Check(dto.Tip.Value, order.Total);
            if (tipError is not null) return Result<OrderDto>.Failure(tipError, 400);
            order.TipAmount = TipRules.Round(dto.Tip.Value);
            order.TipMethod = dto.TipMethod is { } m && m != dto.PaymentMethod ? m : null;
            order.TipForAdminId = user.UserId;
        }
        if (dto.HandedOver)
        {
            order.Status = OrderStatus.Completed;
            order.CompletedAt = soldAt;
        }

        // Keep what the ingredients cost today, for the profit report (prices change later).
        await new IngredientCostCalculator(db).SnapshotAsync(order, ct);
        db.Orders.Add(order);
        try
        {
            await db.SaveChangesAsync(ct);
        }
        catch (DbUpdateException)
        {
            // The same sale arrived twice at the same moment (the till retried); the first one won.
            db.Orders.Remove(order);
            var first = await LoadAsync(o => o.LocationId == dto.LocationId && o.CustomerUserId == null && o.IdempotencyKey == key, ct);
            if (first is null) throw;
            return Result<OrderDto>.Success(OrderService.MapToDto(first), 200);
        }
        order.Location = location;

        await audit.LogAsync("CounterSale", "Order", order.Id.ToString(),
            $"{order.Total:0.##} {dto.PaymentMethod}{(live ? "" : $", made offline at {soldAt:u}")}"
            + (order.TipAmount > 0 ? $" · tip {order.TipAmount:0.##}" : "")
            + (dto.Discount is { } d ? $" · discount -{order.DiscountAmount:0.##} ({ManualDiscount.Describe(d)}): {approval?.Describe() ?? d.Reason}" : ""), ct);

        // Stock goes down even if it was already at zero (it stays at zero): the goods are already sold.
        if (stock is not null) await stock.ConsumeForOrderAsync(order, ct);

        // The kitchen display and other tills see it at once; nobody is notified for a guest without an account.
        await hub.NotifyOrderCreatedAsync(order, ct);

        // Printing and the drawer only for a sale made just now. An offline sale was printed by the till itself.
        // Both never fail the sale.
        if (live && print is not null)
        {
            if (dto.HandedOver)
            {
                // A receipt that fails to queue can be printed again by hand from the order.
                try { await print.QueueReceiptAsync(order, autoOnly: true, ct); }
                catch (Exception ex) when (ex is not OperationCanceledException) { }
            }
            else await print.QueueForAcceptedOrderAsync(order, ct);
            if (dto.PaymentMethod == PaymentMethod.Cash) await print.OpenDrawerForCashAsync(order, ct);
        }

        return Result<OrderDto>.Success(OrderService.MapToDto(order), 201);
    }

    private Task<Order?> LoadAsync(System.Linq.Expressions.Expression<Func<Order, bool>> where, CancellationToken ct)
        => db.Orders
            .Include(o => o.Location)
            .Include(o => o.Items).ThenInclude(i => i.Toppings)
            .Include(o => o.ReadyStations)
            .FirstOrDefaultAsync(where, ct);
}
