namespace Yurt.Application.Features.AppUpdate;

public static class AppVersion
{
    /// <summary>Accepts 1–4 dot-separated numbers, e.g. "5", "5.3", "5.3.0".</summary>
    public static bool IsValid(string? v)
        => !string.IsNullOrWhiteSpace(v)
           && v.Trim().Split('.').Length <= 4
           && v.Trim().Split('.').All(p => p.Length is > 0 and <= 6 && p.All(char.IsAsciiDigit));

    public static int Compare(string? a, string? b)
    {
        var pa = Parse(a);
        var pb = Parse(b);
        for (var i = 0; i < Math.Max(pa.Length, pb.Length); i++)
        {
            var diff = (i < pa.Length ? pa[i] : 0).CompareTo(i < pb.Length ? pb[i] : 0);
            if (diff != 0) return diff;
        }
        return 0;
    }

    /// <summary>The larger of two minimum versions; a blank means "no minimum".</summary>
    public static string Max(string? a, string? b)
    {
        if (string.IsNullOrWhiteSpace(a)) return b ?? string.Empty;
        if (string.IsNullOrWhiteSpace(b)) return a;
        return Compare(a, b) >= 0 ? a : b;
    }

    private static int[] Parse(string? v)
        => (v ?? string.Empty).Trim().Split('.', StringSplitOptions.RemoveEmptyEntries)
            .Select(p => int.TryParse(p, out var n) ? n : 0).ToArray();
}
