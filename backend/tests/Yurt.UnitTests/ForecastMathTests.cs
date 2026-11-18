using Yurt.Application.Features.Forecast;

namespace Yurt.UnitTests;

public class ForecastMathTests
{
    [Fact]
    public void Recent_weeks_count_more()
    {
        Assert.Equal(8m, ForecastMath.Weight(1));
        Assert.Equal(1m, ForecastMath.Weight(8));
        // 4 last week (weight 8) and 2 the week before (weight 7): (32 + 14) / 15.
        Assert.Equal(46m / 15m, ForecastMath.WeightedAverage([(4m, 8m), (2m, 7m)]));
        Assert.Equal(0m, ForecastMath.WeightedAverage([]));
    }

    [Theory]
    [InlineData(0, "none")]
    [InlineData(1, "low")]
    [InlineData(2, "low")]
    [InlineData(3, "medium")]
    [InlineData(5, "medium")]
    [InlineData(6, "high")]
    [InlineData(8, "high")]
    public void Trust_grows_with_the_days_it_learned_from(int days, string expected)
        => Assert.Equal(expected, ForecastMath.Confidence(days));

    [Theory]
    [InlineData(6, 0)]
    [InlineData(10, 0)]
    [InlineData(11, 1)]
    [InlineData(14, 1)]
    [InlineData(15, 2)]
    [InlineData(17, 2)]
    [InlineData(18, 3)]
    [InlineData(23, 3)]
    public void Day_parts_follow_the_clock(int hour, int part) => Assert.Equal(part, ForecastMath.DayPart(hour));

    [Fact]
    public void Margins_stack_and_portions_round_up()
    {
        Assert.Equal(110m, ForecastMath.WithMargins(100m, 0, 10));
        Assert.Equal(132m, ForecastMath.WithMargins(100m, 20, 10));
        Assert.Equal(50m, ForecastMath.WithMargins(100m, -50, 0));
        Assert.Equal(4, ForecastMath.Portions(3.01m));
        Assert.Equal(3, ForecastMath.Portions(3m));
        Assert.Equal(0, ForecastMath.Portions(0m));
        Assert.Equal(0, ForecastMath.Portions(-2m));
    }
}
