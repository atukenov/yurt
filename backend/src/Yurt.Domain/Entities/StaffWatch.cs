using Yurt.Domain.Common;

namespace Yurt.Domain.Entities;

// Staff watch (module staff_watch): a camera box in the branch counts people in zones drawn on its cameras (bar, till,
// queue, a door line), notices a phone in hand or nobody moving, and sends minute counts, alerts and a daily report.
// Video and camera passwords stay in the cafe; only numbers, alert texts and (on request) a still picture with heads
// blurred come here.

/// <summary>The camera box of one branch. It signs in with a key; only a hash of the key is stored.</summary>
public class WatchBox : BaseEntity, ITenantOwned
{
    public Guid TenantId { get; set; }
    public Guid LocationId { get; set; }
    public Location Location { get; set; } = null!;

    /// <summary>SHA-256 of the key, in hex.</summary>
    public string KeyHash { get; set; } = string.Empty;

    public DateTime? LastSeenAt { get; set; }

    /// <summary>The camera names the box has in its own settings, as it last said (comma-separated).</summary>
    public string? CameraNames { get; set; }

    public string? Version { get; set; }
}

/// <summary>A camera of a branch and the zones drawn on it (JSON list, coordinates in percent of the picture).</summary>
public class WatchCamera : BaseEntity, ITenantOwned
{
    public Guid TenantId { get; set; }
    public Guid LocationId { get; set; }
    public Location Location { get; set; } = null!;

    /// <summary>Must match the camera name in the box's settings file.</summary>
    public string Name { get; set; } = string.Empty;

    /// <summary>[{ name, label, kind: station|queue|door, points: [[x,y],...], queue?, inside?: [x,y] }]</summary>
    public string ZonesJson { get; set; } = "[]";

    /// <summary>The last still picture (JPEG, heads blurred by the box), used to draw zones. Replaced by the next one.</summary>
    public byte[]? Snapshot { get; set; }
    public int SnapshotWidth { get; set; }
    public int SnapshotHeight { get; set; }
    public DateTime? SnapshotAt { get; set; }

    /// <summary>Set when someone asks for a new picture; the box sends one within a minute and this is cleared.</summary>
    public DateTime? SnapshotRequestedAt { get; set; }
}

/// <summary>Opening hours and alert settings of one branch for staff watch.</summary>
public class WatchSettings : BaseEntity, ITenantOwned
{
    public Guid TenantId { get; set; }
    public Guid LocationId { get; set; }
    public Location Location { get; set; } = null!;

    public string Open { get; set; } = "08:00";
    public string Close { get; set; } = "22:00";

    /// <summary>{ alerts: {...minutes}, detect: { phones, still, ... } } as the box reads it.</summary>
    public string SettingsJson { get; set; } = "{}";

    /// <summary>Telegram chat that gets this branch's alerts and daily report (the bot token is a cafe secret).</summary>
    public string? TelegramChatId { get; set; }
}

/// <summary>A row of the shift plan: who works at which station when. Names come only from here, never from the camera.</summary>
public class WatchShift : BaseEntity, ITenantOwned
{
    public Guid TenantId { get; set; }
    public Guid LocationId { get; set; }
    public Location Location { get; set; } = null!;

    /// <summary>"daily", a weekday (mon..sun) or a date (2026-11-12).</summary>
    public string Day { get; set; } = "daily";
    public string Name { get; set; } = string.Empty;
    public string Station { get; set; } = string.Empty;
    public string Start { get; set; } = "08:00";
    public string End { get; set; } = "16:00";
    public int SortOrder { get; set; }
}

/// <summary>One minute of one zone, as counted by the box.</summary>
public class WatchMinute : ITenantOwned
{
    public long Id { get; set; }
    public Guid TenantId { get; set; }
    public Guid LocationId { get; set; }

    /// <summary>Start of the minute, UTC.</summary>
    public DateTime Minute { get; set; }
    public string Zone { get; set; } = string.Empty;

    /// <summary>Seconds with a camera picture.</summary>
    public int Seconds { get; set; }
    /// <summary>Seconds with at least one person in the zone.</summary>
    public int Occupied { get; set; }
    public int PeopleSum { get; set; }
    public int PeopleMax { get; set; }
    /// <summary>Seconds someone in the zone held a phone.</summary>
    public int Phone { get; set; }
    /// <summary>Seconds people were in the zone and nobody moved.</summary>
    public int Still { get; set; }
    /// <summary>Door line: people who walked in / out.</summary>
    public int Entered { get; set; }
    public int Left { get; set; }
}

/// <summary>An alert the box raised (station empty while guests wait, late, phone, not moving, camera down...).</summary>
public class WatchAlert : BaseEntity, ITenantOwned
{
    public Guid TenantId { get; set; }
    public Guid LocationId { get; set; }
    public DateTime At { get; set; }
    public string Kind { get; set; } = string.Empty;
    public string Zone { get; set; } = string.Empty;
    public string Text { get; set; } = string.Empty;
    public bool SentToTelegram { get; set; }

    /// <summary>A small picture (JPEG, heads blurred by the box) for alerts where seeing the scene helps: guests
    /// waiting with nobody at the station. Kept as long as the alert.</summary>
    public byte[]? Picture { get; set; }
}

/// <summary>What the box sees right now in one branch (stations staffed or away, guests waiting, visitors today).
/// One row per branch, replaced on each update; the box sends it only when something changes.</summary>
public class WatchLive : BaseEntity, ITenantOwned
{
    public Guid TenantId { get; set; }
    public Guid LocationId { get; set; }

    /// <summary>{ stations: [...], queues: [...], visitorsIn, visitorsOut } as the box sent it.</summary>
    public string StateJson { get; set; } = "{}";

    /// <summary>When the box saw this, UTC.</summary>
    public DateTime At { get; set; }
}

/// <summary>The daily report the box builds after closing: the text and the numbers behind it (JSON).</summary>
public class WatchReport : BaseEntity, ITenantOwned
{
    public Guid TenantId { get; set; }
    public Guid LocationId { get; set; }
    public DateOnly Day { get; set; }
    public string Text { get; set; } = string.Empty;
    public string DataJson { get; set; } = "{}";
}
