using Yurt.Application.Features.Orders.Services;
using Yurt.Domain.Entities;

namespace Yurt.UnitTests;

public class OrderNumbersTests
{
    [Fact]
    public void TheDayEndsAtTheBranchsMidnight()
    {
        var almaty = new Location { TimeZone = null }; // Asia/Almaty, UTC+5
        Assert.Equal(new DateOnly(2026, 11, 13), OrderNumbers.DayOf(almaty, new DateTime(2026, 11, 13, 18, 59, 0, DateTimeKind.Utc)));
        Assert.Equal(new DateOnly(2026, 11, 14), OrderNumbers.DayOf(almaty, new DateTime(2026, 11, 13, 19, 0, 0, DateTimeKind.Utc)));

        var other = new Location { TimeZone = "Pacific/Kiritimati" }; // UTC+14
        Assert.Equal(new DateOnly(2026, 11, 14), OrderNumbers.DayOf(other, new DateTime(2026, 11, 13, 10, 0, 0, DateTimeKind.Utc)));
    }

    [Fact]
    public void AnUnknownTimeZone_FallsBackToAlmaty()
        => Assert.Equal(new DateOnly(2026, 11, 14), OrderNumbers.DayOf(new Location { TimeZone = "No/Such" }, new DateTime(2026, 11, 13, 20, 0, 0, DateTimeKind.Utc)));

    [Fact]
    public void ShowsOfflineNumberFirst_ThenDayNumber_ThenTheEndOfTheId()
    {
        var id = Guid.Parse("3f2a9c10-aaaa-bbbb-cccc-0123456789ab");
        Assert.Equal("T-3FA9C1", OrderNumbers.Display(new Order { Id = id, DailyNumber = 7, OfflineNumber = "T-3FA9C1" }));
        Assert.Equal("7", OrderNumbers.Display(new Order { Id = id, DailyNumber = 7 }));
        Assert.Equal("6789AB", OrderNumbers.Display(new Order { Id = id }));
    }

    [Fact]
    public void OfflineNumber_MatchesWhatTheTillPrints()
    {
        Assert.Equal("T-3FA9C1", OrderNumbers.OfflineNumberOf("3fa9c1aa-bb"));
        Assert.Null(OrderNumbers.OfflineNumberOf("ab"));
        Assert.Null(OrderNumbers.OfflineNumberOf(null));
    }
}
