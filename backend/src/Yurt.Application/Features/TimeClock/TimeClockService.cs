using System.Globalization;
using Microsoft.EntityFrameworkCore;
using Yurt.Application.Common.Interfaces;
using Yurt.Application.Common.Models;
using Yurt.Domain.Entities;
using Yurt.Domain.Enums;

namespace Yurt.Application.Features.TimeClock;

public record PunchDto(string? Pin, Guid? LocationId);
public record PunchResultDto(Guid AdminUserId, string Username, string Action, DateTime At, DateTime? ClockedInSince, decimal? HoursThisShift, bool ForgotLast);
public record MyClockDto(bool ClockedIn, DateTime? Since);
public record OnTheClockDto(Guid EntryId, Guid AdminUserId, string Username, Guid? LocationId, DateTime ClockInAt);

public record TimeEntryDto(
    Guid Id, Guid AdminUserId, string Username, Guid? LocationId, DateTime ClockInAt, DateTime? ClockOutAt,
    decimal Hours, bool Open, bool Forgotten, string? EditedBy, string? EditNote);

public record StaffHoursDto(Guid AdminUserId, string Username, string Role, decimal Hours, int Days, int Problems);

/// <param name="Problems">Entries nobody clocked out of (forgotten, or still open since yesterday): a manager should fix them.</param>
public record HoursReportDto(string From, string To, decimal TotalHours, List<StaffHoursDto> ByStaff, List<TimeEntryDto> Entries);

public record SaveTimeEntryDto(Guid? AdminUserId, Guid? LocationId, DateTime? ClockInAt, DateTime? ClockOutAt, string? Reason);

/// <summary>
/// Clock-in and clock-out at the till with the staff PIN, who is on the clock now, worked hours per person, and the
/// hours that go into payroll. Days and months are cafe time (Asia/Almaty), like the profit reports.
/// </summary>
public class TimeClockService(IApplicationDbContext db, StaffPinService pins, ICurrentUserService user, IAuditLogService audit)
{
    public const int MaxDays = 93;
    /// <summary>A shift longer than this with no clock-out is a forgotten clock-out.</summary>
    public static readonly TimeSpan LongestShift = TimeSpan.FromHours(16);

    public static readonly TimeZoneInfo CafeTime = TimeZoneInfo.FindSystemTimeZoneById("Asia/Almaty");

    private string Who => user.Username ?? "unknown";

    /// <summary>Clock in, or clock out when already clocked in. The PIN says who; the till login only needs to be signed in.</summary>
    public async Task<Result<PunchResultDto>> PunchAsync(PunchDto dto, CancellationToken ct = default)
    {
        var found = await pins.FindByPinAsync(dto.Pin, "clock", ct);
        if (!found.Succeeded) return Result<PunchResultDto>.Failure(found.Error!, found.StatusCode);
        return await ToggleAsync(found.Data!, dto.LocationId, ct);
    }

    /// <summary>Am I clocked in? (The signed-in person, no PIN: every role clocks from the admin app.)</summary>
    public async Task<MyClockDto> MineAsync(CancellationToken ct = default)
    {
        var me = user.UserId ?? Guid.Empty;
        var since = DateTime.UtcNow - LongestShift;
        var open = await db.TimeEntries.AsNoTracking().Where(e => e.AdminUserId == me && e.ClockOutAt == null && e.ClockInAt > since)
            .OrderByDescending(e => e.ClockInAt).FirstOrDefaultAsync(ct);
        return new MyClockDto(open != null, open?.ClockInAt);
    }

    /// <summary>Clock in or out as the signed-in person, from the admin app on a phone or at the office computer.</summary>
    public async Task<Result<PunchResultDto>> ToggleMineAsync(Guid? locationId, CancellationToken ct = default)
    {
        if (user.UserId is not { } meId) return Result<PunchResultDto>.Failure("Sign in first.", 401);
        var me = await db.AdminUsers.FirstOrDefaultAsync(u => u.Id == meId && u.IsActive && u.Role != AdminRole.SuperAdmin, ct);
        if (me is null) return Result<PunchResultDto>.Failure("Account not found.", 404);
        return await ToggleAsync(me, locationId, ct);
    }

