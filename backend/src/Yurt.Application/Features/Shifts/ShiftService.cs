using Microsoft.EntityFrameworkCore;
using Yurt.Application.Common.Interfaces;
using Yurt.Application.Common.Models;
using Yurt.Domain.Entities;
using Yurt.Domain.Enums;

namespace Yurt.Application.Features.Shifts;

/// <summary>Cashier shifts, cash in/out and the X/Z report (Phase 6, <c>own_pos</c>).</summary>
public class ShiftService(IApplicationDbContext db, ICurrentUserService user, IAuditLogService audit)
{
    private string Who => user.Username ?? "unknown";

    public async Task<Result<ShiftDto?>> CurrentAsync(Guid locationId, CancellationToken ct = default)
    {
        var shift = await db.Shifts.Include(s => s.Location)
            .FirstOrDefaultAsync(s => s.LocationId == locationId && s.ClosedAt == null, ct);
        return Result<ShiftDto?>.Success(shift is null ? null : ToDto(shift));
    }

    public async Task<Result<List<ShiftDto>>> ListAsync(Guid locationId, CancellationToken ct = default)
    {
        var shifts = await db.Shifts.Include(s => s.Location)
            .Where(s => s.LocationId == locationId)
            .OrderByDescending(s => s.OpenedAt).Take(30).ToListAsync(ct);
        return Result<List<ShiftDto>>.Success(shifts.Select(ToDto).ToList());
    }

    public async Task<Result<ShiftDto>> OpenAsync(OpenShiftDto dto, CancellationToken ct = default)
    {
        if (dto.OpeningCash < 0) return Result<ShiftDto>.Failure("Cash cannot be negative.", 400);
        var location = await db.Locations.FirstOrDefaultAsync(l => l.Id == dto.LocationId, ct);
        if (location is null) return Result<ShiftDto>.Failure("Location not found.", 404);
        if (await db.Shifts.AnyAsync(s => s.LocationId == dto.LocationId && s.ClosedAt == null, ct))
            return Result<ShiftDto>.Failure("A shift is already open at this branch.", 409);

        var shift = new Shift { LocationId = dto.LocationId, Location = location, OpenedBy = Who, OpeningCash = dto.OpeningCash };
        db.Shifts.Add(shift);
        try { await db.SaveChangesAsync(ct); }
        catch (DbUpdateException) { return Result<ShiftDto>.Failure("A shift is already open at this branch.", 409); }
        await audit.LogAsync("ShiftOpen", "Shift", shift.Id.ToString(), $"opening cash {dto.OpeningCash}", ct);
        return Result<ShiftDto>.Success(ToDto(shift), 201);
    }

    public async Task<Result<CashMovementDto>> AddMovementAsync(Guid shiftId, CashMovementInputDto dto, CancellationToken ct = default)
    {
        if (dto.Amount <= 0) return Result<CashMovementDto>.Failure("Amount must be more than zero.", 400);
        var reason = dto.Reason?.Trim() ?? "";
        if (reason.Length is 0 or > 200) return Result<CashMovementDto>.Failure("Write a short reason (up to 200 characters).", 400);
        var shift = await db.Shifts.FirstOrDefaultAsync(s => s.Id == shiftId, ct);
        if (shift is null) return Result<CashMovementDto>.Failure("Shift not found.", 404);
        if (shift.ClosedAt is not null) return Result<CashMovementDto>.Failure("This shift is closed.", 409);

        var m = new CashMovement { ShiftId = shiftId, Amount = dto.Amount, IsOut = dto.IsOut, Reason = reason, CreatedBy = Who };
        db.CashMovements.Add(m);
        await db.SaveChangesAsync(ct);
        await audit.LogAsync(dto.IsOut ? "CashOut" : "CashIn", "Shift", shiftId.ToString(), $"{dto.Amount}: {reason}", ct);
        return Result<CashMovementDto>.Success(ToDto(m), 201);
    }

