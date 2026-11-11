using System.Globalization;
using Microsoft.EntityFrameworkCore;
using Yurt.Application.Common.Interfaces;
using Yurt.Application.Common.Models;
using Yurt.Application.Features.Payroll;
using Yurt.Application.Features.Stock;
using Yurt.Domain;
using Yurt.Domain.Entities;
using Yurt.Domain.Enums;

namespace Yurt.Application.Features.Profit;

/// <summary>
/// Profit reports for the CEO: what each dish earns after its ingredients, and the month's profit after ingredients, stock
/// lost (write-offs, stocktake shortages), wages (payroll module) and the other costs the CEO types in (rent, utilities...).
/// Months follow the cafe's local time (Asia/Almaty), like the rest of the reports.
/// </summary>
public class ProfitService(
    IApplicationDbContext db, IModuleGate modules, PayrollService payroll, ICurrentUserService user, IAuditLogService audit)
{
    private static readonly TimeZoneInfo CafeTime = TimeZoneInfo.FindSystemTimeZoneById("Asia/Almaty");
    private const int MaxExpensesPerMonth = 200;

    private string Who => user.Username ?? "unknown";

    public static bool IsValidMonth(string? month) => PayrollService.IsValidMonth(month);

    /// <summary>The month's start and end in UTC, in the cafe's local time.</summary>
    public static (DateTime Start, DateTime End) MonthRangeUtc(string month)
    {
        var first = DateTime.ParseExact(month + "-01", "yyyy-MM-dd", CultureInfo.InvariantCulture);
        return (TimeZoneInfo.ConvertTimeToUtc(first, CafeTime), TimeZoneInfo.ConvertTimeToUtc(first.AddMonths(1), CafeTime));
    }

    public static string PreviousMonth(string month)
        => DateTime.ParseExact(month + "-01", "yyyy-MM-dd", CultureInfo.InvariantCulture).AddMonths(-1)
            .ToString("yyyy-MM", CultureInfo.InvariantCulture);

    // ── sales ──

    private sealed record SoldLine(
        Guid MenuItemId, string Name, Guid? VariantId, string? Size, int Quantity, decimal Revenue, bool Refunded,
        decimal? UnitCost, bool Estimated, bool MissingPrice);

    private sealed record SoldOrder(decimal Subtotal, decimal Total, bool Refunded);

    /// <summary>Completed orders of the month with their lines, each line's cost (saved, or estimated at today's costs).</summary>
    private async Task<(List<SoldOrder> Orders, List<SoldLine> Lines)> SalesAsync(string month, Guid? locationId, CancellationToken ct)
    {
        var (start, end) = MonthRangeUtc(month);
        var orders = await db.Orders.AsNoTracking()
            .Where(o => o.Status == OrderStatus.Completed
                && (o.CompletedAt ?? o.CreatedAt) >= start && (o.CompletedAt ?? o.CreatedAt) < end
                && (locationId == null || o.LocationId == locationId))
            .Select(o => new
            {
                o.Subtotal, o.Total, Refunded = o.PaymentStatus == PaymentStatus.Refunded,
                Items = o.Items.Select(i => new
                {
                    i.MenuItemId, i.MenuItemName, i.VariantId, i.VariantLabel, i.Quantity, i.LineTotal, i.UnitCost,
                    Toppings = i.Toppings.Select(t => t.ToppingId).ToList(),
                }).ToList(),
            })
            .ToListAsync(ct);

        // Lines sold before costs were saved on each sale: price them at today's costs once, all together.
        var unknown = orders.SelectMany(o => o.Items).Where(i => i.UnitCost is null).ToList();
        var estimates = await new IngredientCostCalculator(db).ForLinesAsync(
            unknown.Select(i => new StockLine(i.MenuItemId, i.VariantId, i.Quantity, i.Toppings)).ToList(), ct);
        var estimateOf = new Dictionary<object, PortionCost>(ReferenceEqualityComparer.Instance);
        for (var k = 0; k < unknown.Count; k++) estimateOf[unknown[k]] = estimates[k];

        var lines = new List<SoldLine>();
        foreach (var o in orders)
        {
            // The order's discount (codes, loyalty points) is shared out over its lines by their price.
            var share = o.Subtotal > 0 ? o.Total / o.Subtotal : 1m;
            foreach (var i in o.Items)
            {
                var estimated = estimateOf.TryGetValue(i, out var estimate);
                lines.Add(new SoldLine(
                    i.MenuItemId, i.MenuItemName, i.VariantId, i.VariantLabel, i.Quantity, i.LineTotal * share, o.Refunded,
                    estimated ? estimate!.UnitCost : i.UnitCost, estimated && estimate!.UnitCost is not null,
                    estimated && estimate!.MissingPrice));
            }
        }
        return (orders.Select(o => new SoldOrder(o.Subtotal, o.Total, o.Refunded)).ToList(), lines);
    }

    /// <summary>Every dish and size sold in the month, most profit first.</summary>
    public async Task<Result<ItemProfitReportDto>> ItemsAsync(string month, Guid? locationId, CancellationToken ct = default)
    {
        if (!IsValidMonth(month)) return Result<ItemProfitReportDto>.Failure("Month must look like 2026-11.", 422);
        var (_, lines) = await SalesAsync(month, locationId, ct);

        var items = lines
            .Where(l => !l.Refunded)
            .GroupBy(l => (l.MenuItemId, l.VariantId))
            .Select(g =>
            {
                var sold = g.Sum(l => l.Quantity);
                var revenue = Math.Round(g.Sum(l => l.Revenue), 2);
                var known = g.All(l => l.UnitCost is not null);
                decimal? cost = known ? Math.Round(g.Sum(l => l.Quantity * l.UnitCost!.Value), 2) : null;
                decimal? profit = cost is { } c ? revenue - c : null;
                return new ItemProfitDto(
                    g.Key.MenuItemId, g.Last().Name, g.Key.VariantId, g.Last().Size, sold, revenue,
                    cost, profit,
                    cost is { } c2 && revenue > 0 ? Math.Round(c2 / revenue * 100m, 1) : null,
                    profit is { } p && sold > 0 ? Math.Round(p / sold, 2) : null,
                    g.Where(l => l.Estimated).Sum(l => l.Quantity),
                    g.Any(l => l.MissingPrice));
            })
            .OrderByDescending(i => i.Profit ?? decimal.MinValue).ThenByDescending(i => i.Revenue)
            .ToList();
        return Result<ItemProfitReportDto>.Success(new ItemProfitReportDto(month, locationId, items));
    }

    // ── month ──

    public async Task<Result<ProfitMonthDto>> MonthAsync(string month, Guid? locationId, CancellationToken ct = default)
    {
        if (!IsValidMonth(month)) return Result<ProfitMonthDto>.Failure("Month must look like 2026-11.", 422);
        if (locationId is { } loc && !await db.Locations.AnyAsync(l => l.Id == loc, ct))
            return Result<ProfitMonthDto>.NotFound("Location not found.");

        var report = await BuildMonthAsync(month, locationId, null, ct);
        var previous = await BuildMonthAsync(PreviousMonth(month), locationId, null, ct);
        return Result<ProfitMonthDto>.Success(report with
        {
            Previous = new ProfitTotalsDto(previous.NetSales, previous.IngredientCost, previous.GrossProfit,
                previous.GrossProfit - previous.Profit, previous.Profit),
        });
    }

    private async Task<ProfitMonthDto> BuildMonthAsync(string month, Guid? locationId, ProfitTotalsDto? previous, CancellationToken ct)
    {
        var (orders, lines) = await SalesAsync(month, locationId, ct);
        var sales = orders.Sum(o => o.Subtotal);
        var discounts = orders.Sum(o => Math.Max(0m, o.Subtotal - o.Total));
        var refunds = orders.Where(o => o.Refunded).Sum(o => o.Total);
        var netSales = sales - discounts - refunds;
        // A refunded order's drinks were still made: their ingredients stay in the cost.
        var ingredientCost = Math.Round(lines.Sum(l => l.Quantity * (l.UnitCost ?? 0m)), 2);
        var grossProfit = netSales - ingredientCost;

        var (start, end) = MonthRangeUtc(month);
        var stock = await db.StockChanges.AsNoTracking()
            .Where(c => (c.Type == StockChangeTypes.WriteOff || c.Type == StockChangeTypes.Stocktake)
                && c.CreatedAt >= start && c.CreatedAt < end
                && (locationId == null || c.LocationId == locationId))
            .Select(c => new { c.Type, c.Reason, c.OldQuantity, c.NewQuantity, c.UnitCost })
            .ToListAsync(ct);
        static decimal Lost(decimal? oldQty, decimal? newQty, decimal? unitCost)
            => unitCost is { } c && oldQty is { } o && newQty is { } n ? (o - n) * c : 0m;
        var writeOffsByReason = stock.Where(c => c.Type == StockChangeTypes.WriteOff)
            .GroupBy(c => c.Reason ?? WriteOffReasons.Other)
            .Select(g => new WriteOffReasonCostDto(g.Key, Math.Round(g.Sum(c => Lost(c.OldQuantity, c.NewQuantity, c.UnitCost)), 2)))
            .OrderByDescending(r => r.Cost)
            .ToList();
        var writeOffs = writeOffsByReason.Sum(r => r.Cost);
        var stocktakeLoss = Math.Round(stock.Where(c => c.Type == StockChangeTypes.Stocktake)
            .Sum(c => Lost(c.OldQuantity, c.NewQuantity, c.UnitCost)), 2);

        // Wages are for the whole cafe: shown only when the report covers every branch.
        decimal? payrollTotal = null;
        var payrollClosed = false;
        if (locationId is null && await modules.IsEnabledAsync(ModuleKeys.Payroll, ct))
        {
            var sheet = await payroll.GetMonthAsync(month, ct);
            if (sheet.Succeeded && sheet.Data is { } s)
            {
                payrollTotal = s.TotalToPay;
                payrollClosed = s.Closed;
            }
        }

        var expenses = await ExpenseRowsAsync(month, locationId, ct);
        var expensesTotal = expenses.Sum(e => e.Amount);

        var profit = grossProfit - writeOffs - stocktakeLoss - (payrollTotal ?? 0m) - expensesTotal;
        return new ProfitMonthDto(
            month, locationId, orders.Count,
            sales, discounts, refunds, netSales,
            ingredientCost,
            lines.Count(l => l.Estimated),
            lines.Count(l => l.UnitCost is null),
            grossProfit, writeOffs, writeOffsByReason, stocktakeLoss,
            payrollTotal, payrollClosed,
            expenses, expensesTotal, profit,
            previous ?? new ProfitTotalsDto(0, 0, 0, 0, 0));
    }

    // ── other costs (rent, utilities...) ──

    private async Task<List<CafeExpenseDto>> ExpenseRowsAsync(string month, Guid? locationId, CancellationToken ct)
        => await db.CafeExpenses.AsNoTracking()
            .Where(e => e.Month == month && (locationId == null || e.LocationId == locationId))
            .OrderBy(e => e.Category).ThenBy(e => e.Name)
            .Select(e => new CafeExpenseDto(e.Id, e.Month, e.LocationId, e.Location != null ? e.Location.Name : null, e.Category, e.Name, e.Amount))
            .ToListAsync(ct);

    public async Task<Result<List<CafeExpenseDto>>> ExpensesAsync(string month, CancellationToken ct = default)
    {
        if (!IsValidMonth(month)) return Result<List<CafeExpenseDto>>.Failure("Month must look like 2026-11.", 422);
        return Result<List<CafeExpenseDto>>.Success(await ExpenseRowsAsync(month, null, ct));
    }

    private async Task<string?> ExpenseErrorAsync(SaveCafeExpenseDto dto, CancellationToken ct)
    {
        if (!IsValidMonth(dto.Month)) return "Month must look like 2026-11.";
        if (!CafeExpenseCategories.All.Contains(dto.Category)) return "Choose what kind of cost it is.";
        if (string.IsNullOrWhiteSpace(dto.Name) || dto.Name.Trim().Length > 100) return "Name the cost (up to 100 characters).";
        if (dto.Amount <= 0 || dto.Amount > 1_000_000_000) return "Amount must be more than 0.";
        if (dto.LocationId is { } loc && !await db.Locations.AnyAsync(l => l.Id == loc, ct)) return "Location not found.";
        return null;
    }

    public async Task<Result<CafeExpenseDto>> AddExpenseAsync(SaveCafeExpenseDto dto, CancellationToken ct = default)
    {
        if (await ExpenseErrorAsync(dto, ct) is { } error) return Result<CafeExpenseDto>.Failure(error, 422);
        if (await db.CafeExpenses.CountAsync(e => e.Month == dto.Month, ct) >= MaxExpensesPerMonth)
            return Result<CafeExpenseDto>.Failure("Too many costs in one month.", 422);
        var row = new CafeExpense
        {
            Month = dto.Month, LocationId = dto.LocationId, Category = dto.Category, Name = dto.Name.Trim(),
            Amount = Math.Round(dto.Amount, 2), CreatedBy = Who,
        };
        db.CafeExpenses.Add(row);
        await db.SaveChangesAsync(ct);
        await audit.LogAsync("ExpenseAdded", "CafeExpense", row.Id.ToString(), $"{row.Month} {row.Name}: {row.Amount.ToString("0.##", CultureInfo.InvariantCulture)}", ct);
        return Result<CafeExpenseDto>.Success(await OneAsync(row.Id, ct));
    }

    public async Task<Result<CafeExpenseDto>> UpdateExpenseAsync(Guid id, SaveCafeExpenseDto dto, CancellationToken ct = default)
    {
        var row = await db.CafeExpenses.FirstOrDefaultAsync(e => e.Id == id, ct);
        if (row is null) return Result<CafeExpenseDto>.NotFound("Cost not found.");
        if (await ExpenseErrorAsync(dto, ct) is { } error) return Result<CafeExpenseDto>.Failure(error, 422);
        row.Month = dto.Month;
        row.LocationId = dto.LocationId;
        row.Category = dto.Category;
        row.Name = dto.Name.Trim();
        row.Amount = Math.Round(dto.Amount, 2);
        row.UpdatedAt = DateTime.UtcNow;
        await db.SaveChangesAsync(ct);
        await audit.LogAsync("ExpenseChanged", "CafeExpense", row.Id.ToString(), $"{row.Month} {row.Name}: {row.Amount.ToString("0.##", CultureInfo.InvariantCulture)}", ct);
        return Result<CafeExpenseDto>.Success(await OneAsync(row.Id, ct));
    }

    public async Task<Result<bool>> DeleteExpenseAsync(Guid id, CancellationToken ct = default)
    {
        var row = await db.CafeExpenses.FirstOrDefaultAsync(e => e.Id == id, ct);
        if (row is null) return Result<bool>.NotFound("Cost not found.");
        db.CafeExpenses.Remove(row);
        await db.SaveChangesAsync(ct);
        await audit.LogAsync("ExpenseDeleted", "CafeExpense", row.Id.ToString(), $"{row.Month} {row.Name}", ct);
        return Result<bool>.Success(true);
    }

    /// <summary>Fills an empty month with last month's costs (rent and utilities rarely change), to edit from there.</summary>
    public async Task<Result<List<CafeExpenseDto>>> CopyPreviousAsync(string month, CancellationToken ct = default)
    {
        if (!IsValidMonth(month)) return Result<List<CafeExpenseDto>>.Failure("Month must look like 2026-11.", 422);
        if (await db.CafeExpenses.AnyAsync(e => e.Month == month, ct))
            return Result<List<CafeExpenseDto>>.Failure("This month already has costs.", 409);
        var from = PreviousMonth(month);
        var rows = await db.CafeExpenses.AsNoTracking().Where(e => e.Month == from).ToListAsync(ct);
        if (rows.Count == 0) return Result<List<CafeExpenseDto>>.Failure("Last month has no costs to copy.", 404);
        foreach (var r in rows)
            db.CafeExpenses.Add(new CafeExpense
            {
                Month = month, LocationId = r.LocationId, Category = r.Category, Name = r.Name, Amount = r.Amount, CreatedBy = Who,
            });
        await db.SaveChangesAsync(ct);
        await audit.LogAsync("ExpensesCopied", "CafeExpense", null, $"{from} → {month}: {rows.Count}", ct);
        return Result<List<CafeExpenseDto>>.Success(await ExpenseRowsAsync(month, null, ct));
    }

    private async Task<CafeExpenseDto> OneAsync(Guid id, CancellationToken ct)
        => await db.CafeExpenses.AsNoTracking().Where(e => e.Id == id)
            .Select(e => new CafeExpenseDto(e.Id, e.Month, e.LocationId, e.Location != null ? e.Location.Name : null, e.Category, e.Name, e.Amount))
            .FirstAsync(ct);
}
