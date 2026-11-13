using Yurt.Application.Features.Menu.Services;
using Yurt.Domain.Entities;

namespace Yurt.UnitTests;

public class TimedMenuTests
{
    // 2026-11-16 is a Monday.
    private static DateTime At(int dayOffset, int hour, int minute = 0) => new DateTime(2026, 11, 16).AddDays(dayOffset).AddHours(hour).AddMinutes(minute);

    private static MenuSchedule Window(int days, string start, string end)
    {
        Assert.True(TimedMenu.TryParseMinute(start, false, out var s));
        Assert.True(TimedMenu.TryParseMinute(end, true, out var e));
        return new MenuSchedule { Days = days, StartMinute = s, EndMinute = e };
    }

    [Fact]
    public void ADaytimeWindow_RunsOnItsDays_FromStartUpToButNotIncludingEnd()
    {
        var breakfast = Window(0b0011111, "07:00", "11:00"); // Mon-Fri
        Assert.True(TimedMenu.IsRunning(breakfast, At(0, 7)));
        Assert.True(TimedMenu.IsRunning(breakfast, At(0, 10, 59)));
        Assert.False(TimedMenu.IsRunning(breakfast, At(0, 11)));
        Assert.False(TimedMenu.IsRunning(breakfast, At(0, 6, 59)));
        Assert.False(TimedMenu.IsRunning(breakfast, At(5, 8))); // Saturday
    }

    [Fact]
    public void AWindowPastMidnight_BelongsToTheDayItStarted()
    {
        var night = Window(0b0100000, "22:00", "03:00"); // Saturday night only
        Assert.True(TimedMenu.IsRunning(night, At(5, 23)));   // Sat 23:00
        Assert.True(TimedMenu.IsRunning(night, At(6, 2)));    // Sun 02:00 (still Saturday's)
        Assert.False(TimedMenu.IsRunning(night, At(6, 23)));  // Sun 23:00
        Assert.False(TimedMenu.IsRunning(night, At(5, 2)));   // Sat 02:00 (Friday's night, not on the list)
    }

    [Fact]
    public void EndOf24_MeansUntilMidnight_AndBadTimesAreRefused()
    {
        var evening = Window(127, "18:00", "24:00");
        Assert.True(TimedMenu.IsRunning(evening, At(2, 23, 59)));
        Assert.False(TimedMenu.IsRunning(evening, At(2, 17, 59)));
        Assert.False(TimedMenu.TryParseMinute("24:00", false, out _));
        Assert.False(TimedMenu.TryParseMinute("00:00", true, out _));
        Assert.False(TimedMenu.TryParseMinute("7:5x", false, out _));
        Assert.False(TimedMenu.TryParseMinute("12:75", false, out _));
        Assert.Equal("07:05", TimedMenu.FormatMinute(425));
    }

    [Fact]
    public void ZonesFallBackToTheDefault_WhenMissingOrUnknown()
    {
        Assert.Equal(TimedMenu.ZoneOf(null).Id, TimedMenu.ZoneOf("Not/AZone").Id);
        Assert.Equal("Europe/London", TimedMenu.ZoneOf("Europe/London").Id);
        Assert.True(TimedMenu.IsKnownZone("Asia/Almaty"));
        Assert.False(TimedMenu.IsKnownZone("Mars/Olympus"));
    }
}
