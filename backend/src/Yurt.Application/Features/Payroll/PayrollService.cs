using System.Globalization;
using Microsoft.EntityFrameworkCore;
using Yurt.Application.Common.Interfaces;
using Yurt.Application.Common.Models;
using Yurt.Application.Features.TimeClock;
using Yurt.Domain.Entities;
using Yurt.Domain.Enums;

namespace Yurt.Application.Features.Payroll;

/// <summary>
/// Monthly pay for the cafe's staff (module <c>payroll</c>): a fixed salary per person, optionally an hourly rate paid on the hours worked (time clock), bonuses and deductions the CEO adds
/// for a month, and a pay sheet per month that the CEO closes once everyone is paid. Hours only count when a person has an hourly rate.
/// </summary>
public class PayrollService(IApplicationDbContext db, ICurrentUserService user, IAuditLogService audit, TimeClockService clock)
{
    private string Who => user.Username ?? "unknown";

    /// <summary>Everyone who can be paid: the cafe's workers, admins and CEOs (never the platform operator).</summary>
    private IQueryable<AdminUser> Staff => db.AdminUsers.Where(u => u.Role != AdminRole.SuperAdmin);

    public static bool IsValidMonth(string? month)
        => month is { Length: 7 } && DateTime.TryParseExact(month + "-01", "yyyy-MM-dd", CultureInfo.InvariantCulture, DateTimeStyles.None, out _);

    public async Task<List<PayrollStaffDto>> GetStaffAsync(CancellationToken ct = default)
    {
        var salaries = await db.StaffSalaries.AsNoTracking().ToDictionaryAsync(s => s.AdminUserId, ct);
        var people = await Staff.AsNoTracking().OrderBy(u => u.Username).ToListAsync(ct);
        return people
            .Select(u => new PayrollStaffDto(u.Id, u.Username, u.Role.ToString(), u.IsActive,
                salaries.GetValueOrDefault(u.Id)?.MonthlySalary ?? 0, salaries.GetValueOrDefault(u.Id)?.HourlyRate ?? 0))
            .ToList();
    }

