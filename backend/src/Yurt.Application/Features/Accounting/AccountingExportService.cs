using System.Globalization;
using Microsoft.EntityFrameworkCore;
using Yurt.Application.Common.Interfaces;
using Yurt.Application.Common.Models;
using Yurt.Application.Features.Printing;
using Yurt.Application.Features.Shifts;
using Yurt.Domain.Entities;
using Yurt.Domain.Enums;

namespace Yurt.Application.Features.Accounting;

/// <summary>
/// Gathers what a cafe's accountant needs for a period: sales by day and payment method, every order, dishes sold,
/// cashier shifts, supplier deliveries and stock lost. The same data is then written as Excel
/// (<see cref="AccountingSpreadsheet"/>) or as a 1C exchange file (<see cref="OneCExchangeWriter"/>).
/// Days follow the cafe's local time (Asia/Almaty), like the other reports.
/// </summary>
public class AccountingExportService(
    IApplicationDbContext db, ITenantContext tenant, ShiftService shifts, IAuditLogService audit)
{
    private static readonly TimeZoneInfo CafeTime = TimeZoneInfo.FindSystemTimeZoneById("Asia/Almaty");

    /// <summary>A year and a day at most: enough for an annual report, small enough to build in one request.</summary>
    public const int MaxDays = 366;

    public static DateTime ToLocal(DateTime utc) => TimeZoneInfo.ConvertTimeFromUtc(DateTime.SpecifyKind(utc, DateTimeKind.Utc), CafeTime);

    /// <summary>Start of <paramref name="from"/> and end of <paramref name="to"/> in UTC.</summary>
    public static (DateTime Start, DateTime End) RangeUtc(DateOnly from, DateOnly to)
        => (TimeZoneInfo.ConvertTimeToUtc(from.ToDateTime(TimeOnly.MinValue), CafeTime),
            TimeZoneInfo.ConvertTimeToUtc(to.AddDays(1).ToDateTime(TimeOnly.MinValue), CafeTime));

    public static bool TryParseDay(string? text, out DateOnly day)
        => DateOnly.TryParseExact(text, "yyyy-MM-dd", CultureInfo.InvariantCulture, DateTimeStyles.None, out day);

    public async Task<Result<AccountingExport>> BuildAsync(
        string? fromText, string? toText, Guid? locationId, string format, CancellationToken ct = default)
    {
        if (!TryParseDay(fromText, out var from) || !TryParseDay(toText, out var to))
            return Result<AccountingExport>.Failure("Dates must look like 2026-11-01.", 422);
        if (to < from) return Result<AccountingExport>.Failure("The end date is before the start date.", 422);
        if (to.DayNumber - from.DayNumber + 1 > MaxDays)
            return Result<AccountingExport>.Failure($"Choose at most {MaxDays} days.", 422);

        string? locationName = null;
        if (locationId is { } loc)
        {
            locationName = await db.Locations.Where(l => l.Id == loc).Select(l => l.Name).FirstOrDefaultAsync(ct);
            if (locationName is null) return Result<AccountingExport>.NotFound("Location not found.");
        }

        var cafeName = await db.Tenants.Where(t => t.Id == tenant.TenantId).Select(t => t.Name).FirstOrDefaultAsync(ct) ?? "Cafe";
        var branches = await db.Locations.AsNoTracking().ToDictionaryAsync(l => l.Id, l => l.Name, ct);
        string Branch(Guid id) => branches.TryGetValue(id, out var n) ? n : "?";

        var (start, end) = RangeUtc(from, to);
        var orders = await OrdersAsync(start, end, locationId, Branch, ct);
        var export = new AccountingExport(
            cafeName, tenant.TenantId, from, to, locationId, locationName,
            SalesByDay(orders),
            orders,
            ItemsSold(orders),
            await ShiftsAsync(start, end, locationId, ct),
            await DeliveriesAsync(from, to, locationId, Branch, ct),
            await StockLossesAsync(start, end, locationId, Branch, ct));

        await audit.LogAsync("AccountingExported", "Accounting", null,
            $"{format} {from:yyyy-MM-dd}..{to:yyyy-MM-dd}{(locationName is null ? "" : " " + locationName)}", ct);
        return Result<AccountingExport>.Success(export);
    }

    // ── orders ──

    private async Task<List<OrderRow>> OrdersAsync(DateTime start, DateTime end, Guid? locationId, Func<Guid, string> branch, CancellationToken ct)
    {
        var orders = await db.Orders.AsNoTracking()
            .Where(o => o.Status == OrderStatus.Completed
                && (o.CompletedAt ?? o.CreatedAt) >= start && (o.CompletedAt ?? o.CreatedAt) < end
                && (locationId == null || o.LocationId == locationId))
            .OrderBy(o => o.CompletedAt ?? o.CreatedAt)
            .Select(o => new
            {
                o.Id, When = o.CompletedAt ?? o.CreatedAt, o.LocationId, o.TableLabel,
                o.Subtotal, o.DiscountAmount, o.Total, o.LoyaltyPointsSpent, o.PaymentMethod,
                Refunded = o.PaymentStatus == PaymentStatus.Refunded,
                Items = o.Items.OrderBy(i => i.CreatedAt).Select(i => new
                {
                    i.MenuItemId, i.VariantId, i.MenuItemName, i.VariantLabel, i.Quantity, i.LineTotal, i.UnitCost,
                }).ToList(),
            })
            .ToListAsync(ct);

        var ids = orders.Select(o => o.Id).ToList();
        var splits = new Dictionary<Guid, List<OrderPayment>>();
        var receipts = new Dictionary<Guid, FiscalReceipt>();
        // Ask in chunks so a year of orders never hits the database's limit on query parameters.
        foreach (var chunk in ids.Chunk(2000))
        {
            foreach (var g in (await db.OrderPayments.AsNoTracking().Where(p => chunk.Contains(p.OrderId)).ToListAsync(ct))
                         .GroupBy(p => p.OrderId))
                splits[g.Key] = g.ToList();
            foreach (var r in await db.FiscalReceipts.AsNoTracking()
                         .Where(r => chunk.Contains(r.OrderId) && r.Kind == FiscalReceiptKind.Sale).ToListAsync(ct))
                receipts[r.OrderId] = r;
        }

        return orders.Select(o =>
        {
            var points = o.LoyaltyPointsSpent ?? 0m;
            var paid = Math.Max(0m, o.Total - points);
            // The same rule as the shift report: split parts each count under their method; otherwise the order's
            // single method, and an order with no method recorded counts as cash.
            var payments = splits.TryGetValue(o.Id, out var parts)
                ? parts.OrderBy(p => p.CreatedAt).Select(p => (p.Method.ToString(), p.Amount)).ToList()
                : [((o.PaymentMethod ?? PaymentMethod.Cash).ToString(), paid)];
            if (splits.ContainsKey(o.Id)) paid = payments.Sum(p => p.Item2);

            // The discount code is shared out over the lines by their price; the last line takes the rounding.
            var share = o.Subtotal > 0 ? o.Total / o.Subtotal : 1m;
            var lines = new List<OrderLineRow>();
            var left = o.Total;
            for (var k = 0; k < o.Items.Count; k++)
            {
                var i = o.Items[k];
                var amount = k == o.Items.Count - 1 ? left : Math.Round(i.LineTotal * share, 2);
                left -= amount;
                lines.Add(new OrderLineRow(i.MenuItemId, i.VariantId, i.MenuItemName, i.VariantLabel, i.Quantity, amount, i.UnitCost));
            }

            receipts.TryGetValue(o.Id, out var receipt);
            return new OrderRow(
                o.Id, PrintLayouts.ShortId(o.Id), ToLocal(o.When), o.LocationId, branch(o.LocationId), o.TableLabel,
                o.Subtotal, o.DiscountAmount, points, paid, o.Refunded, payments,
                receipt?.CheckNumber, receipt?.Status.ToString(), lines);
        }).ToList();
    }

    private static List<SalesDayRow> SalesByDay(List<OrderRow> orders)
        => orders
            .GroupBy(o => (Day: DateOnly.FromDateTime(o.CompletedLocal), o.LocationId))
            .OrderBy(g => g.Key.Day).ThenBy(g => g.First().Branch)
            .Select(g =>
            {
                var paid = g.Sum(o => o.Paid);
                var refunds = g.Where(o => o.Refunded).Sum(o => o.Paid);
                var byMethod = g.SelectMany(o => o.Payments)
                    .GroupBy(p => p.Method)
                    .ToDictionary(m => m.Key, m => m.Sum(p => p.Amount));
                return new SalesDayRow(
                    g.Key.Day, g.Key.LocationId, g.First().Branch, g.Count(),
                    g.Sum(o => o.SalesBeforeDiscounts), g.Sum(o => o.Discount), g.Sum(o => o.Points),
                    paid, refunds, paid - refunds, byMethod);
            })
            .ToList();

    private static List<ItemSoldRow> ItemsSold(List<OrderRow> orders)
        => orders.Where(o => !o.Refunded)
            .SelectMany(o => o.Lines)
            .GroupBy(l => (l.MenuItemId, l.VariantId))
            .Select(g =>
            {
                var known = g.All(l => l.UnitCost is not null);
                return new ItemSoldRow(
                    g.Key.MenuItemId, g.Key.VariantId, g.Last().Name, g.Last().Size, g.Sum(l => l.Quantity),
                    g.Sum(l => l.Amount), known ? Math.Round(g.Sum(l => l.Quantity * l.UnitCost!.Value), 2) : null);
            })
            .OrderByDescending(i => i.Amount)
            .ToList();

    // ── shifts ──

    private async Task<List<ShiftRow>> ShiftsAsync(DateTime start, DateTime end, Guid? locationId, CancellationToken ct)
    {
        var ids = await db.Shifts.AsNoTracking()
            .Where(s => s.OpenedAt >= start && s.OpenedAt < end && (locationId == null || s.LocationId == locationId))
            .OrderBy(s => s.OpenedAt)
            .Select(s => s.Id)
            .ToListAsync(ct);
        var rows = new List<ShiftRow>();
        foreach (var id in ids)
        {
            // The Z report the cashier saw, worked out the same way.
            var report = await shifts.ReportAsync(id, ct);
            if (!report.Succeeded) continue;
            var r = report.Data!;
            rows.Add(new ShiftRow(
                r.Shift.LocationName, ToLocal(r.Shift.OpenedAt), r.Shift.OpenedBy,
                r.Shift.ClosedAt is { } c ? ToLocal(c) : null, r.Shift.ClosedBy,
                r.OrdersCompleted, r.Takings, r.Shift.OpeningCash, r.CashSales, r.CashIn, r.CashOut, r.ExpectedCash,
                r.Shift.CountedCash, r.Difference, r.Shift.FiscalZReportAt is not null));
        }
        return rows;
    }

    // ── stock ──

    private async Task<List<DeliveryRow>> DeliveriesAsync(DateOnly from, DateOnly to, Guid? locationId, Func<Guid, string> branch, CancellationToken ct)
    {
        // Purchase.Date is the delivery day kept as UTC midnight.
        var first = from.ToDateTime(TimeOnly.MinValue, DateTimeKind.Utc);
        var last = to.AddDays(1).ToDateTime(TimeOnly.MinValue, DateTimeKind.Utc);
        var purchases = await db.Purchases.AsNoTracking()
            .Where(p => p.Status == PurchaseStatuses.Posted && p.Date >= first && p.Date < last
                && (locationId == null || p.LocationId == locationId))
            .OrderBy(p => p.Date).ThenBy(p => p.PostedAt)
            .Select(p => new
            {
                p.Id, p.Date, p.Number, p.SupplierId, Supplier = p.Supplier.Name, p.Supplier.Bin, p.LocationId,
                p.Total, p.IsPaid, p.PaidAt,
                Lines = p.Lines.OrderBy(l => l.SortOrder).Select(l => new { l.IngredientId, l.IngredientName, l.Unit, l.Quantity, l.Total }).ToList(),
            })
            .ToListAsync(ct);
        return purchases.SelectMany(p => p.Lines.Select(l => new DeliveryRow(
                p.Id, DateOnly.FromDateTime(p.Date), p.Number, p.SupplierId, p.Supplier, p.Bin, p.LocationId, branch(p.LocationId),
                l.IngredientId, l.IngredientName, l.Unit, l.Quantity, l.Total, p.Total, p.IsPaid,
                p.PaidAt is { } paid ? ToLocal(paid) : null)))
            .ToList();
    }

    private async Task<List<StockLossRow>> StockLossesAsync(DateTime start, DateTime end, Guid? locationId, Func<Guid, string> branch, CancellationToken ct)
    {
        var changes = await db.StockChanges.AsNoTracking()
            .Where(c => (c.Type == StockChangeTypes.WriteOff || c.Type == StockChangeTypes.Stocktake)
                && c.CreatedAt >= start && c.CreatedAt < end
                && (locationId == null || c.LocationId == locationId))
            .OrderBy(c => c.CreatedAt)
            .ToListAsync(ct);
        return changes
            .Where(c => c.OldQuantity is not null && c.NewQuantity is not null && c.OldQuantity != c.NewQuantity)
            .Select(c =>
            {
                var lost = c.OldQuantity!.Value - c.NewQuantity!.Value;
                var type = c.Type == StockChangeTypes.WriteOff ? StockLossTypes.WriteOff
                    : lost > 0 ? StockLossTypes.StocktakeShortage : StockLossTypes.StocktakeSurplus;
                var local = ToLocal(c.CreatedAt);
                return new StockLossRow(
                    DateOnly.FromDateTime(local), local, c.LocationId, branch(c.LocationId), c.Kind, c.ItemId, c.ItemName, c.Unit,
                    type, c.Reason, c.Note, lost, c.UnitCost, c.UnitCost is { } u ? Math.Round(lost * u, 2) : null, c.ChangedBy);
            })
            .ToList();
    }
}