    private async Task<Result<PunchResultDto>> ToggleAsync(AdminUser who, Guid? locationId, CancellationToken ct)
    {
        var dto = new PunchDto(null, locationId);
        var now = DateTime.UtcNow;

        var open = await db.TimeEntries.Where(e => e.AdminUserId == who.Id && e.ClockOutAt == null).OrderByDescending(e => e.ClockInAt).ToListAsync(ct);
        var current = open.FirstOrDefault();
        var forgot = false;
        // Anything open from earlier than the longest shift was never clocked out: close it with zero hours, a manager fixes it.
        foreach (var stale in open.Where(e => now - e.ClockInAt > LongestShift))
        {
            stale.ClockOutAt = stale.ClockInAt;
            stale.Forgotten = true;
            forgot = true;
            if (stale == current) current = null;
        }

        PunchResultDto result;
        if (current is null)
        {
            if (dto.LocationId is { } loc && !await db.Locations.AnyAsync(l => l.Id == loc, ct))
                return Result<PunchResultDto>.Failure("Branch not found.", 404);
            db.TimeEntries.Add(new TimeEntry { AdminUserId = who.Id, LocationId = dto.LocationId, ClockInAt = now });
            result = new PunchResultDto(who.Id, who.Username, "in", now, now, null, forgot);
        }
        else
        {
            current.ClockOutAt = now;
            result = new PunchResultDto(who.Id, who.Username, "out", now, current.ClockInAt, Hours(current.ClockInAt, now), false);
        }
        await db.SaveChangesAsync(ct);
        await audit.LogAsync(result.Action == "in" ? "ClockIn" : "ClockOut", "AdminUser", who.Id.ToString(), who.Username, ct);
        return Result<PunchResultDto>.Success(result);
    }

    /// <summary>Who is clocked in right now.</summary>
    public async Task<List<OnTheClockDto>> OnTheClockAsync(CancellationToken ct = default)
    {
        var since = DateTime.UtcNow - LongestShift;
        var rows = await db.TimeEntries.AsNoTracking().Where(e => e.ClockOutAt == null && e.ClockInAt > since).OrderBy(e => e.ClockInAt).ToListAsync(ct);
        var names = await Names(rows.Select(r => r.AdminUserId), ct);
        return rows.Select(r => new OnTheClockDto(r.Id, r.AdminUserId, names.GetValueOrDefault(r.AdminUserId, "?"), r.LocationId, r.ClockInAt)).ToList();
    }

