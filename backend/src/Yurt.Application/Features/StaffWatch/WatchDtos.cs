using System.Text.Json;

namespace Yurt.Application.Features.StaffWatch;

/// <summary>A zone drawn on a camera picture. Coordinates are percent of the picture (0..100).</summary>
public record WatchZoneDto(string Name, string? Label, string Kind, List<double[]> Points, string? Queue = null, double[]? Inside = null);

public record WatchCameraDto(
    Guid Id, Guid LocationId, string Name, List<WatchZoneDto> Zones,
    DateTime? SnapshotAt, DateTime? SnapshotRequestedAt, int SnapshotWidth, int SnapshotHeight);

public record SaveWatchCameraDto(Guid LocationId, string Name, List<WatchZoneDto>? Zones);

/// <summary>Alert settings. <c>QueueWaitSeconds</c> (guests waiting with nobody at the station) replaces
/// <c>EmptyWhileQueueMinutes</c>, which is kept, rounded up, for boxes from before the seconds-based alerts.</summary>
public record WatchAlertSettings(
    int EmptyWhileQueueMinutes = 3, int QueueMinGuests = 2, int EmptyOnShiftMinutes = 10, int EmptyMinutes = 15,
    int LateGraceMinutes = 10, int PhoneMinutes = 5, int StillMinutes = 10,
    int QueueWaitSeconds = 90, int AwayGraceSeconds = 20);

public record WatchDetectSettings(bool Phones = true, bool Still = true);

public record WatchSettingsDto(
    Guid LocationId, string Open, string Close, WatchAlertSettings Alerts, WatchDetectSettings Detect,
    string? TelegramChatId, bool TelegramBotSet);

public record SaveWatchSettingsDto(
    Guid LocationId, string Open, string Close, WatchAlertSettings? Alerts, WatchDetectSettings? Detect, string? TelegramChatId);

public record WatchTelegramTokenDto(string? Token);

public record WatchShiftDto(string Day, string Name, string Station, string Start, string End);

public record SaveWatchShiftsDto(Guid LocationId, List<WatchShiftDto>? Rows);

/// <summary><c>Online</c>: the box checked in within the last 3 minutes (it does so every minute).</summary>
public record WatchBoxDto(Guid LocationId, bool HasKey, DateTime? LastSeenAt, List<string> CameraNames, string? Version, bool Online = false);

/// <summary>A new box key. It is shown once; only its hash is kept.</summary>
public record WatchBoxKeyDto(Guid LocationId, string Key);

/// <summary>An alert, with its time in the cafe's local time.</summary>
public record WatchAlertDto(DateTime At, string Kind, string Zone, string Text, Guid? Id = null, bool HasPicture = false);

/// <summary>The branch right now. <c>State</c> is what the box last sent (null when it never did); <c>At</c> is the
/// cafe's local time of that; <c>Online</c> as in <see cref="WatchBoxDto"/>.</summary>
public record WatchLiveDto(Guid LocationId, bool HasBox, bool Online, DateTime? LastSeenAt, DateTime? At, JsonElement? State);

public record WatchHourDto(int Hour, int Visitors, int Orders);

/// <summary>Minutes so far for one station (for today, before the daily report exists).</summary>
public record WatchStationLiveDto(string Zone, int CameraMinutes, int CoveredMinutes, int PhoneMinutes, int StillMinutes);

public record WatchDayDto(
    Guid LocationId, string Day, string? ReportText, JsonElement? ReportData, List<WatchAlertDto> Alerts,
    List<WatchHourDto> Hours, int Visitors, int Orders, List<WatchStationLiveDto> Stations);

// ── what the box sends ──────────────────────────────────────────────────────

/// <summary>One minute of one zone; <c>Minute</c> is the cafe's local time, "yyyy-MM-ddTHH:mm".</summary>
public record AgentMinuteDto(
    string Minute, string Zone, int Seconds, int Occupied, int PeopleSum, int PeopleMax,
    int Phone = 0, int Still = 0, int Entered = 0, int Left = 0);

public record AgentMinutesDto(List<AgentMinuteDto>? Minutes);

/// <summary><c>JpegBase64</c>: an optional small picture (up to 300 KB), heads blurred by the box.</summary>
public record AgentAlertDto(string At, string Kind, string Zone, string Text, string? JpegBase64 = null);

public record AgentAlertsDto(List<AgentAlertDto>? Alerts);

public record AgentReportDto(string Day, string Text, JsonElement Data);

public record AgentSnapshotDto(string Camera, int Width, int Height, string JpegBase64);
