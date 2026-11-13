using Microsoft.EntityFrameworkCore;
using Yurt.Application.Common.Interfaces;
using Yurt.Domain.Entities;

namespace Yurt.Application.Features.Menu.Services;

/// <summary>
/// Time-based menus at one moment: which schedules are running, which dishes are on the menu, and what time prices
/// apply. One place used by the menu the guest sees and by the checks when an order or counter sale is made, so the
/// two can never disagree. The clock is the branch's own (<see cref="Location.TimeZone"/>).
/// </summary>
public sealed class TimedMenu
{
    public const string DefaultZone = "Asia/Almaty";

    private readonly HashSet<Guid> _active;
    private readonly List<MenuTimePrice> _rules;
    private readonly Dictionary<Guid, Guid> _categorySchedule;
    private readonly HashSet<Guid> _known;

    private TimedMenu(HashSet<Guid> active, List<MenuTimePrice> rules, Dictionary<Guid, Guid> categorySchedule, HashSet<Guid> known)
    {
        _active = active; _rules = rules; _categorySchedule = categorySchedule; _known = known;
    }

    /// <summary>The schedules running now, as a stable text. A menu cached under it is right for as long as it stays the same.</summary>
    public string Signature => string.Join(',', _active.OrderBy(i => i).Select(i => i.ToString("N")[..8]));

    public IReadOnlyCollection<Guid> ActiveScheduleIds => _active;

    public static TimeZoneInfo ZoneOf(string? name)
    {
        try { return TimeZoneInfo.FindSystemTimeZoneById(string.IsNullOrWhiteSpace(name) ? DefaultZone : name); }
        catch (Exception e) when (e is TimeZoneNotFoundException or InvalidTimeZoneException) { return TimeZoneInfo.FindSystemTimeZoneById(DefaultZone); }
    }

    public static bool IsKnownZone(string name)
    {
        try { TimeZoneInfo.FindSystemTimeZoneById(name); return true; }
        catch (Exception e) when (e is TimeZoneNotFoundException or InvalidTimeZoneException) { return false; }
    }

    /// <summary>Is the window running at this local time?</summary>
    public static bool IsRunning(MenuSchedule s, DateTime local)
    {
        var day = ((int)local.DayOfWeek + 6) % 7; // Monday = 0
        var minute = local.Hour * 60 + local.Minute;
        bool On(int d) => (s.Days >> d & 1) == 1;
        if (s.StartMinute < s.EndMinute) return On(day) && minute >= s.StartMinute && minute < s.EndMinute;
        // Runs past midnight: the late part belongs to the day it started on.
        return (On(day) && minute >= s.StartMinute) || (On((day + 6) % 7) && minute < s.EndMinute);
    }

    /// <summary>Schedules that matter for a branch (all of them when no branch is given).</summary>
    private static bool Applies(MenuSchedule s, Guid? locationId) => s.LocationId is null || locationId is null || s.LocationId == locationId;

    public static async Task<TimedMenu> LoadAsync(IApplicationDbContext db, Guid? locationId, DateTime atUtc, CancellationToken ct)
    {
        var schedules = await db.MenuSchedules.Where(s => s.IsActive).Include(s => s.Prices).ToListAsync(ct);
        if (schedules.Count == 0) return new TimedMenu([], [], [], []);
        // A schedule that is switched off, or that belongs to another branch, is ignored: what is tied to it stays on the menu.
        var known = schedules.Where(s => Applies(s, locationId)).Select(s => s.Id).ToHashSet();

        string? zoneName = null;
        if (locationId is { } loc)
            zoneName = await db.Locations.Where(l => l.Id == loc).Select(l => l.TimeZone).FirstOrDefaultAsync(ct);
        var local = TimeZoneInfo.ConvertTimeFromUtc(DateTime.SpecifyKind(atUtc, DateTimeKind.Utc), ZoneOf(zoneName));

        var active = schedules.Where(s => Applies(s, locationId) && IsRunning(s, local)).Select(s => s.Id).ToHashSet();
        var rules = schedules.Where(s => active.Contains(s.Id)).SelectMany(s => s.Prices).ToList();
        var catSchedules = await db.MenuCategories.Where(c => c.ScheduleId != null)
            .Select(c => new { c.Id, S = c.ScheduleId!.Value }).ToListAsync(ct);
        return new TimedMenu(active, rules, catSchedules.ToDictionary(c => c.Id, c => c.S), known);
    }

