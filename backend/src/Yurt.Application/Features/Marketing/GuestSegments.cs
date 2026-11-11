namespace Yurt.Application.Features.Marketing;

/// <summary>What Yurt knows about one guest, worked out from their completed orders.</summary>
public record GuestStats(
    Guid Id,
    string FirstName,
    DateTime RegisteredAt,
    DateOnly? DateOfBirth,
    bool OptedOut,
    bool HasPhone,
    int CompletedOrders,
    int OrdersLast30Days,
    DateTime? FirstOrderAt,
    DateTime? LastOrderAt,
    decimal TotalSpent);

/// <summary>
/// Guest groups an owner can look at and send a message to. Everything is worked out from completed orders, so the
/// groups are always up to date and nothing has to be kept in sync.
/// </summary>
public static class GuestSegments
{
    public const string All = "all";
    public const string NeverOrdered = "never_ordered";
    public const string New = "new";
    public const string Regulars = "regulars";
    public const string Sleeping30 = "sleeping_30";
    public const string Sleeping60 = "sleeping_60";
    public const string Sleeping90 = "sleeping_90";
    public const string BirthdayWeek = "birthday_week";
    public const string BigSpenders = "big_spenders";

    /// <summary>Completed orders in the last 30 days that make a guest a regular.</summary>
    public const int RegularOrders = 5;

    /// <summary>Share of paying guests (by money spent) counted as big spenders.</summary>
    public const double BigSpenderShare = 0.10;

    public static readonly IReadOnlyList<string> Keys =
        [All, New, Regulars, Sleeping30, Sleeping60, Sleeping90, BirthdayWeek, BigSpenders, NeverOrdered];

    public static bool IsKnown(string? key) => key is not null && Keys.Contains(key);

    /// <summary>The guests of a group. <paramref name="today"/> is the cafe's local date (for birthdays).</summary>
    public static IEnumerable<GuestStats> Filter(string key, IReadOnlyList<GuestStats> guests, DateTime nowUtc, DateOnly today)
        => key switch
        {
            All => guests,
            NeverOrdered => guests.Where(g => g.CompletedOrders == 0),
            New => guests.Where(g => g.CompletedOrders == 1),
            Regulars => guests.Where(g => g.OrdersLast30Days >= RegularOrders),
            Sleeping30 => Sleeping(guests, nowUtc, 30),
            Sleeping60 => Sleeping(guests, nowUtc, 60),
            Sleeping90 => Sleeping(guests, nowUtc, 90),
            BirthdayWeek => guests.Where(g => DaysToBirthday(g.DateOfBirth, today) is <= 6),
            BigSpenders => BigSpendersOf(guests),
            _ => [],
        };

    private static IEnumerable<GuestStats> Sleeping(IEnumerable<GuestStats> guests, DateTime nowUtc, int days)
        => guests.Where(g => g.LastOrderAt is { } last && last <= nowUtc.AddDays(-days));

    private static IEnumerable<GuestStats> BigSpendersOf(IReadOnlyList<GuestStats> guests)
    {
        var paying = guests.Where(g => g.TotalSpent > 0).OrderByDescending(g => g.TotalSpent).ToList();
        if (paying.Count == 0) return [];
        var take = Math.Max(1, (int)Math.Ceiling(paying.Count * BigSpenderShare));
        return paying.Take(take);
    }

    /// <summary>Days from <paramref name="today"/> to the next birthday (0 = today); null when no birthday is known.
    /// A 29 February birthday counts as 28 February in other years.</summary>
    public static int? DaysToBirthday(DateOnly? dateOfBirth, DateOnly today)
    {
        if (dateOfBirth is not { } dob) return null;
        DateOnly InYear(int year)
            => new(year, dob.Month, Math.Min(dob.Day, DateTime.DaysInMonth(year, dob.Month)));
        var next = InYear(today.Year);
        if (next < today) next = InYear(today.Year + 1);
        return next.DayNumber - today.DayNumber;
    }
}
