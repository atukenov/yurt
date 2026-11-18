namespace Yurt.Application.Features.Forecast;

/// <summary>The small pure rules of the forecast, kept apart so they can be tested without a database.</summary>
public static class ForecastMath
{
    /// <summary>How many past weeks (same weekday) the forecast looks at.</summary>
    public const int Weeks = 8;
    public const int DefaultBufferPercent = 10;
    public const int MaxPercent = 200;

    /// <summary>The most recent week counts most: week 1 = <paramref name="weeks"/>, the oldest = 1.</summary>
    public static decimal Weight(int weeksAgo, int weeks = Weeks) => weeks - weeksAgo + 1;

    /// <summary>The average of the values, each counted by its weight. Empty gives 0.</summary>
    public static decimal WeightedAverage(IEnumerable<(decimal Value, decimal Weight)> values)
    {
        decimal sum = 0, weights = 0;
        foreach (var (v, w) in values) { sum += v * w; weights += w; }
        return weights == 0 ? 0m : sum / weights;
    }

    /// <summary>How much to trust the forecast: by how many past days it could learn from.</summary>
    public static string Confidence(int daysUsed) => daysUsed switch
    {
        0 => "none",
        <= 2 => "low",
        <= 5 => "medium",
        _ => "high",
    };

    /// <summary>morning (before 11), lunch (11-15), afternoon (15-18) and evening (18 on), by the cafe's local hour.</summary>
    public static int DayPart(int hour) => hour switch { < 11 => 0, < 15 => 1, < 18 => 2, _ => 3 };

    /// <summary>The raw average, plus the busy-day adjustment and the safety buffer (both in percent).</summary>
    public static decimal WithMargins(decimal raw, int adjustPercent, int bufferPercent)
        => raw * (1m + adjustPercent / 100m) * (1m + bufferPercent / 100m);

    /// <summary>Portions are whole: always round up so the shelf is not short.</summary>
    public static int Portions(decimal value) => value <= 0 ? 0 : (int)Math.Ceiling(Math.Round(value, 4));
}