    public async Task<Result<PayrollStaffDto>> SetSalaryAsync(Guid adminUserId, decimal monthlySalary, decimal? hourlyRate = null, CancellationToken ct = default)
    {
        if (monthlySalary < 0 || monthlySalary > 100_000_000)
            return Result<PayrollStaffDto>.Failure("Salary must be between 0 and 100 000 000.", 422);
        if (hourlyRate is < 0 or > 1_000_000)
            return Result<PayrollStaffDto>.Failure("The hourly rate must be between 0 and 1 000 000.", 422);
        var person = await Staff.FirstOrDefaultAsync(u => u.Id == adminUserId, ct);
        if (person is null) return Result<PayrollStaffDto>.NotFound("Staff member not found.");

        var row = await db.StaffSalaries.FirstOrDefaultAsync(s => s.AdminUserId == adminUserId, ct);
        if (row is null)
            db.StaffSalaries.Add(row = new StaffSalary { AdminUserId = adminUserId, MonthlySalary = monthlySalary, HourlyRate = hourlyRate ?? 0 });
        else
        {
            row.MonthlySalary = monthlySalary;
            if (hourlyRate is { } rate) row.HourlyRate = rate;
            row.UpdatedAt = DateTime.UtcNow;
        }
        await db.SaveChangesAsync(ct);

        await audit.LogAsync("SalarySet", "AdminUser", adminUserId.ToString(),
            $"{person.Username}: {monthlySalary.ToString("0.##", CultureInfo.InvariantCulture)}"
            + (row.HourlyRate > 0 ? $" + {row.HourlyRate.ToString("0.##", CultureInfo.InvariantCulture)}/h" : ""), ct);
        return Result<PayrollStaffDto>.Success(new PayrollStaffDto(person.Id, person.Username, person.Role.ToString(), person.IsActive, monthlySalary, row.HourlyRate));
    }

    public async Task<Result<PayrollSheetDto>> GetMonthAsync(string month, CancellationToken ct = default)
    {
        if (!IsValidMonth(month)) return Result<PayrollSheetDto>.Failure("Month must look like 2026-11.", 422);

        var closed = await db.PayrollMonths.AsNoTracking().Include(m => m.Lines).FirstOrDefaultAsync(m => m.Month == month, ct);
        var adjustments = await db.PayAdjustments.AsNoTracking()
            .Where(a => a.Month == month).OrderBy(a => a.CreatedAt).ToListAsync(ct);
        var byUser = adjustments.GroupBy(a => a.AdminUserId).ToDictionary(g => g.Key, g => g.ToList());

        List<PayrollLineDto> lines;
        if (closed is not null)
        {
            lines = closed.Lines.OrderBy(l => l.Username, StringComparer.OrdinalIgnoreCase).Select(l => new PayrollLineDto(
                l.AdminUserId, l.Username, l.Role, l.Salary, l.Bonuses, l.Deductions, l.Total,
                (byUser.GetValueOrDefault(l.AdminUserId) ?? []).Select(ToDto).ToList(), l.Hours, l.HourlyRate, l.HourlyPay)).ToList();
        }
        else
        {
            var salaries = await db.StaffSalaries.AsNoTracking().ToDictionaryAsync(s => s.AdminUserId, ct);
            var worked = await clock.HoursForMonthAsync(month, ct);
            var people = await Staff.AsNoTracking().OrderBy(u => u.Username).ToListAsync(ct);
            lines = [];
            foreach (var p in people)
            {
                var pay = salaries.GetValueOrDefault(p.Id);
                var salary = p.IsActive ? pay?.MonthlySalary ?? 0m : 0m;
                var rate = pay?.HourlyRate ?? 0m;
                var hours = rate > 0 ? worked.GetValueOrDefault(p.Id) : 0m;
                var mine = byUser.GetValueOrDefault(p.Id) ?? [];
                if (salary == 0 && rate == 0 && mine.Count == 0) continue;
                lines.Add(Line(p.Id, p.Username, p.Role.ToString(), salary, mine, hours, rate));
            }
        }

        return Result<PayrollSheetDto>.Success(new PayrollSheetDto(
            month, closed is not null, closed?.ClosedAt, closed?.ClosedBy, lines,
            lines.Sum(l => l.Salary), lines.Sum(l => l.Bonuses), lines.Sum(l => l.Deductions), lines.Sum(l => l.Total),
            lines.Sum(l => l.HourlyPay), lines.Sum(l => l.Hours)));
    }

    public async Task<Result<PayAdjustmentDto>> AddAdjustmentAsync(AddAdjustmentDto dto, CancellationToken ct = default)
    {
        if (!IsValidMonth(dto.Month)) return Result<PayAdjustmentDto>.Failure("Month must look like 2026-11.", 422);
        var kind = dto.Kind?.Trim().ToLowerInvariant();
        if (kind is not ("bonus" or "deduction")) return Result<PayAdjustmentDto>.Failure("Kind must be bonus or deduction.", 422);
        if (dto.Amount <= 0 || dto.Amount > 100_000_000) return Result<PayAdjustmentDto>.Failure("Amount must be more than 0.", 422);
        var reason = dto.Reason?.Trim() ?? "";
        if (reason.Length == 0) return Result<PayAdjustmentDto>.Failure("Write a reason.", 422);
        if (reason.Length > 200) reason = reason[..200];

        var person = await Staff.FirstOrDefaultAsync(u => u.Id == dto.AdminUserId, ct);
        if (person is null) return Result<PayAdjustmentDto>.NotFound("Staff member not found.");
        if (await db.PayrollMonths.AnyAsync(m => m.Month == dto.Month, ct))
            return Result<PayAdjustmentDto>.Failure("This month is closed. Reopen it to change it.", 409);

        var row = new PayAdjustment
        {
            AdminUserId = person.Id, Month = dto.Month, Amount = Math.Round(dto.Amount, 2),
            IsDeduction = kind == "deduction", Reason = reason, CreatedBy = Who,
        };
        db.PayAdjustments.Add(row);
        await db.SaveChangesAsync(ct);

        await audit.LogAsync(row.IsDeduction ? "PayDeductionAdded" : "PayBonusAdded", "AdminUser", person.Id.ToString(),
            $"{person.Username} {dto.Month}: {row.Amount.ToString("0.##", CultureInfo.InvariantCulture)} ({reason})", ct);
        return Result<PayAdjustmentDto>.Success(ToDto(row), 201);
    }

    public async Task<Result> DeleteAdjustmentAsync(Guid id, CancellationToken ct = default)
    {
        var row = await db.PayAdjustments.FirstOrDefaultAsync(a => a.Id == id, ct);
        if (row is null) return Result.Failure("Not found.", 404);
        if (await db.PayrollMonths.AnyAsync(m => m.Month == row.Month, ct))
            return Result.Failure("This month is closed. Reopen it to change it.", 409);

        db.PayAdjustments.Remove(row);
        await db.SaveChangesAsync(ct);
        await audit.LogAsync("PayAdjustmentRemoved", "AdminUser", row.AdminUserId.ToString(), $"{row.Month}: {row.Reason}", ct);
        return Result.Success();
    }

    /// <summary>Closes the month after everyone is paid: the sheet is stored as it is now and can no longer change.</summary>
    public async Task<Result<PayrollSheetDto>> CloseMonthAsync(string month, CancellationToken ct = default)
    {
        var sheet = await GetMonthAsync(month, ct);
        if (!sheet.Succeeded) return sheet;
        if (sheet.Data!.Closed) return Result<PayrollSheetDto>.Failure("This month is already closed.", 409);
        if (sheet.Data.Lines.Count == 0) return Result<PayrollSheetDto>.Failure("Nobody to pay this month. Set a salary first.", 422);

        var row = new PayrollMonth { Month = month, ClosedAt = DateTime.UtcNow, ClosedBy = Who };
        foreach (var l in sheet.Data.Lines)
            row.Lines.Add(new PayrollLine
            {
                AdminUserId = l.AdminUserId, Username = l.Username, Role = l.Role,
                Salary = l.Salary, Hours = l.Hours, HourlyRate = l.HourlyRate, HourlyPay = l.HourlyPay, Bonuses = l.Bonuses, Deductions = l.Deductions, Total = l.Total,
            });
        db.PayrollMonths.Add(row);
        await db.SaveChangesAsync(ct);

        await audit.LogAsync("PayrollClosed", "PayrollMonth", row.Id.ToString(),
            $"{month}: {sheet.Data.TotalToPay.ToString("0.##", CultureInfo.InvariantCulture)} to {sheet.Data.Lines.Count} people", ct);
        return await GetMonthAsync(month, ct);
    }

    /// <summary>Opens a closed month again to fix a mistake. Written to the audit log.</summary>
    public async Task<Result<PayrollSheetDto>> ReopenMonthAsync(string month, CancellationToken ct = default)
    {
        if (!IsValidMonth(month)) return Result<PayrollSheetDto>.Failure("Month must look like 2026-11.", 422);
        var row = await db.PayrollMonths.FirstOrDefaultAsync(m => m.Month == month, ct);
        if (row is null) return Result<PayrollSheetDto>.Failure("This month is not closed.", 409);

        db.PayrollMonths.Remove(row); // its lines go with it
        await db.SaveChangesAsync(ct);
        await audit.LogAsync("PayrollReopened", "PayrollMonth", row.Id.ToString(), month, ct);
        return await GetMonthAsync(month, ct);
    }

    private static PayrollLineDto Line(Guid userId, string username, string role, decimal salary, List<PayAdjustment> mine, decimal hours, decimal rate)
    {
        var hourlyPay = Math.Round(hours * rate, 2);
        var bonuses = mine.Where(a => !a.IsDeduction).Sum(a => a.Amount);
        var deductions = mine.Where(a => a.IsDeduction).Sum(a => a.Amount);
        return new PayrollLineDto(userId, username, role, salary, bonuses, deductions, salary + hourlyPay + bonuses - deductions,
            mine.Select(ToDto).ToList(), hours, rate, hourlyPay);
    }

    private static PayAdjustmentDto ToDto(PayAdjustment a) => new(a.Id, a.IsDeduction, a.Amount, a.Reason, a.CreatedBy, a.CreatedAt);
}
