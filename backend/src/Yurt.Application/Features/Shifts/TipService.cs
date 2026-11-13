using System.Globalization;
using Microsoft.EntityFrameworkCore;
using Yurt.Application.Common.Interfaces;
using Yurt.Application.Common.Models;
using Yurt.Domain.Enums;

namespace Yurt.Application.Features.Shifts;

public record ShiftTipDto(Guid ShiftId, Guid LocationId, string LocationName, DateTime OpenedAt, DateTime? ClosedAt, decimal Total);

/// <param name="From">First day (inclusive), <c>yyyy-MM-dd</c>.</param>
/// <param name="To">Last day (inclusive), <c>yyyy-MM-dd</c>.</param>
public record TipReportDto(
    string From, string To, int Orders, decimal Total, decimal Cash, decimal NonCash,
    List<StaffTipDto> ByStaff, List<ShiftTipDto> ByShift);

/// <summary>Who got how many tips, for a period and per cashier shift (own_pos). Tips are staff money: CEO only.</summary>
public class TipService(IApplicationDbContext db)
{
    public const int MaxDays = 366;

    public async Task<Result<TipReportDto>> ReportAsync(string? from, string? to, Guid? locationId, CancellationToken ct = default)
    {
        if (!TryDay(from, out var start) || !TryDay(to, out var end))
            return Result<TipReportDto>.Failure("Give the dates as yyyy-MM-dd.", 400);
        if (end < start) return Result<TipReportDto>.Failure("The end date is before the start date.", 400);
        if ((end - start).TotalDays >= MaxDays) return Result<TipReportDto>.Failure("Pick at most a year.", 400);
        var until = end.AddDays(1);

        var orders = await db.Orders
            .Where(o => o.Status == OrderStatus.Completed && o.TipAmount > 0 && o.CompletedAt >= start && o.CompletedAt < until
                && (locationId == null || o.LocationId == locationId))
            .Select(o => new { o.LocationId, o.CompletedAt, o.PaymentMethod, o.TipMethod, o.TipAmount, o.TipForAdminId })
            .ToListAsync(ct);

        static bool IsCash(PaymentMethod? tip, PaymentMethod? bill) => (tip ?? bill ?? PaymentMethod.Cash) == PaymentMethod.Cash;
        var staffIds = orders.Where(o => o.TipForAdminId != null).Select(o => o.TipForAdminId!.Value).Distinct().ToList();
        var names = staffIds.Count == 0 ? [] : await db.AdminUsers.Where(u => staffIds.Contains(u.Id)).ToDictionaryAsync(u => u.Id, u => u.Username, ct);

        var byStaff = orders.GroupBy(o => o.TipForAdminId)
            .Select(g => new StaffTipDto(g.Key, g.Key is { } id && names.TryGetValue(id, out var n) ? n : "", g.Count(), g.Sum(o => o.TipAmount),
                g.Where(o => IsCash(o.TipMethod, o.PaymentMethod)).Sum(o => o.TipAmount),
                g.Where(o => !IsCash(o.TipMethod, o.PaymentMethod)).Sum(o => o.TipAmount)))
            .OrderByDescending(t => t.Total).ToList();

        // Per shift: the tips of orders finished while the shift was open, the same rule the shift's own report uses.
        var shifts = await db.Shifts.Include(s => s.Location)
            .Where(s => s.OpenedAt < until && (s.ClosedAt == null || s.ClosedAt >= start) && (locationId == null || s.LocationId == locationId))
            .OrderByDescending(s => s.OpenedAt).ToListAsync(ct);
        var byShift = shifts
            .Select(s => new ShiftTipDto(s.Id, s.LocationId, s.Location?.Name ?? "", s.OpenedAt, s.ClosedAt,
                orders.Where(o => o.LocationId == s.LocationId && o.CompletedAt >= s.OpenedAt && o.CompletedAt <= (s.ClosedAt ?? DateTime.UtcNow))
                    .Sum(o => o.TipAmount)))
            .Where(s => s.Total > 0).ToList();

        var cash = orders.Where(o => IsCash(o.TipMethod, o.PaymentMethod)).Sum(o => o.TipAmount);
        var total = orders.Sum(o => o.TipAmount);
        return Result<TipReportDto>.Success(new TipReportDto(
            start.ToString("yyyy-MM-dd", CultureInfo.InvariantCulture), end.ToString("yyyy-MM-dd", CultureInfo.InvariantCulture),
            orders.Count, total, cash, total - cash, byStaff, byShift));
    }

    private static bool TryDay(string? s, out DateTime day)
        => DateTime.TryParseExact(s, "yyyy-MM-dd", CultureInfo.InvariantCulture, DateTimeStyles.AssumeUniversal | DateTimeStyles.AdjustToUniversal, out day);
}