    /// <summary>Is the dish on the menu now? A dish or category tied to a schedule shows only while it is running.</summary>
    public bool IsOnMenu(MenuItem item)
        => (item.ScheduleId is not { } own || !_known.Contains(own) || _active.Contains(own))
           && (!_categorySchedule.TryGetValue(item.CategoryId, out var cat) || !_known.Contains(cat) || _active.Contains(cat));

    /// <summary>Ids of dishes and categories tied to schedules that are not running now (for filtering a query).</summary>
    public List<Guid> HiddenScheduleIds() => _known.Where(i => !_active.Contains(i)).ToList();

    /// <summary>
    /// The price now. <paramref name="sized"/> is true for a price of a size: a fixed time price is for the dish itself,
    /// so sizes only follow a percent rule. With several rules running the lowest price wins.
    /// </summary>
    public decimal PriceOf(MenuItem item, decimal price, bool sized = false)
    {
        if (_rules.Count == 0) return price;
        var rules = _rules.Where(r => r.MenuItemId == item.Id).ToList();
        if (rules.Count == 0) rules = _rules.Where(r => r.MenuItemId == null && r.CategoryId == item.CategoryId).ToList();
        var best = price;
        foreach (var r in rules)
        {
            var candidate = r.PercentOff is { } off && r.Price is null
                ? Math.Round(price * (1 - off / 100m), 2, MidpointRounding.AwayFromZero)
                : r.Price is { } fixedPrice && !sized ? fixedPrice : price;
            if (candidate < best) best = candidate;
        }
        return best;
    }

    /// <summary>The next moment (UTC) some window opens or closes at the branch, or null when no schedule exists.</summary>
    public static async Task<DateTime?> NextChangeAsync(IApplicationDbContext db, Guid? locationId, DateTime nowUtc, CancellationToken ct)
    {
        var schedules = (await db.MenuSchedules.Where(s => s.IsActive).ToListAsync(ct)).Where(s => Applies(s, locationId)).ToList();
        if (schedules.Count == 0) return null;
        string? zoneName = locationId is { } loc
            ? await db.Locations.Where(l => l.Id == loc).Select(l => l.TimeZone).FirstOrDefaultAsync(ct) : null;
        var zone = ZoneOf(zoneName);
        var localNow = TimeZoneInfo.ConvertTimeFromUtc(DateTime.SpecifyKind(nowUtc, DateTimeKind.Utc), zone);

        DateTime? best = null;
        for (var d = 0; d <= 8; d++)
        {
            var date = localNow.Date.AddDays(d);
            foreach (var s in schedules)
                foreach (var minute in new[] { s.StartMinute, s.EndMinute })
                {
                    var local = DateTime.SpecifyKind(date.AddMinutes(minute), DateTimeKind.Unspecified);
                    if (zone.IsInvalidTime(local)) local = local.AddHours(1);
                    var utc = TimeZoneInfo.ConvertTimeToUtc(local, zone);
                    if (utc > nowUtc && (best is null || utc < best)) best = utc;
                }
        }
        return best;
    }

    /// <summary>"07:30" to minutes after midnight; "24:00" is the end of the day.</summary>
    public static bool TryParseMinute(string? text, bool isEnd, out int minute)
    {
        minute = 0;
        if (string.IsNullOrWhiteSpace(text)) return false;
        var parts = text.Trim().Split(':');
        if (parts.Length != 2 || !int.TryParse(parts[0], out var h) || !int.TryParse(parts[1], out var m)) return false;
        if (m is < 0 or > 59 || h < 0) return false;
        minute = h * 60 + m;
        return isEnd ? minute is >= 1 and <= 1440 : minute is >= 0 and <= 1439;
    }

    public static string FormatMinute(int minute) => $"{minute / 60:00}:{minute % 60:00}";
}
