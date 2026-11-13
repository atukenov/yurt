namespace Yurt.Application.Features.Orders.Services;

/// <summary>Limits for a tip. A tip sits on top of the bill and is never part of the order total.</summary>
public static class TipRules
{
    public const decimal MaxTip = 1_000_000m;

    public static decimal Round(decimal amount) => Math.Round(amount, 2, MidpointRounding.AwayFromZero);

    /// <summary>Null if the tip is fine. At most the size of the bill (100%), and never more than a million.</summary>
    public static string? Check(decimal amount, decimal bill)
    {
        if (amount < 0) return "A tip can't be negative.";
        if (amount > MaxTip) return "That tip is too big.";
        if (Round(amount) > Math.Max(bill, 0m)) return "A tip can't be more than the bill.";
        return null;
    }
}
