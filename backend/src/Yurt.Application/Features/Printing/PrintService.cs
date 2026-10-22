using System.Linq.Expressions;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;
using Yurt.Application.Common.Interfaces;
using Yurt.Application.Common.Models;
using Yurt.Application.Features.Fiscal;
using Yurt.Application.Features.Platform;
using Yurt.Domain.Entities;

namespace Yurt.Application.Features.Printing;

/// <summary>
/// Puts kitchen tickets and receipts in the print queue, and sends queued jobs to the printers (Phase 6, <c>own_pos</c>).
/// Printing never blocks an order: a printer that is off only leaves a failed job in the list, and staff can still
/// print from the browser.
/// </summary>
public class PrintService(
    IApplicationDbContext db, IPrinterTransport transport, PrintQueueSignal signal, ILogger<PrintService> logger,
    IAuditLogService audit)
{
    /// <summary>Tries per job before it is marked failed.</summary>
    public const int MaxAttempts = 3;

    /// <summary>A ticket that could not print within this time is dropped: printing it much later would only confuse the kitchen.</summary>
    public static readonly TimeSpan MaxAge = TimeSpan.FromMinutes(15);

    /// <summary>
    /// A cash drawer that could not open within this time stays shut: a drawer popping open minutes later, when nobody
    /// is at the till, is worse than the cashier pressing Open drawer again.
    /// </summary>
    public static readonly TimeSpan DrawerMaxAge = TimeSpan.FromMinutes(1);

    /// <summary>A job left "sending" this long (the server stopped mid-send) is marked failed.</summary>
    private static readonly TimeSpan StuckAfter = TimeSpan.FromMinutes(2);

    private static readonly TimeSpan KeepFor = TimeSpan.FromDays(7);

    private const int BatchSize = 20;

    /// <summary>Called when an order is accepted: queues tickets and a receipt on every printer of the branch set to print by itself.</summary>
    public async Task QueueForAcceptedOrderAsync(Order order, CancellationToken ct = default)
    {
        try
        {
            await QueueTicketsAsync(order, null, autoOnly: true, ct);
            await QueueReceiptAsync(order, autoOnly: true, ct);
        }
        catch (Exception ex) when (ex is not OperationCanceledException)
        {
            logger.LogError(ex, "Could not queue print jobs for order {OrderId}", order.Id);
        }
    }

    /// <summary>Staff pressed Print ticket. With a station, only that station's printers; without, every kitchen printer.</summary>
    public async Task<Result<PrintQueuedDto>> PrintTicketAsync(Guid orderId, Guid? stationId, CancellationToken ct = default)
    {
        var order = await LoadAsync(orderId, ct);
        if (order is null) return Result<PrintQueuedDto>.NotFound();
        return Result<PrintQueuedDto>.Success(new PrintQueuedDto(await QueueTicketsAsync(order, stationId, autoOnly: false, ct)));
    }

    /// <summary>Staff pressed Print receipt: the receipt goes to every receipt printer of the order's branch.</summary>
    public async Task<Result<PrintQueuedDto>> PrintReceiptAsync(Guid orderId, CancellationToken ct = default)
    {
        var order = await LoadAsync(orderId, ct);
        if (order is null) return Result<PrintQueuedDto>.NotFound();
        return Result<PrintQueuedDto>.Success(new PrintQueuedDto(await QueueReceiptAsync(order, autoOnly: false, ct)));
    }

    /// <summary>
    /// A cash payment was taken for this order: opens the cash drawer of the order's branch. Never fails the payment;
    /// a drawer that does not open only leaves a failed job in the list.
    /// </summary>
    public async Task OpenDrawerForCashAsync(Order order, CancellationToken ct = default)
    {
        try
        {
            await QueueDrawerAsync(order.LocationId, order.Id, ct);
        }
        catch (Exception ex) when (ex is not OperationCanceledException)
        {
            logger.LogError(ex, "Could not open the cash drawer for order {OrderId}", order.Id);
        }
    }

    /// <summary>The cashier pressed Open drawer (for change, cash in/out or counting). Logged in the audit log.</summary>
    public async Task<Result<PrintQueuedDto>> OpenDrawerAsync(Guid locationId, CancellationToken ct = default)
    {
        if (!await db.Locations.AnyAsync(l => l.Id == locationId, ct)) return Result<PrintQueuedDto>.NotFound("Branch not found.");
        var queued = await QueueDrawerAsync(locationId, null, ct);
        if (queued > 0) await audit.LogAsync("CashDrawerOpened", "Location", locationId.ToString(), null, ct);
        return Result<PrintQueuedDto>.Success(new PrintQueuedDto(queued));
    }

    /// <summary>Sends "open drawer" to every receipt printer of the branch that has a cash drawer plugged in.</summary>
    private async Task<int> QueueDrawerAsync(Guid locationId, Guid? orderId, CancellationToken ct)
    {
        var printers = await db.Printers.AsNoTracking()
            .Where(p => p.LocationId == locationId && p.Kind == Printer.ReceiptKind && p.CashDrawer)
            .ToListAsync(ct);
        foreach (var printer in printers)
            db.PrintJobs.Add(new PrintJob { PrinterId = printer.Id, OrderId = orderId, Kind = PrintJob.DrawerKind, Data = PrintLayouts.Drawer(printer) });
        return await SaveAsync(printers.Count, ct);
    }

    /// <summary>
    /// Kitchen printers get the items of their station plus items with no station (the same items the kitchen screen
    /// shows for that station); a printer with no station gets every item. A printer with nothing to make gets nothing.
    /// </summary>
    public async Task<int> QueueTicketsAsync(Order order, Guid? stationId, bool autoOnly, CancellationToken ct = default)
    {
        var printers = await db.Printers.AsNoTracking()
            .Where(p => p.LocationId == order.LocationId && p.Kind == Printer.KitchenKind && (!autoOnly || p.AutoPrint))
            .Where(p => stationId == null || p.StationId == stationId)
            .ToListAsync(ct);
        if (printers.Count == 0) return 0;

        var menuItemIds = order.Items.Select(i => i.MenuItemId).Distinct().ToList();
        var itemStations = await db.MenuItems.Where(m => menuItemIds.Contains(m.Id) && m.Category.StationId != null)
            .Select(m => new { m.Id, StationId = m.Category.StationId!.Value }).ToDictionaryAsync(m => m.Id, m => m.StationId, ct);
        var stationNames = await db.Stations.ToDictionaryAsync(s => s.Id, s => s.Name, ct);

        var queued = 0;
        foreach (var printer in printers)
        {
            var items = printer.StationId is { } sid
                ? order.Items.Where(i => !itemStations.TryGetValue(i.MenuItemId, out var s) || s == sid).ToList()
                : order.Items.ToList();
            if (items.Count == 0) continue;
            var station = printer.StationId is { } id ? stationNames.GetValueOrDefault(id) : null;
            Add(printer, order.Id, PrintJob.TicketKind, PrintLayouts.Ticket(printer, order, items, station));
            queued++;
        }
        return await SaveAsync(queued, ct);
    }

    /// <summary>
    /// The order's official tax receipt was just sent: prints the receipt again, now with its QR code, on the receipt
    /// printers of the branch that have "print again with the tax QR code" ticked. Never fails the tax receipt.
    /// </summary>
    public async Task ReprintWithTaxQrAsync(Guid orderId, CancellationToken ct = default)
    {
        try
        {
            var order = await LoadAsync(orderId, ct);
            if (order is not null) await QueueReceiptAsync(order, p => p.ReprintWithTaxQr, ct);
        }
        catch (Exception ex) when (ex is not OperationCanceledException)
        {
            logger.LogError(ex, "Could not queue the receipt with the tax QR code for order {OrderId}", orderId);
        }
    }

    public Task<int> QueueReceiptAsync(Order order, bool autoOnly, CancellationToken ct = default)
        => QueueReceiptAsync(order, p => !autoOnly || p.AutoPrint, ct);

    private async Task<int> QueueReceiptAsync(Order order, Expression<Func<Printer, bool>> which, CancellationToken ct)
    {
        var printers = await db.Printers.AsNoTracking()
            .Where(p => p.LocationId == order.LocationId && p.Kind == Printer.ReceiptKind)
            .Where(which)
            .ToListAsync(ct);
        if (printers.Count == 0) return 0;

        var brand = (await BrandReader.ReadAsync(db, ct)).Name;
        var split = await db.OrderPayments.AsNoTracking().Where(p => p.OrderId == order.Id).OrderBy(p => p.CreatedAt).ToListAsync(ct);
        // The tax receipt usually goes out a minute after the order is accepted, so the first auto-printed receipt
        // often has no QR code yet; printing the receipt again later adds it (by hand, or by itself with ReprintWithTaxQr).
        var fiscal = await db.FiscalReceipts.AsNoTracking()
            .FirstOrDefaultAsync(r => r.OrderId == order.Id && r.Kind == FiscalReceiptKind.Sale && r.Status == FiscalReceiptStatus.Sent, ct);
        if (fiscal is not null) fiscal.TicketUrl = FiscalLinks.Safe(fiscal.TicketUrl);
        foreach (var printer in printers)
            Add(printer, order.Id, PrintJob.ReceiptKind, PrintLayouts.Receipt(printer, order, brand, split, fiscal));
        return await SaveAsync(printers.Count, ct);
    }

    /// <summary>
    /// Sends waiting jobs of the current cafe to their printers, oldest first. A job is claimed before it is sent, so two
    /// senders never print it twice. A failed send is tried again on the next run, up to <see cref="MaxAttempts"/> times.
    /// </summary>
    public async Task<int> DispatchPendingAsync(CancellationToken ct = default)
    {
        var now = DateTime.UtcNow;
        var tooOld = now - MaxAge;
        var stuck = now - StuckAfter;
        var drawerTooOld = now - DrawerMaxAge;
        await db.PrintJobs.Where(j => j.Status == PrintJob.Pending && j.Kind == PrintJob.DrawerKind && j.CreatedAt < drawerTooOld)
            .ExecuteUpdateAsync(s => s.SetProperty(j => j.Status, PrintJob.Failed)
                .SetProperty(j => j.LastError, "Drawer not opened in time.").SetProperty(j => j.UpdatedAt, now), ct);
        await db.PrintJobs.Where(j => j.Status == PrintJob.Pending && j.CreatedAt < tooOld)
            .ExecuteUpdateAsync(s => s.SetProperty(j => j.Status, PrintJob.Failed)
                .SetProperty(j => j.LastError, "Not printed in time.").SetProperty(j => j.UpdatedAt, now), ct);
        await db.PrintJobs.Where(j => j.Status == PrintJob.Sending && j.UpdatedAt < stuck)
            .ExecuteUpdateAsync(s => s.SetProperty(j => j.Status, PrintJob.Failed)
                .SetProperty(j => j.LastError, "Sending stopped half way.").SetProperty(j => j.UpdatedAt, now), ct);

        // Finished jobs are kept a week for the list on the Printers page, then removed so the bytes do not pile up.
        var keepFrom = now - KeepFor;
        await db.PrintJobs.Where(j => (j.Status == PrintJob.Printed || j.Status == PrintJob.Failed) && j.CreatedAt < keepFrom)
            .ExecuteDeleteAsync(ct);

        // Jobs for printers behind a print agent wait for the agent to fetch them (PrintAgentService), and jobs for
        // Bluetooth printers wait for the tablet paired with them (BluetoothPrintService).
        var jobs = await db.PrintJobs.AsNoTracking().Where(j => j.Status == PrintJob.Pending && !j.Printer.ViaAgent && !j.Printer.ViaBluetooth)
            .OrderBy(j => j.CreatedAt).Take(BatchSize)
            .Select(j => new { j.Id, j.Data, j.Attempts, j.Printer.Host, j.Printer.Port })
            .ToListAsync(ct);

        var printed = 0;
        foreach (var job in jobs)
        {
            var claimed = await db.PrintJobs.Where(j => j.Id == job.Id && j.Status == PrintJob.Pending)
                .ExecuteUpdateAsync(s => s.SetProperty(j => j.Status, PrintJob.Sending)
                    .SetProperty(j => j.Attempts, j => j.Attempts + 1).SetProperty(j => j.UpdatedAt, DateTime.UtcNow), ct);
            if (claimed == 0) continue;

            string status;
            string? error = null;
            try
            {
                await transport.SendAsync(job.Host, job.Port, job.Data, ct);
                status = PrintJob.Printed;
                printed++;
            }
            catch (Exception ex) when (ex is not OperationCanceledException || !ct.IsCancellationRequested)
            {
                status = StatusAfter(false, job.Attempts + 1);
                error = Short(ex);
                logger.LogWarning("Print job {JobId} to {Host}:{Port} failed: {Error}", job.Id, job.Host, job.Port, error);
            }

            var done = status == PrintJob.Printed ? DateTime.UtcNow : (DateTime?)null;
            await db.PrintJobs.Where(j => j.Id == job.Id)
                .ExecuteUpdateAsync(s => s.SetProperty(j => j.Status, status).SetProperty(j => j.LastError, error)
                    .SetProperty(j => j.PrintedAt, done).SetProperty(j => j.UpdatedAt, DateTime.UtcNow), CancellationToken.None);
        }
        return printed;
    }

    /// <summary>The state a job moves to after a send: printed, or tried again until <see cref="MaxAttempts"/>.</summary>
    internal static string StatusAfter(bool ok, int attemptsSoFar)
        => ok ? PrintJob.Printed : attemptsSoFar >= MaxAttempts ? PrintJob.Failed : PrintJob.Pending;

    internal static string Short(Exception ex) => Short(ex.Message);

    internal static string Short(string message) => message.Length <= 300 ? message : message[..300];

    private void Add(Printer printer, Guid orderId, string kind, byte[] data)
        => db.PrintJobs.Add(new PrintJob { PrinterId = printer.Id, OrderId = orderId, Kind = kind, Data = data });

    private async Task<int> SaveAsync(int queued, CancellationToken ct)
    {
        if (queued == 0) return 0;
        await db.SaveChangesAsync(ct);
        signal.Notify();
        return queued;
    }

    private Task<Order?> LoadAsync(Guid id, CancellationToken ct)
        => db.Orders.AsNoTracking()
            .Include(o => o.Location)
            .Include(o => o.CustomerUser)
            .Include(o => o.Items).ThenInclude(i => i.Toppings)
            .Include(o => o.DiscountCode)
            .FirstOrDefaultAsync(o => o.Id == id, ct);
}