    /// <summary>Closes the shift with the cash counted and returns the Z report.</summary>
    public async Task<Result<ShiftReportDto>> CloseAsync(Guid shiftId, CloseShiftDto dto, CancellationToken ct = default)
    {
        if (dto.CountedCash < 0) return Result<ShiftReportDto>.Failure("Cash cannot be negative.", 400);
        var shift = await db.Shifts.FirstOrDefaultAsync(s => s.Id == shiftId, ct);
        if (shift is null) return Result<ShiftReportDto>.Failure("Shift not found.", 404);
        if (shift.ClosedAt is not null) return Result<ShiftReportDto>.Failure("This shift is already closed.", 409);

        shift.ClosedAt = DateTime.UtcNow;
        shift.ClosedBy = Who;
        shift.CountedCash = dto.CountedCash;
        shift.Note = string.IsNullOrWhiteSpace(dto.Note) ? null : dto.Note.Trim()[..Math.Min(dto.Note.Trim().Length, 500)];
        await db.SaveChangesAsync(ct);
        await audit.LogAsync("ShiftClose", "Shift", shift.Id.ToString(), $"counted {dto.CountedCash}", ct);
        return await ReportAsync(shiftId, ct);
    }

    public async Task<Result<ShiftReportDto>> ReportAsync(Guid shiftId, CancellationToken ct = default)
    {
        var shift = await db.Shifts.Include(s => s.Location).Include(s => s.Movements)
            .FirstOrDefaultAsync(s => s.Id == shiftId, ct);
        if (shift is null) return Result<ShiftReportDto>.Failure("Shift not found.", 404);

        var to = shift.ClosedAt ?? DateTime.UtcNow;
        var orders = await db.Orders
            .Where(o => o.LocationId == shift.LocationId && o.CompletedAt != null
                && o.CompletedAt >= shift.OpenedAt && o.CompletedAt <= to && o.Status == OrderStatus.Completed)
            .Select(o => new { o.Subtotal, o.DiscountAmount, o.Total, o.LoyaltyPointsSpent, o.PaymentMethod })
            .ToListAsync(ct);
        var declined = await db.Orders.CountAsync(o => o.LocationId == shift.LocationId && o.Status == OrderStatus.Declined
            && o.UpdatedAt >= shift.OpenedAt && o.UpdatedAt <= to, ct);

        // What the guest pays in money: the total less points. A cash order with no method recorded counts as cash.
        var lines = orders.Select(o => (
            Method: o.PaymentMethod ?? PaymentMethod.Cash,
            Paid: Math.Max(0m, o.Total - (o.LoyaltyPointsSpent ?? 0m)))).ToList();
        var byMethod = lines.GroupBy(l => l.Method)
            .Select(g => new PaymentMethodTotalDto(g.Key.ToString(), g.Count(), g.Sum(x => x.Paid)))
            .OrderByDescending(x => x.Amount).ToList();
        var cashSales = lines.Where(l => l.Method == PaymentMethod.Cash).Sum(l => l.Paid);
        var cashIn = shift.Movements.Where(m => !m.IsOut).Sum(m => m.Amount);
        var cashOut = shift.Movements.Where(m => m.IsOut).Sum(m => m.Amount);
        var expected = shift.OpeningCash + cashSales + cashIn - cashOut;

        var report = new ShiftReportDto(
            ToDto(shift), orders.Count, declined,
            orders.Sum(o => o.Subtotal), orders.Sum(o => o.DiscountAmount), orders.Sum(o => o.LoyaltyPointsSpent ?? 0m),
            lines.Sum(l => l.Paid), byMethod, cashSales, cashIn, cashOut, expected,
            shift.CountedCash is null ? null : shift.CountedCash - expected,
            shift.Movements.OrderBy(m => m.CreatedAt).Select(ToDto).ToList());
        return Result<ShiftReportDto>.Success(report);
    }

    private static ShiftDto ToDto(Shift s) => new(s.Id, s.LocationId, s.Location?.Name ?? "", s.OpenedAt, s.OpenedBy,
        s.OpeningCash, s.ClosedAt, s.ClosedBy, s.CountedCash, s.Note);

    private static CashMovementDto ToDto(CashMovement m) => new(m.Id, m.Amount, m.IsOut, m.Reason, m.CreatedBy, m.CreatedAt);
}
