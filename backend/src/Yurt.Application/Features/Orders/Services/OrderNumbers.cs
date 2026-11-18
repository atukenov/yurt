using Microsoft.EntityFrameworkCore;
using Yurt.Application.Common.Interfaces;
using Yurt.Application.Features.Menu.Services;
using Yurt.Domain.Entities;

namespace Yurt.Application.Features.Orders.Services;

/// <summary>
/// The short order number guests remember: 1, 2, 3 ... starting again at 1 every day at each branch. The day ends at the
/// branch's own midnight (<see cref="Location.TimeZone"/>, Asia/Almaty when empty). The order keeps its real id; this is
/// only what people say and read. There is no wrap-around: a branch with more than 999 orders a day simply gets 1000.
/// </summary>
public static class OrderNumbers
{
    /// <summary>The branch's calendar day for a moment in time.</summary>
    public static DateOnly DayOf(Location location, DateTime utc)
        => DateOnly.FromDateTime(TimeZoneInfo.ConvertTimeFromUtc(DateTime.SpecifyKind(utc, DateTimeKind.Utc), TimedMenu.ZoneOf(location.TimeZone)));

    /// <summary>
    /// Gives the order the next number of its branch's day. One statement ("insert 1, or add 1 to the row that is already
    /// there"), so orders placed at the same moment each get their own number. A number is used up even if the order is
    /// then not saved; a gap in the numbers is harmless, a repeat is not.
    /// </summary>
    public static async Task AssignAsync(IApplicationDbContext db, Order order, Location location, CancellationToken ct = default)
    {
        var day = DayOf(location, order.CreatedAt);
        var numbers = await db.Database.SqlQuery<int>($"""
            INSERT INTO "OrderDayCounters" ("LocationId", "Day", "TenantId", "LastNumber") VALUES ({location.Id}, {day}, {location.TenantId}, 1)
            ON CONFLICT ("LocationId", "Day") DO UPDATE SET "LastNumber" = "OrderDayCounters"."LastNumber" + 1
            RETURNING "LastNumber" AS "Value"
            """).ToListAsync(ct); // not composable, so read it as it is
        order.BusinessDate = day;
        order.DailyNumber = numbers.Single();
    }

    /// <summary>What to show for an order: the offline receipt's number, else the day's number, else (an old order) the end of its id.</summary>
    public static string Display(Order o)
        => !string.IsNullOrEmpty(o.OfflineNumber) ? o.OfflineNumber
            : o.DailyNumber is { } n ? n.ToString()
            : o.Id.ToString()[^6..].ToUpperInvariant();

    /// <summary>The number a till prints on a receipt it made without the internet: "T-" and the start of the sale's key.</summary>
    public static string? OfflineNumberOf(string? key)
    {
        if (string.IsNullOrWhiteSpace(key)) return null;
        var compact = key.Replace("-", "");
        return compact.Length < 6 ? null : "T-" + compact[..6].ToUpperInvariant();
    }
}
