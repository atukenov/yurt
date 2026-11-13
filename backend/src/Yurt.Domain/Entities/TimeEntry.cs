using Yurt.Domain.Common;

namespace Yurt.Domain.Entities;

/// <summary>
/// One stretch of work of one staff member (module <c>own_pos</c>): clocked in with the staff PIN at the till, clocked
/// out the same way. Open while <see cref="ClockOutAt"/> is empty.
/// </summary>
public class TimeEntry : BaseEntity, ITenantOwned
{
    public Guid TenantId { get; set; }

    public Guid AdminUserId { get; set; }
    public Guid? LocationId { get; set; }

    public DateTime ClockInAt { get; set; } = DateTime.UtcNow;
    public DateTime? ClockOutAt { get; set; }

    /// <summary>Clock-out was never pressed: the next clock-in closed this entry with zero hours. A manager can fix the time.</summary>
    public bool Forgotten { get; set; }

    /// <summary>Set when a manager added or changed the entry by hand: who and why.</summary>
    public string? EditedBy { get; set; }
    public string? EditNote { get; set; }
}
