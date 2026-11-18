using Yurt.Application.Features.StaffWatch;

namespace Yurt.UnitTests;

public class ClockCrossCheckTests
{
    private static readonly DateTime T0 = new(2026, 11, 13, 9, 0, 0);

    private static IEnumerable<CrossMinute> Minutes(int from, int count, int seen, int clocked)
        => Enumerable.Range(from, count).Select(i => new CrossMinute(T0.AddMinutes(i), seen, clocked));

    [Fact]
    public void WorkingWithNobodyClockedIn_IsReported_OnlyFromTenMinutes()
    {
        Assert.Empty(ClockCrossCheckService.Windows(Minutes(0, 9, 1, 0)));
        var f = Assert.Single(ClockCrossCheckService.Windows(Minutes(0, 10, 1, 0)));
        Assert.Equal("working_not_clocked", f.Kind);
        Assert.Equal(10, f.Minutes);
        Assert.Equal(T0, f.From);
        Assert.Equal(T0.AddMinutes(10), f.To);
    }

    [Fact]
    public void ShortHolesInsideAStretchDoNotSplitIt_ButALongGapDoes()
    {
        var withHole = Minutes(0, 6, 2, 0).Concat(Minutes(8, 6, 2, 0)); // 2 missing minutes
        var one = Assert.Single(ClockCrossCheckService.Windows(withHole));
        Assert.Equal(14, one.Minutes);

        var split = Minutes(0, 10, 2, 0).Concat(Minutes(20, 10, 2, 0)); // camera off for 10 minutes
        Assert.Equal(2, ClockCrossCheckService.Windows(split).Count);
    }

    [Fact]
    public void MorePeopleThanClockedIn_IsReported_FromFifteenMinutes()
    {
        Assert.Empty(ClockCrossCheckService.Windows(Minutes(0, 14, 2, 1)));
        var f = Assert.Single(ClockCrossCheckService.Windows(Minutes(0, 15, 3, 1)));
        Assert.Equal("more_seen_than_clocked", f.Kind);
        Assert.Equal(3, f.MaxSeen);
        Assert.Equal(1, f.MaxClocked);
    }

    [Fact]
    public void ClockedInButNeverSeen_IsOnlyAHintAfterAnHour()
    {
        Assert.Empty(ClockCrossCheckService.Windows(Minutes(0, 59, 0, 2)));
        Assert.Equal("clocked_not_seen", Assert.Single(ClockCrossCheckService.Windows(Minutes(0, 60, 0, 2))).Kind);
    }

    [Fact]
    public void AgreeingMinutes_AndEmptyInput_GiveNothing()
    {
        Assert.Empty(ClockCrossCheckService.Windows(Minutes(0, 120, 2, 2)));
        Assert.Empty(ClockCrossCheckService.Windows(Minutes(0, 120, 0, 0)));
        Assert.Empty(ClockCrossCheckService.Windows([]));
    }

    [Fact]
    public void Findings_AreSortedByTime()
    {
        var all = Minutes(0, 15, 1, 0).Concat(Minutes(40, 70, 0, 1)).Concat(Minutes(130, 20, 3, 1));
        Assert.Equal(["working_not_clocked", "clocked_not_seen", "more_seen_than_clocked"],
            ClockCrossCheckService.Windows(all.Reverse()).Select(f => f.Kind).ToArray());
    }
}
