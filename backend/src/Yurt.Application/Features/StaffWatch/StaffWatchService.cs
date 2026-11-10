using System.Globalization;
using System.Security.Cryptography;
using System.Text;
using System.Text.Json;
using System.Text.RegularExpressions;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;
using Yurt.Application.Common.Interfaces;
using Yurt.Application.Common.Models;
using Yurt.Domain.Entities;
using Yurt.Domain.Enums;

namespace Yurt.Application.Features.StaffWatch;

/// <summary>
/// Staff watch (<c>staff_watch</c>): the admin side (box key, cameras and zones, shift plan, settings, the day's report)
/// and the box side (settings fetch, minute counts, alerts, daily report, still pictures). The box does the counting
/// and the alert rules (watch/ in the repository); the server stores, shows and forwards to Telegram.
/// </summary>
public partial class StaffWatchService(
    IApplicationDbContext db, ITenantContext tenant, ITenantSecrets secrets, IWatchTelegram telegram,
    IAuditLogService audit, ILogger<StaffWatchService> logger)
{
    public const string TelegramTokenKey = "Watch:TelegramBotToken";
    public const int KeepDays = 90;
    private const int MaxCameras = 16, MaxZonesPerCamera = 12, MaxShiftRows = 200, MaxBatch = 2000;
    private const int MaxSnapshotBytes = 2 * 1024 * 1024;

    public static readonly TimeZoneInfo CafeTime = TimeZoneInfo.FindSystemTimeZoneById("Asia/Almaty");
    private static readonly string[] Kinds = ["station", "queue", "door"];
    private static readonly string[] Weekdays = ["mon", "tue", "wed", "thu", "fri", "sat", "sun"];
    private static readonly JsonSerializerOptions Json = new(JsonSerializerDefaults.Web);

    [GeneratedRegex("^[a-z0-9_-]{1,40}$")] private static partial Regex SlugRx();
    [GeneratedRegex("^([01][0-9]|2[0-3]):[0-5][0-9]$")] private static partial Regex HhmmRx();

    // ── box key ─────────────────────────────────────────────────────────────

    public async Task<List<WatchBoxDto>> BoxesAsync(CancellationToken ct = default)
        => (await db.WatchBoxes.AsNoTracking().ToListAsync(ct))
            .Select(b => new WatchBoxDto(b.LocationId, true, b.LastSeenAt, SplitNames(b.CameraNames), b.Version)).ToList();

    /// <summary>A new key for a branch's box; an older key stops working. It starts with the cafe's short name and a dot.</summary>
    public async Task<Result<WatchBoxKeyDto>> CreateKeyAsync(Guid locationId, CancellationToken ct = default)
    {
        if (!await db.Locations.AnyAsync(l => l.Id == locationId, ct)) return Result<WatchBoxKeyDto>.NotFound("Branch not found.");
        var key = $"{tenant.Slug}.{Convert.ToBase64String(RandomNumberGenerator.GetBytes(32)).TrimEnd('=').Replace('+', '-').Replace('/', '_')}";
        var box = await db.WatchBoxes.FirstOrDefaultAsync(b => b.LocationId == locationId, ct);
        if (box is null) db.WatchBoxes.Add(box = new WatchBox { LocationId = locationId });
        box.KeyHash = Hash(key);
        box.LastSeenAt = null;
        await db.SaveChangesAsync(ct);
        await audit.LogAsync("WatchBoxKeyCreated", "Location", locationId.ToString(), null, ct);
        return Result<WatchBoxKeyDto>.Success(new WatchBoxKeyDto(locationId, key), 201);
    }

    public async Task<Result<bool>> RevokeKeyAsync(Guid locationId, CancellationToken ct = default)
    {
        var box = await db.WatchBoxes.FirstOrDefaultAsync(b => b.LocationId == locationId, ct);
        if (box is null) return Result<bool>.NotFound("This branch has no camera box key.");
        db.WatchBoxes.Remove(box);
        await db.SaveChangesAsync(ct);
        await audit.LogAsync("WatchBoxKeyRemoved", "Location", locationId.ToString(), null, ct);
        return Result<bool>.Success(true);
    }

    // ── cameras and zones ───────────────────────────────────────────────────

    public async Task<List<WatchCameraDto>> CamerasAsync(Guid locationId, CancellationToken ct = default)
        => (await db.WatchCameras.AsNoTracking().Where(c => c.LocationId == locationId).OrderBy(c => c.Name)
                .Select(c => new { c.Id, c.LocationId, c.Name, c.ZonesJson, c.SnapshotAt, c.SnapshotRequestedAt, c.SnapshotWidth, c.SnapshotHeight })
                .ToListAsync(ct))
            .Select(c => new WatchCameraDto(c.Id, c.LocationId, c.Name, ParseZones(c.ZonesJson), c.SnapshotAt, c.SnapshotRequestedAt, c.SnapshotWidth, c.SnapshotHeight))
            .ToList();

    public async Task<Result<WatchCameraDto>> CreateCameraAsync(SaveWatchCameraDto dto, CancellationToken ct = default)
    {
        if (!await db.Locations.AnyAsync(l => l.Id == dto.LocationId, ct)) return Result<WatchCameraDto>.NotFound("Branch not found.");
        var name = dto.Name?.Trim() ?? "";
        if (!SlugRx().IsMatch(name)) return Result<WatchCameraDto>.Failure("Camera name: 1-40 small latin letters, digits, - or _ (the same as in the box settings).");
        var existing = await db.WatchCameras.Where(c => c.LocationId == dto.LocationId).ToListAsync(ct);
        if (existing.Count >= MaxCameras) return Result<WatchCameraDto>.Failure($"A branch can have up to {MaxCameras} cameras.");
        if (existing.Any(c => c.Name == name)) return Result<WatchCameraDto>.Failure($"Camera \"{name}\" already exists.", 409);
        var camera = new WatchCamera { LocationId = dto.LocationId, Name = name, SnapshotRequestedAt = DateTime.UtcNow };
        var zones = dto.Zones ?? [];
        if (CheckZones(zones, existing.Where(c => c.Id != camera.Id)) is { } bad) return Result<WatchCameraDto>.Failure(bad);
        camera.ZonesJson = JsonSerializer.Serialize(Normalize(zones), Json);
        db.WatchCameras.Add(camera);
        await db.SaveChangesAsync(ct);
        return Result<WatchCameraDto>.Success(ToDto(camera), 201);
    }

    public async Task<Result<WatchCameraDto>> UpdateCameraAsync(Guid id, SaveWatchCameraDto dto, CancellationToken ct = default)
    {
        var camera = await db.WatchCameras.FirstOrDefaultAsync(c => c.Id == id, ct);
        if (camera is null) return Result<WatchCameraDto>.NotFound("Camera not found.");
        var name = dto.Name?.Trim() ?? "";
        if (!SlugRx().IsMatch(name)) return Result<WatchCameraDto>.Failure("Camera name: 1-40 small latin letters, digits, - or _ (the same as in the box settings).");
        var others = await db.WatchCameras.Where(c => c.LocationId == camera.LocationId && c.Id != id).ToListAsync(ct);
        if (others.Any(c => c.Name == name)) return Result<WatchCameraDto>.Failure($"Camera \"{name}\" already exists.", 409);
        var zones = dto.Zones ?? [];
        if (CheckZones(zones, others) is { } bad) return Result<WatchCameraDto>.Failure(bad);
        camera.Name = name;
        camera.ZonesJson = JsonSerializer.Serialize(Normalize(zones), Json);
        camera.UpdatedAt = DateTime.UtcNow;
        await db.SaveChangesAsync(ct);
        await audit.LogAsync("WatchZonesSaved", "WatchCamera", id.ToString(), $"{name}: {zones.Count} zone(s)", ct);
        return Result<WatchCameraDto>.Success(ToDto(camera));
    }

    public async Task<Result<bool>> DeleteCameraAsync(Guid id, CancellationToken ct = default)
    {
        var camera = await db.WatchCameras.FirstOrDefaultAsync(c => c.Id == id, ct);
        if (camera is null) return Result<bool>.NotFound("Camera not found.");
        db.WatchCameras.Remove(camera);
        await db.SaveChangesAsync(ct);
        return Result<bool>.Success(true);
    }

    /// <summary>Asks the box for a new still picture of this camera; it arrives within about a minute.</summary>
    public async Task<Result<WatchCameraDto>> RequestSnapshotAsync(Guid id, CancellationToken ct = default)
    {
        var camera = await db.WatchCameras.FirstOrDefaultAsync(c => c.Id == id, ct);
        if (camera is null) return Result<WatchCameraDto>.NotFound("Camera not found.");
        camera.SnapshotRequestedAt = DateTime.UtcNow;
        await db.SaveChangesAsync(ct);
        return Result<WatchCameraDto>.Success(ToDto(camera));
    }

    public async Task<byte[]?> SnapshotAsync(Guid id, CancellationToken ct = default)
        => await db.WatchCameras.AsNoTracking().Where(c => c.Id == id).Select(c => c.Snapshot).FirstOrDefaultAsync(ct);

    // ── settings and Telegram ───────────────────────────────────────────────

    public async Task<WatchSettingsDto> SettingsAsync(Guid locationId, CancellationToken ct = default)
    {
        var s = await db.WatchSettings.AsNoTracking().FirstOrDefaultAsync(x => x.LocationId == locationId, ct)
                ?? new WatchSettings { LocationId = locationId };
        var (alerts, detect) = ParseSettings(s.SettingsJson);
        var botSet = !string.IsNullOrWhiteSpace(await secrets.GetAsync(TelegramTokenKey, ct));
        return new WatchSettingsDto(locationId, s.Open, s.Close, alerts, detect, s.TelegramChatId, botSet);
    }

    public async Task<Result<WatchSettingsDto>> SaveSettingsAsync(SaveWatchSettingsDto dto, CancellationToken ct = default)
    {
        if (!await db.Locations.AnyAsync(l => l.Id == dto.LocationId, ct)) return Result<WatchSettingsDto>.NotFound("Branch not found.");
        if (!HhmmRx().IsMatch(dto.Open ?? "") || !HhmmRx().IsMatch(dto.Close ?? ""))
            return Result<WatchSettingsDto>.Failure("Opening hours must be HH:MM, for example 08:00.");
        var alerts = dto.Alerts ?? new WatchAlertSettings();
        int[] minutes = [alerts.EmptyWhileQueueMinutes, alerts.EmptyOnShiftMinutes, alerts.EmptyMinutes, alerts.LateGraceMinutes, alerts.PhoneMinutes, alerts.StillMinutes];
        if (minutes.Any(m => m < 0 || m > 240) || alerts.QueueMinGuests < 1 || alerts.QueueMinGuests > 30)
            return Result<WatchSettingsDto>.Failure("Alert minutes must be 0-240 (0 = off), guests waiting 1-30.");
        var chat = dto.TelegramChatId?.Trim();
        if (!string.IsNullOrEmpty(chat) && !long.TryParse(chat, out _) && !chat.StartsWith('@'))
            return Result<WatchSettingsDto>.Failure("Telegram chat id is a number like 123456789 or -1001234567890.");

        var s = await db.WatchSettings.FirstOrDefaultAsync(x => x.LocationId == dto.LocationId, ct);
        if (s is null) db.WatchSettings.Add(s = new WatchSettings { LocationId = dto.LocationId });
        s.Open = dto.Open!;
        s.Close = dto.Close!;
        s.SettingsJson = JsonSerializer.Serialize(new { alerts, detect = dto.Detect ?? new WatchDetectSettings() }, Json);
        s.TelegramChatId = string.IsNullOrEmpty(chat) ? null : chat;
        s.UpdatedAt = DateTime.UtcNow;
        await db.SaveChangesAsync(ct);
        await audit.LogAsync("WatchSettingsSaved", "Location", dto.LocationId.ToString(), null, ct);
        return Result<WatchSettingsDto>.Success(await SettingsAsync(dto.LocationId, ct));
    }

    /// <summary>The cafe's own Telegram bot token (from @BotFather), kept encrypted. Empty clears it.</summary>
    public async Task<Result<bool>> SetTelegramTokenAsync(WatchTelegramTokenDto dto, CancellationToken ct = default)
    {
        var token = dto.Token?.Trim() ?? "";
        if (token.Length == 0)
        {
            await secrets.ClearAsync(tenant.TenantId, TelegramTokenKey, ct);
            return Result<bool>.Success(true);
        }
        if (!TokenRx().IsMatch(token)) return Result<bool>.Failure("This does not look like a bot token from @BotFather (123456:ABC...).");
        try
        {
            await secrets.SetAsync(tenant.TenantId, TelegramTokenKey, token, ct);
        }
        catch (InvalidOperationException)
        {
            return Result<bool>.Failure("The server cannot store secrets yet: the platform operator must set Secrets__MasterKey.", 409);
        }
        await audit.LogAsync("WatchTelegramTokenSet", "Tenant", tenant.TenantId.ToString(), null, ct);
        return Result<bool>.Success(true);
    }

    [GeneratedRegex(@"^\d{5,15}:[A-Za-z0-9_-]{30,60}$")] private static partial Regex TokenRx();

    /// <summary>Finds the chat that last wrote to the bot and saves it for the branch, then sends a test message there.</summary>
    public async Task<Result<WatchSettingsDto>> FindTelegramChatAsync(Guid locationId, CancellationToken ct = default)
    {
        var token = await secrets.GetAsync(TelegramTokenKey, ct);
        if (string.IsNullOrWhiteSpace(token)) return Result<WatchSettingsDto>.Failure("Save the bot token first.");
        var chat = await telegram.LatestChatAsync(token, ct);
        if (chat is null) return Result<WatchSettingsDto>.Failure("No message found. Open your bot in Telegram, send it any message, then try again.", 404);
        var s = await db.WatchSettings.FirstOrDefaultAsync(x => x.LocationId == locationId, ct);
        if (s is null)
        {
            if (!await db.Locations.AnyAsync(l => l.Id == locationId, ct)) return Result<WatchSettingsDto>.NotFound("Branch not found.");
            db.WatchSettings.Add(s = new WatchSettings { LocationId = locationId });
        }
        s.TelegramChatId = chat;
        await db.SaveChangesAsync(ct);
        await telegram.SendAsync(token, chat, "✅ Yurt: staff watch messages for this branch will come here.", ct);
        return Result<WatchSettingsDto>.Success(await SettingsAsync(locationId, ct));
    }

    public async Task<Result<bool>> TestTelegramAsync(Guid locationId, CancellationToken ct = default)
    {
        var (token, chat) = await TelegramForAsync(locationId, ct);
        if (token is null || chat is null) return Result<bool>.Failure("Set the bot token and the chat first.");
        return await telegram.SendAsync(token, chat, "✅ Yurt: test message from staff watch.", ct)
            ? Result<bool>.Success(true)
            : Result<bool>.Failure("Telegram did not take the message. Check the token, and that the chat wrote to the bot.", 502);
    }

    // ── shift plan ──────────────────────────────────────────────────────────

    public async Task<List<WatchShiftDto>> ShiftsAsync(Guid locationId, CancellationToken ct = default)
        => await db.WatchShifts.AsNoTracking().Where(s => s.LocationId == locationId).OrderBy(s => s.SortOrder)
            .Select(s => new WatchShiftDto(s.Day, s.Name, s.Station, s.Start, s.End)).ToListAsync(ct);

    /// <summary>Replaces the branch's whole shift plan.</summary>
    public async Task<Result<List<WatchShiftDto>>> SaveShiftsAsync(SaveWatchShiftsDto dto, CancellationToken ct = default)
    {
        if (!await db.Locations.AnyAsync(l => l.Id == dto.LocationId, ct)) return Result<List<WatchShiftDto>>.NotFound("Branch not found.");
        var rows = dto.Rows ?? [];
        if (rows.Count > MaxShiftRows) return Result<List<WatchShiftDto>>.Failure($"Up to {MaxShiftRows} rows.");
        var clean = new List<WatchShiftDto>();
        foreach (var (r, i) in rows.Select((r, i) => (r, i + 1)))
        {
            var day = r.Day?.Trim().ToLowerInvariant() ?? "";
            var okDay = day == "daily" || Weekdays.Contains(day) || DateOnly.TryParseExact(day, "yyyy-MM-dd", out _);
            var name = r.Name?.Trim() ?? "";
            var station = r.Station?.Trim() ?? "";
            if (!okDay) return Result<List<WatchShiftDto>>.Failure($"Row {i}: day must be daily, mon…sun or a date like 2026-11-12.");
            if (name.Length is 0 or > 60) return Result<List<WatchShiftDto>>.Failure($"Row {i}: name is required (up to 60 characters).");
            if (!SlugRx().IsMatch(station)) return Result<List<WatchShiftDto>>.Failure($"Row {i}: pick a station.");
            if (!HhmmRx().IsMatch(r.Start ?? "") || !HhmmRx().IsMatch(r.End ?? "") || r.Start == r.End)
                return Result<List<WatchShiftDto>>.Failure($"Row {i}: start and end are HH:MM and differ.");
            clean.Add(new WatchShiftDto(day, name, station, r.Start!, r.End!));
        }
        db.WatchShifts.RemoveRange(await db.WatchShifts.Where(s => s.LocationId == dto.LocationId).ToListAsync(ct));
        db.WatchShifts.AddRange(clean.Select((r, i) => new WatchShift
            { LocationId = dto.LocationId, Day = r.Day, Name = r.Name, Station = r.Station, Start = r.Start, End = r.End, SortOrder = i }));
        await db.SaveChangesAsync(ct);
        await audit.LogAsync("WatchShiftsSaved", "Location", dto.LocationId.ToString(), $"{clean.Count} row(s)", ct);
        return Result<List<WatchShiftDto>>.Success(clean);
    }

    // ── the day ─────────────────────────────────────────────────────────────

    /// <summary>The daily report of a day (when the box sent it), its alerts, visitors and orders per hour, and minutes so far.</summary>
    public async Task<Result<WatchDayDto>> DayAsync(Guid locationId, string? day, CancellationToken ct = default)
    {
        var d = DateOnly.TryParseExact(day ?? "", "yyyy-MM-dd", out var parsed) ? parsed : DateOnly.FromDateTime(ToLocal(DateTime.UtcNow));
        var fromUtc = ToUtc(d.ToDateTime(TimeOnly.MinValue));
        var toUtc = ToUtc(d.AddDays(1).ToDateTime(TimeOnly.MinValue));

        var report = await db.WatchReports.AsNoTracking().FirstOrDefaultAsync(r => r.LocationId == locationId && r.Day == d, ct);
        var alerts = await db.WatchAlerts.AsNoTracking().Where(a => a.LocationId == locationId && a.At >= fromUtc && a.At < toUtc)
            .OrderBy(a => a.At).Select(a => new { a.At, a.Kind, a.Zone, a.Text }).ToListAsync(ct);
        var minutes = await db.WatchMinutes.AsNoTracking().Where(m => m.LocationId == locationId && m.Minute >= fromUtc && m.Minute < toUtc)
            .Select(m => new { m.Minute, m.Zone, m.Seconds, m.Occupied, m.Phone, m.Still, m.Entered }).ToListAsync(ct);
        var orders = await db.Orders.AsNoTracking().Where(o => o.LocationId == locationId && o.CreatedAt >= fromUtc && o.CreatedAt < toUtc && o.Status != OrderStatus.Declined)
            .Select(o => o.CreatedAt).ToListAsync(ct);

        var cameras = await db.WatchCameras.AsNoTracking().Where(c => c.LocationId == locationId).Select(c => c.ZonesJson).ToListAsync(ct);
        var zones = cameras.SelectMany(ParseZones).ToList();
        var doors = zones.Where(z => z.Kind == "door").Select(z => z.Name).ToHashSet();
        var stations = zones.Where(z => z.Kind == "station").Select(z => z.Name).ToList();

        var visitorsByHour = minutes.Where(m => doors.Contains(m.Zone)).GroupBy(m => ToLocal(m.Minute).Hour).ToDictionary(g => g.Key, g => g.Sum(m => m.Entered));
        var ordersByHour = orders.GroupBy(o => ToLocal(o).Hour).ToDictionary(g => g.Key, g => g.Count());
        var hours = visitorsByHour.Keys.Union(ordersByHour.Keys).OrderBy(h => h)
            .Select(h => new WatchHourDto(h, visitorsByHour.GetValueOrDefault(h), ordersByHour.GetValueOrDefault(h))).ToList();

        var live = stations.Select(z =>
        {
            var mine = minutes.Where(m => m.Zone == z && m.Seconds >= 20).ToList();
            var covered = mine.Where(m => m.Occupied * 2 >= m.Seconds).ToList();
            return new WatchStationLiveDto(z, mine.Count, covered.Count,
                covered.Count(m => m.Phone * 2 >= m.Occupied), covered.Count(m => m.Still * 10 >= m.Occupied * 8));
        }).ToList();

        JsonElement? data = null;
        if (report is not null)
        {
            try { data = JsonDocument.Parse(report.DataJson).RootElement.Clone(); }
            catch (JsonException) { data = null; }
        }
        return Result<WatchDayDto>.Success(new WatchDayDto(
            locationId, d.ToString("yyyy-MM-dd", CultureInfo.InvariantCulture), report?.Text, data,
            alerts.Select(a => new WatchAlertDto(ToLocal(a.At), a.Kind, a.Zone, a.Text)).ToList(),
            hours, visitorsByHour.Values.Sum(), orders.Count, live));
    }

    // ── the box ─────────────────────────────────────────────────────────────

    /// <summary>Settings for the box: zones per camera, shift plan, hours, alert settings, and which cameras to take a picture of.</summary>
    public async Task<Result<object>> AgentConfigAsync(string? key, string? cameras, string? version, CancellationToken ct = default)
    {
        var box = await FindBoxAsync(key, ct);
        if (box is null) return Result<object>.Unauthorized("Unknown camera box key.");
        var names = SplitNames(cameras).Where(n => SlugRx().IsMatch(n)).Take(MaxCameras).ToList();
        box.LastSeenAt = DateTime.UtcNow;
        box.CameraNames = string.Join(',', names);
        box.Version = version is { Length: <= 20 } ? version : null;

        // cameras the box has but nobody added yet: add them and ask for a picture, so zones can be drawn right away
        var known = await db.WatchCameras.Where(c => c.LocationId == box.LocationId).ToListAsync(ct);
        foreach (var n in names.Where(n => known.All(c => c.Name != n)).Take(MaxCameras - known.Count))
        {
            var cam = new WatchCamera { LocationId = box.LocationId, Name = n, SnapshotRequestedAt = DateTime.UtcNow };
            db.WatchCameras.Add(cam);
            known.Add(cam);
        }
        await db.SaveChangesAsync(ct);

        var location = await db.Locations.AsNoTracking().Where(l => l.Id == box.LocationId).Select(l => l.Name).FirstAsync(ct);
        var s = await db.WatchSettings.AsNoTracking().FirstOrDefaultAsync(x => x.LocationId == box.LocationId, ct) ?? new WatchSettings();
        var (alerts, detect) = ParseSettings(s.SettingsJson);
        var shifts = await ShiftsAsync(box.LocationId, ct);
        return Result<object>.Success(new Dictionary<string, object?>
        {
            ["cafe"] = location,
            ["timezone"] = "Asia/Almaty",
            ["open"] = s.Open,
            ["close"] = s.Close,
            ["language"] = "ru",
            ["alerts"] = new Dictionary<string, int>
            {
                ["empty_while_queue_minutes"] = alerts.EmptyWhileQueueMinutes,
                ["queue_min_guests"] = alerts.QueueMinGuests,
                ["empty_on_shift_minutes"] = alerts.EmptyOnShiftMinutes,
                ["empty_minutes"] = alerts.EmptyMinutes,
                ["late_grace_minutes"] = alerts.LateGraceMinutes,
                ["phone_minutes"] = alerts.PhoneMinutes,
                ["still_minutes"] = alerts.StillMinutes,
            },
            ["detect"] = new Dictionary<string, bool> { ["phones"] = detect.Phones, ["still"] = detect.Still },
            ["cameras"] = known.OrderBy(c => c.Name).Select(c => new { name = c.Name, zones = ParseZones(c.ZonesJson) }).ToList(),
            ["shifts"] = shifts,
            ["snapshotRequests"] = known.Where(c => c.SnapshotRequestedAt != null && names.Contains(c.Name)).Select(c => c.Name).ToList(),
        });
    }

    public async Task<Result<int>> AgentMinutesAsync(string? key, AgentMinutesDto dto, CancellationToken ct = default)
    {
        var box = await FindBoxAsync(key, ct);
        if (box is null) return Result<int>.Unauthorized("Unknown camera box key.");
        var rows = (dto.Minutes ?? []).Take(MaxBatch).ToList();
        var parsed = new List<WatchMinute>();
        foreach (var r in rows)
        {
            if (!TryLocal(r.Minute, out var local) || !SlugRx().IsMatch(r.Zone ?? "")) continue;
            if (r.Seconds is < 0 or > 60 || r.Occupied < 0 || r.Occupied > r.Seconds) continue;
            parsed.Add(new WatchMinute
            {
                LocationId = box.LocationId, Minute = ToUtc(local), Zone = r.Zone!, Seconds = r.Seconds, Occupied = r.Occupied,
                PeopleSum = Math.Clamp(r.PeopleSum, 0, 60 * 100), PeopleMax = Math.Clamp(r.PeopleMax, 0, 100),
                Phone = Math.Clamp(r.Phone, 0, 60), Still = Math.Clamp(r.Still, 0, 60),
                Entered = Math.Clamp(r.Entered, 0, 1000), Left = Math.Clamp(r.Left, 0, 1000),
            });
        }
        if (parsed.Count == 0) return Result<int>.Success(0);
        var from = parsed.Min(m => m.Minute);
        var to = parsed.Max(m => m.Minute);
        var existing = (await db.WatchMinutes.Where(m => m.LocationId == box.LocationId && m.Minute >= from && m.Minute <= to).ToListAsync(ct))
            .ToDictionary(m => (m.Minute, m.Zone));
        foreach (var m in parsed.GroupBy(m => (m.Minute, m.Zone)).Select(g => g.Last()))
        {
            if (existing.TryGetValue((m.Minute, m.Zone), out var row))
            {
                row.Seconds = m.Seconds; row.Occupied = m.Occupied; row.PeopleSum = m.PeopleSum; row.PeopleMax = m.PeopleMax;
                row.Phone = m.Phone; row.Still = m.Still; row.Entered = m.Entered; row.Left = m.Left;
            }
            else db.WatchMinutes.Add(m);
        }
        await db.SaveChangesAsync(ct);
        return Result<int>.Success(parsed.Count);
    }

    /// <summary>Stores the box's alerts and forwards each one to the branch's Telegram chat.</summary>
    public async Task<Result<int>> AgentAlertsAsync(string? key, AgentAlertsDto dto, CancellationToken ct = default)
    {
        var box = await FindBoxAsync(key, ct);
        if (box is null) return Result<int>.Unauthorized("Unknown camera box key.");
        var (token, chat) = await TelegramForAsync(box.LocationId, ct);
        var location = await db.Locations.AsNoTracking().Where(l => l.Id == box.LocationId).Select(l => l.Name).FirstAsync(ct);
        var n = 0;
        foreach (var a in (dto.Alerts ?? []).Take(100))
        {
            if (!TryLocal(a.At, out var local) || string.IsNullOrWhiteSpace(a.Text)) continue;
            var alert = new WatchAlert
            {
                LocationId = box.LocationId, At = ToUtc(local), Kind = Cut(a.Kind, 40), Zone = Cut(a.Zone, 40), Text = Cut(a.Text, 1000),
            };
            if (token is not null && chat is not null)
                alert.SentToTelegram = await telegram.SendAsync(token, chat, $"📍 {location}\n{alert.Text}", ct);
            db.WatchAlerts.Add(alert);
            n++;
        }
        await db.SaveChangesAsync(ct);
        return Result<int>.Success(n);
    }

    /// <summary>Stores the daily report (a later one for the same day replaces it), sends it to Telegram and drops data older than 90 days.</summary>
    public async Task<Result<bool>> AgentReportAsync(string? key, AgentReportDto dto, CancellationToken ct = default)
    {
        var box = await FindBoxAsync(key, ct);
        if (box is null) return Result<bool>.Unauthorized("Unknown camera box key.");
        if (!DateOnly.TryParseExact(dto.Day ?? "", "yyyy-MM-dd", out var day)) return Result<bool>.Failure("Day must be yyyy-MM-dd.");
        if (string.IsNullOrWhiteSpace(dto.Text)) return Result<bool>.Failure("The report is empty.");
        var raw = dto.Data.ValueKind == JsonValueKind.Object ? dto.Data.GetRawText() : "{}";
        if (raw.Length > 200_000) return Result<bool>.Failure("The report is too large.");

        var report = await db.WatchReports.FirstOrDefaultAsync(r => r.LocationId == box.LocationId && r.Day == day, ct);
        if (report is null) db.WatchReports.Add(report = new WatchReport { LocationId = box.LocationId, Day = day });
        report.Text = Cut(dto.Text, 8000);
        report.DataJson = raw;
        report.UpdatedAt = DateTime.UtcNow;
        await db.SaveChangesAsync(ct);

        var (token, chat) = await TelegramForAsync(box.LocationId, ct);
        if (token is not null && chat is not null) await telegram.SendAsync(token, chat, report.Text, ct);

        var cut = DateTime.UtcNow.AddDays(-KeepDays);
        await db.WatchMinutes.Where(m => m.LocationId == box.LocationId && m.Minute < cut).ExecuteDeleteAsync(ct);
        await db.WatchAlerts.Where(a => a.LocationId == box.LocationId && a.At < cut).ExecuteDeleteAsync(ct);
        return Result<bool>.Success(true);
    }

    public async Task<Result<bool>> AgentSnapshotAsync(string? key, AgentSnapshotDto dto, CancellationToken ct = default)
    {
        var box = await FindBoxAsync(key, ct);
        if (box is null) return Result<bool>.Unauthorized("Unknown camera box key.");
        byte[] jpeg;
        try { jpeg = Convert.FromBase64String(dto.JpegBase64 ?? ""); }
        catch (FormatException) { return Result<bool>.Failure("The picture is not valid base64."); }
        if (jpeg.Length is < 100 or > MaxSnapshotBytes || jpeg[0] != 0xFF || jpeg[1] != 0xD8)
            return Result<bool>.Failure("The picture must be a JPEG up to 2 MB.");
        var camera = await db.WatchCameras.FirstOrDefaultAsync(c => c.LocationId == box.LocationId && c.Name == dto.Camera, ct);
        if (camera is null) return Result<bool>.NotFound("Camera not found.");
        camera.Snapshot = jpeg;
        camera.SnapshotWidth = Math.Clamp(dto.Width, 0, 10000);
        camera.SnapshotHeight = Math.Clamp(dto.Height, 0, 10000);
        camera.SnapshotAt = DateTime.UtcNow;
        camera.SnapshotRequestedAt = null;
        await db.SaveChangesAsync(ct);
        return Result<bool>.Success(true);
    }

    // ── helpers ─────────────────────────────────────────────────────────────

    private async Task<WatchBox?> FindBoxAsync(string? key, CancellationToken ct)
    {
        if (string.IsNullOrWhiteSpace(key) || key.Length > 200) return null;
        var hash = Hash(key.Trim());
        return await db.WatchBoxes.FirstOrDefaultAsync(b => b.KeyHash == hash, ct);
    }

    private async Task<(string? Token, string? Chat)> TelegramForAsync(Guid locationId, CancellationToken ct)
    {
        var chat = await db.WatchSettings.AsNoTracking().Where(s => s.LocationId == locationId).Select(s => s.TelegramChatId).FirstOrDefaultAsync(ct);
        if (string.IsNullOrWhiteSpace(chat)) return (null, null);
        var token = await secrets.GetAsync(TelegramTokenKey, ct);
        if (string.IsNullOrWhiteSpace(token))
        {
            logger.LogInformation("Staff watch: branch {LocationId} has a Telegram chat but the cafe has no bot token", locationId);
            return (null, null);
        }
        return (token, chat);
    }

    private static string? CheckZones(List<WatchZoneDto> zones, IEnumerable<WatchCamera> otherCameras)
    {
        if (zones.Count > MaxZonesPerCamera) return $"Up to {MaxZonesPerCamera} zones per camera.";
        var taken = otherCameras.SelectMany(c => ParseZones(c.ZonesJson)).Select(z => z.Name).ToHashSet();
        var names = new HashSet<string>();
        foreach (var z in zones)
        {
            var name = z.Name?.Trim() ?? "";
            if (!SlugRx().IsMatch(name)) return "Zone name: 1-40 small latin letters, digits, - or _.";
            if (!names.Add(name) || taken.Contains(name)) return $"Zone \"{name}\" is used twice. Zone names must differ across all cameras of the branch.";
            if (!Kinds.Contains(z.Kind)) return $"Zone \"{name}\": kind must be station, queue or door.";
            if ((z.Label ?? "").Length > 40) return $"Zone \"{name}\": label up to 40 characters.";
            var pts = z.Points ?? [];
            if (pts.Any(p => p is null || p.Length != 2 || p.Any(v => double.IsNaN(v) || v < 0 || v > 100)))
                return $"Zone \"{name}\": points must be inside the picture.";
            if (z.Kind == "door")
            {
                if (pts.Count != 2) return $"Zone \"{name}\": a door is a line of 2 points.";
                if (z.Inside is not { Length: 2 } inside || inside.Any(v => v < 0 || v > 100)) return $"Zone \"{name}\": mark which side is inside.";
                var side = (pts[1][0] - pts[0][0]) * (inside[1] - pts[0][1]) - (pts[1][1] - pts[0][1]) * (inside[0] - pts[0][0]);
                if (Math.Abs(side) < 1e-6) return $"Zone \"{name}\": the inside mark must not lie on the door line.";
            }
            else if (pts.Count is < 3 or > 20) return $"Zone \"{name}\": needs 3 to 20 corners.";
        }
        var all = zones.Select(z => z.Name.Trim()).Concat(taken).ToHashSet();
        foreach (var z in zones.Where(z => z.Kind == "station" && !string.IsNullOrWhiteSpace(z.Queue)))
            if (!all.Contains(z.Queue!.Trim())) return $"Zone \"{z.Name}\": queue zone \"{z.Queue}\" does not exist.";
        return null;
    }

    private static List<WatchZoneDto> Normalize(List<WatchZoneDto> zones)
        => zones.Select(z => new WatchZoneDto(
            z.Name.Trim(), string.IsNullOrWhiteSpace(z.Label) ? null : z.Label.Trim(), z.Kind,
            z.Points.Select(p => new[] { Math.Round(p[0], 2), Math.Round(p[1], 2) }).ToList(),
            z.Kind == "station" && !string.IsNullOrWhiteSpace(z.Queue) ? z.Queue.Trim() : null,
            z.Kind == "door" ? z.Inside!.Select(v => Math.Round(v, 2)).ToArray() : null)).ToList();

    private static List<WatchZoneDto> ParseZones(string json)
    {
        try { return JsonSerializer.Deserialize<List<WatchZoneDto>>(json, Json) ?? []; }
        catch (JsonException) { return []; }
    }

    private static (WatchAlertSettings, WatchDetectSettings) ParseSettings(string json)
    {
        try
        {
            using var doc = JsonDocument.Parse(string.IsNullOrWhiteSpace(json) ? "{}" : json);
            var alerts = doc.RootElement.TryGetProperty("alerts", out var a) ? a.Deserialize<WatchAlertSettings>(Json) : null;
            var detect = doc.RootElement.TryGetProperty("detect", out var d) ? d.Deserialize<WatchDetectSettings>(Json) : null;
            return (alerts ?? new WatchAlertSettings(), detect ?? new WatchDetectSettings());
        }
        catch (JsonException) { return (new WatchAlertSettings(), new WatchDetectSettings()); }
    }

    private static WatchCameraDto ToDto(WatchCamera c)
        => new(c.Id, c.LocationId, c.Name, ParseZones(c.ZonesJson), c.SnapshotAt, c.SnapshotRequestedAt, c.SnapshotWidth, c.SnapshotHeight);

    private static List<string> SplitNames(string? csv)
        => (csv ?? "").Split(',', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries).Distinct().ToList();

    private static bool TryLocal(string? text, out DateTime local)
        => DateTime.TryParseExact(text ?? "", ["yyyy-MM-ddTHH:mm", "yyyy-MM-ddTHH:mm:ss", "yyyy-MM-dd HH:mm"],
            CultureInfo.InvariantCulture, DateTimeStyles.None, out local);

    public static DateTime ToUtc(DateTime local) => TimeZoneInfo.ConvertTimeToUtc(DateTime.SpecifyKind(local, DateTimeKind.Unspecified), CafeTime);

    public static DateTime ToLocal(DateTime utc) => TimeZoneInfo.ConvertTimeFromUtc(DateTime.SpecifyKind(utc, DateTimeKind.Utc), CafeTime);

    private static string Cut(string? s, int max) => (s ?? "").Length <= max ? s ?? "" : s![..max];

    private static string Hash(string key) => Convert.ToHexStringLower(SHA256.HashData(Encoding.UTF8.GetBytes(key)));
}
