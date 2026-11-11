using Yurt.Domain.Common;

namespace Yurt.Domain.Entities;

/// <summary>
/// A cost of running the cafe in one month that Yurt does not know by itself (rent, utilities, marketing...), typed in by
/// the CEO for the profit report. Ingredients, write-offs and wages are not entered here: they come from stock and payroll.
/// </summary>
public class CafeExpense : BaseEntity, ITenantOwned
{
    public Guid TenantId { get; set; }

    /// <summary>The month it belongs to, "yyyy-MM".</summary>
    public string Month { get; set; } = string.Empty;

    /// <summary>The branch it belongs to; null = the whole cafe (shown only when the report covers all branches).</summary>
    public Guid? LocationId { get; set; }
    public Location? Location { get; set; }

    /// <summary>One of <see cref="CafeExpenseCategories"/>.</summary>
    public string Category { get; set; } = CafeExpenseCategories.Other;
    public string Name { get; set; } = string.Empty;
    public decimal Amount { get; set; }
    public string CreatedBy { get; set; } = string.Empty;
}

public static class CafeExpenseCategories
{
    public const string Rent = "rent";
    public const string Utilities = "utilities";
    public const string Marketing = "marketing";
    public const string Repairs = "repairs";
    public const string Taxes = "taxes";
    public const string Other = "other";

    public static readonly IReadOnlyList<string> All = [Rent, Utilities, Marketing, Repairs, Taxes, Other];
}
