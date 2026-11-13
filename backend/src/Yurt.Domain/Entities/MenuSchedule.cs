using Yurt.Domain.Common;

namespace Yurt.Domain.Entities;

/// <summary>
/// A time window of the week, such as "Breakfast, every day 07:00-11:00" or "Happy hour, Mon-Fri 16:00-18:00".
/// Dishes and categories pointing at it (<see cref="MenuItem.ScheduleId"/>, <see cref="MenuCategory.ScheduleId"/>) are
/// on the menu only inside the window. <see cref="MenuTimePrice"/> rows give prices that apply only inside the window.
/// The clock is the branch's own (<see cref="Location.TimeZone"/>).
/// </summary>
public class MenuSchedule : BaseEntity, ITenantOwned
{
    public Guid TenantId { get; set; }

    public string Name { get; set; } = string.Empty;

    /// <summary>Days of the week as bits: Monday = 1, Tuesday = 2, Wednesday = 4 ... Sunday = 64. 127 = every day.</summary>
    public int Days { get; set; } = 127;
    /// <summary>Start of the window, minutes after midnight (0-1439).</summary>
    public int StartMinute { get; set; }
    /// <summary>End of the window, minutes after midnight (1-1440). At or before the start means it runs past midnight.</summary>
    public int EndMinute { get; set; } = 1440;

    /// <summary>Only this branch. Null = every branch (each by its own clock).</summary>
    public Guid? LocationId { get; set; }
    public bool IsActive { get; set; } = true;

    public ICollection<MenuTimePrice> Prices { get; set; } = new List<MenuTimePrice>();
}

/// <summary>A price that applies while a <see cref="MenuSchedule"/> is running, for one dish or a whole category.</summary>
public class MenuTimePrice : BaseEntity, ITenantOwned
{
    public Guid TenantId { get; set; }

    public Guid ScheduleId { get; set; }
    public MenuSchedule Schedule { get; set; } = null!;

    /// <summary>One of the two is set. A rule for a dish wins over a rule for its category.</summary>
    public Guid? MenuItemId { get; set; }
    public Guid? CategoryId { get; set; }

    /// <summary>New price of the dish (a dish without sizes). Used when set.</summary>
    public decimal? Price { get; set; }
    /// <summary>Percent taken off the price, 1-100 (works for sizes too). Used when <see cref="Price"/> is empty.</summary>
    public decimal? PercentOff { get; set; }
}
