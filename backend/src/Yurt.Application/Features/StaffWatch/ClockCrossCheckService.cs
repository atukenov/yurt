using System.Globalization;
using Microsoft.EntityFrameworkCore;
using Yurt.Application.Common.Interfaces;
using Yurt.Application.Common.Models;
using Yurt.Application.Features.TimeClock;
using Yurt.Domain;

namespace Yurt.Application.Features.StaffWatch;

/// <summary>One minute the camera box was running: people seen at the stations, and people clocked in at that moment.</summary>
public record CrossMinute(DateTime Minute, int Seen, int Clocked);

/// <summary>A stretch of time where the camera and the time clock disagree. Times are cafe local time.</summary>
public record ClockCrossFinding(string Kind, DateTime From, DateTime To, int Minutes, int MaxSeen, int MaxClocked);

public record ClockCrossCheckDto(Guid LocationId, string Day, bool HasCameras, int CameraMinutes, List<ClockCrossFinding> Findings);

/// <summary>
/// Compares what the Yurt Watch camera sees at the stations with who is clocked in (modules <c>staff_watch</c> and
/// <c>own_pos</c>). It counts people, never names them: the camera does not know who anybody is. So it can say "someone
/// worked for 40 minutes and nobody was clocked in", not "Anna forgot to clock in".
/// </summary>
public class ClockCrossCheckService(IApplicationDbContext db, IModuleGate modules)
{
    /// <summary>People at the stations with nobody clocked in: this long before it is reported.</summary>
    public const int WorkingNotClockedMinutes = 10;
    /// <summary>More people at the stations than are clocked in.</summary>
    public const int MoreSeenMinutes = 15;
    /// <summary>Clocked in but nobody at a station (they may be in the kitchen or the storeroom, so this is only a hint).</summary>
    public const int ClockedNotSeenMinutes = 60;
    /// <summary>A hole of up to this many minutes inside a stretch does not end it.</summary>
    public const int MaxGapMinutes = 2;

    public async Task<Result<ClockCrossCheckDto>> CheckAsync(Guid locationId, string? day, CancellationToken ct = default)
    {
        if (!await modules.IsEnabledAsync(ModuleKeys.StaffWatch, ct) || !await modules.IsEnabledAsync(ModuleKeys.OwnPos, ct))
            return Result<ClockCrossCheckDto>.Failure("Turn on staff watch and the own POS module first.", 403);
        if (!await db.Locations.AnyAsync(l => l.Id == locationId, ct)) return Result<ClockCrossCheckDto>.NotFound("Branch not found.");

        var d = DateOnly.TryParseExact(day ?? "", "yyyy-MM-dd", CultureInfo.InvariantCulture, DateTimeStyles.None, out var parsed)
            ? parsed : DateOnly.FromDateTime(StaffWatchService.ToLocal(DateTime.UtcNow));
        var fromUtc = StaffWatchService.ToUtc(d.ToDateTime(TimeOnly.MinValue));
        var toUtc = StaffWatchService.ToUtc(d.AddDays(1).ToDateTime(TimeOnly.MinValue));
        var dayText = d.ToString("yyyy-MM-dd", CultureInfo.InvariantCulture);

        var cameras = await db.WatchCameras.AsNoTracking().Select(c => new { c.LocationId, c.ZonesJson }).ToListAsync(ct);
        var mine = cameras.Where(c => c.LocationId == locationId).SelectMany(c => StaffWatchService.ParseZones(c.ZonesJson)).ToList();
        var stations = mine.Where(z => z.Kind == "station").Select(z => z.Name).ToList();
        if (stations.Count == 0) return Result<ClockCrossCheckDto>.Success(new ClockCrossCheckDto(locationId, dayText, false, 0, []));

        // Same rule as the day report: a minute counts when the camera had at least 20 s of pictures and a station was
        // occupied for at least half of them.
        var rows = await db.WatchMinutes.AsNoTracking()
            .Where(m => m.LocationId == locationId && m.Minute >= fromUtc && m.Minute < toUtc && m.Seconds >= 20)
            .Select(m => new { m.Minute, m.Zone, m.Seconds, m.Occupied, m.PeopleMax }).ToListAsync(ct);
        var seenByMinute = rows.Where(r => stations.Contains(r.Zone)).GroupBy(r => r.Minute)
            .ToDictionary(g => g.Key, g => g.Where(r => r.Occupied * 2 >= r.Seconds).Sum(r => Math.Max(1, r.PeopleMax)));
        var cameraMinutes = rows.Select(r => r.Minute).Distinct().OrderBy(m => m).ToList();

        // Entries with no branch (clocked in from the admin app) belong to the one branch that has cameras, if there is only one.
        var onlyBranch = cameras.Select(c => c.LocationId).Distinct().Count() <= 1;
        var now = DateTime.UtcNow;
        var entries = (await db.TimeEntries.AsNoTracking()
                .Where(e => !e.Forgotten && e.ClockInAt < toUtc && (e.ClockOutAt == null || e.ClockOutAt > fromUtc))
                .Select(e => new { e.LocationId, e.ClockInAt, e.ClockOutAt }).ToListAsync(ct))
            .Where(e => e.LocationId == locationId || (e.LocationId == null && onlyBranch))
            .Select(e => (From: e.ClockInAt, To: e.ClockOutAt ?? (now - e.ClockInAt <= TimeClockService.LongestShift ? now : e.ClockInAt)))
            .ToList();

        var minutes = cameraMinutes.Select(m => new CrossMinute(
            StaffWatchService.ToLocal(m),
            seenByMinute.GetValueOrDefault(m),
            entries.Count(e => e.From <= m && m < e.To))).ToList();

        return Result<ClockCrossCheckDto>.Success(new ClockCrossCheckDto(locationId, dayText, true, minutes.Count, Windows(minutes)));
    }

    /// <summary>The disagreeing stretches in a list of camera minutes (any order). Pure, so it can be tested without a database.</summary>
    public static List<ClockCrossFinding> Windows(IEnumerable<CrossMinute> minutes)
    {
        var ordered = minutes.OrderBy(m => m.Minute).ToList();
        var found = new List<ClockCrossFinding>();
        found.AddRange(Run(ordered, "working_not_clocked", m => m.Seen > 0 && m.Clocked == 0, WorkingNotClockedMinutes));
        found.AddRange(Run(ordered, "more_seen_than_clocked", m => m.Clocked > 0 && m.Seen > m.Clocked, MoreSeenMinutes));
        found.AddRange(Run(ordered, "clocked_not_seen", m => m.Clocked > 0 && m.Seen == 0, ClockedNotSeenMinutes));
        return found.OrderBy(f => f.From).ToList();
    }

    private static IEnumerable<ClockCrossFinding> Run(List<CrossMinute> ordered, string kind, Func<CrossMinute, bool> hit, int minMinutes)
    {
        List<CrossMinute>? run = null;
        foreach (var m in ordered.Where(hit).Append(null!))
        {
            if (run is not null && (m is null || (m.Minute - run[^1].Minute).TotalMinutes > MaxGapMinutes + 1))
            {
                var span = (int)(run[^1].Minute - run[0].Minute).TotalMinutes + 1;
                if (span >= minMinutes)
                    yield return new ClockCrossFinding(kind, run[0].Minute, run[^1].Minute.AddMinutes(1), span, run.Max(r => r.Seen), run.Max(r => r.Clocked));
                run = null;
            }
            if (m is null) break;
            (run ??= []).Add(m);
        }
    }
}