    public async Task<Result<HoursReportDto>> ReportAsync(string? from, string? to, Guid? adminUserId, CancellationToken ct = default)
    {
        if (!TryDay(from, out var start) || !TryDay(to, out var end)) return Result<HoursReportDto>.Failure("Give the dates as yyyy-MM-dd.", 400);
        if (end < start) return Result<HoursReportDto>.Failure("The end date is before the start date.", 400);
        if ((end - start).TotalDays >= MaxDays) return Result<HoursReportDto>.Failure("Pick at most three months.", 400);

        var fromUtc = TimeZoneInfo.ConvertTimeToUtc(DateTime.SpecifyKind(start, DateTimeKind.Unspecified), CafeTime);
        var toUtc = TimeZoneInfo.ConvertTimeToUtc(DateTime.SpecifyKind(end.AddDays(1), DateTimeKind.Unspecified), CafeTime);
        var rows = await db.TimeEntries.AsNoTracking()
            .Where(e => e.ClockInAt >= fromUtc && e.ClockInAt < toUtc && (adminUserId == null || e.AdminUserId == adminUserId))
            .OrderBy(e => e.ClockInAt).ToListAsync(ct);
        var userIds = rows.Select(r => r.AdminUserId).Distinct().ToList();
        var people = await db.AdminUsers.AsNoTracking().Where(u => userIds.Contains(u.Id))
            .ToDictionaryAsync(u => u.Id, u => new { u.Username, u.Role }, ct);

        var now = DateTime.UtcNow;
        var entries = rows.Select(r => ToDto(r, people.GetValueOrDefault(r.AdminUserId)?.Username ?? "?", now)).ToList();
        var byStaff = rows.GroupBy(r => r.AdminUserId).Select(g =>
        {
            var p = people.GetValueOrDefault(g.Key);
            var done = g.Where(e => e.ClockOutAt != null && !e.Forgotten).ToList();
            return new StaffHoursDto(g.Key, p?.Username ?? "?", p?.Role.ToString() ?? "",
                Math.Round(done.Sum(e => Hours(e.ClockInAt, e.ClockOutAt!.Value)), 2),
                done.Select(e => TimeZoneInfo.ConvertTimeFromUtc(e.ClockInAt, CafeTime).Date).Distinct().Count(),
                g.Count(e => e.Forgotten || (e.ClockOutAt == null && now - e.ClockInAt > LongestShift)));
        }).OrderBy(s => s.Username, StringComparer.OrdinalIgnoreCase).ToList();

        return Result<HoursReportDto>.Success(new HoursReportDto(start.ToString("yyyy-MM-dd", CultureInfo.InvariantCulture),
            end.ToString("yyyy-MM-dd", CultureInfo.InvariantCulture), byStaff.Sum(s => s.Hours), byStaff, entries));
    }

    /// <summary>A manager adds a forgotten entry or fixes one (set the clock-out of a forgotten shift). A reason is required.</summary>
    public async Task<Result<TimeEntryDto>> SaveAsync(Guid? id, SaveTimeEntryDto dto, CancellationToken ct = default)
    {
        var reason = dto.Reason?.Trim() ?? "";
        if (reason.Length == 0) return Result<TimeEntryDto>.Failure("Write the reason first.", 400);
        if (reason.Length > 200) return Result<TimeEntryDto>.Failure("The reason is too long (up to 200 characters).", 400);

        TimeEntry entry;
        if (id is null)
        {
            if (dto.AdminUserId is not { } who || dto.ClockInAt is null || dto.ClockOutAt is null)
                return Result<TimeEntryDto>.Failure("Choose the person and both times.", 400);
            if (!await db.AdminUsers.AnyAsync(u => u.Id == who, ct)) return Result<TimeEntryDto>.NotFound("Staff member not found.");
            entry = new TimeEntry { AdminUserId = who, LocationId = dto.LocationId };
            db.TimeEntries.Add(entry);
        }
        else
        {
            var found = await db.TimeEntries.FirstOrDefaultAsync(e => e.Id == id, ct);
            if (found is null) return Result<TimeEntryDto>.NotFound("Entry not found.");
            entry = found;
        }
        if (dto.ClockInAt is { } cin) entry.ClockInAt = DateTime.SpecifyKind(cin, DateTimeKind.Utc).ToUniversalTime();
        if (dto.ClockOutAt is { } cout) entry.ClockOutAt = DateTime.SpecifyKind(cout, DateTimeKind.Utc).ToUniversalTime();
        if (entry.ClockOutAt is { } o)
        {
            if (o < entry.ClockInAt) return Result<TimeEntryDto>.Failure("Clock-out is before clock-in.", 400);
            if (o - entry.ClockInAt > LongestShift) return Result<TimeEntryDto>.Failure("A shift can't be longer than 16 hours.", 400);
            entry.Forgotten = false;
        }
        if (entry.ClockInAt > DateTime.UtcNow.AddMinutes(5)) return Result<TimeEntryDto>.Failure("Clock-in is in the future.", 400);
        entry.EditedBy = Who;
        entry.EditNote = reason;
        entry.UpdatedAt = DateTime.UtcNow;
        await db.SaveChangesAsync(ct);

        var name = (await Names([entry.AdminUserId], ct)).GetValueOrDefault(entry.AdminUserId, "?");
        await audit.LogAsync(id is null ? "TimeEntryAdded" : "TimeEntryChanged", "TimeEntry", entry.Id.ToString(), $"{name}: {reason}", ct);
        return Result<TimeEntryDto>.Success(ToDto(entry, name, DateTime.UtcNow), id is null ? 201 : 200);
    }

    public async Task<Result> DeleteAsync(Guid id, string? reason, CancellationToken ct = default)
    {
        reason = reason?.Trim() ?? "";
        if (reason.Length == 0) return Result.Failure("Write the reason first.", 400);
        var entry = await db.TimeEntries.FirstOrDefaultAsync(e => e.Id == id, ct);
        if (entry is null) return Result.Failure("Entry not found.", 404);
        var name = (await Names([entry.AdminUserId], ct)).GetValueOrDefault(entry.AdminUserId, "?");
        db.TimeEntries.Remove(entry);
        await db.SaveChangesAsync(ct);
        await audit.LogAsync("TimeEntryRemoved", "TimeEntry", id.ToString(), $"{name}: {reason}", ct);
        return Result.Success();
    }

    /// <summary>Worked hours per person for one payroll month ("yyyy-MM", cafe time). Forgotten and still-open entries count 0.</summary>
    public async Task<Dictionary<Guid, decimal>> HoursForMonthAsync(string month, CancellationToken ct = default)
    {
        var first = DateTime.ParseExact(month + "-01", "yyyy-MM-dd", CultureInfo.InvariantCulture);
        var fromUtc = TimeZoneInfo.ConvertTimeToUtc(DateTime.SpecifyKind(first, DateTimeKind.Unspecified), CafeTime);
        var toUtc = TimeZoneInfo.ConvertTimeToUtc(DateTime.SpecifyKind(first.AddMonths(1), DateTimeKind.Unspecified), CafeTime);
        var rows = await db.TimeEntries.AsNoTracking()
            .Where(e => e.ClockOutAt != null && !e.Forgotten && e.ClockInAt >= fromUtc && e.ClockInAt < toUtc)
            .Select(e => new { e.AdminUserId, e.ClockInAt, ClockOutAt = e.ClockOutAt!.Value }).ToListAsync(ct);
        return rows.GroupBy(r => r.AdminUserId).ToDictionary(g => g.Key, g => Math.Round(g.Sum(r => Hours(r.ClockInAt, r.ClockOutAt)), 2));
    }

    private static decimal Hours(DateTime from, DateTime to) => (decimal)(to - from).TotalHours;

    private static TimeEntryDto ToDto(TimeEntry e, string name, DateTime now)
        => new(e.Id, e.AdminUserId, name, e.LocationId, e.ClockInAt, e.ClockOutAt,
            e.ClockOutAt is { } o && !e.Forgotten ? Math.Round(Hours(e.ClockInAt, o), 2) : 0,
            e.ClockOutAt == null && now - e.ClockInAt <= LongestShift, e.Forgotten || (e.ClockOutAt == null && now - e.ClockInAt > LongestShift),
            e.EditedBy, e.EditNote);

    private async Task<Dictionary<Guid, string>> Names(IEnumerable<Guid> ids, CancellationToken ct)
    {
        var list = ids.Distinct().ToList();
        return await db.AdminUsers.AsNoTracking().Where(u => list.Contains(u.Id)).ToDictionaryAsync(u => u.Id, u => u.Username, ct);
    }

    private static bool TryDay(string? s, out DateTime day)
        => DateTime.TryParseExact(s, "yyyy-MM-dd", CultureInfo.InvariantCulture, DateTimeStyles.None, out day);
}
