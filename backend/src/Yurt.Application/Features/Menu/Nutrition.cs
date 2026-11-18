using Yurt.Application.Features.Menu.DTOs;
using Yurt.Domain.Entities;

namespace Yurt.Application.Features.Menu;

/// <summary>
/// Allergens and nutrition for a dish. Allergens are stored as a comma-separated list of the codes below
/// (the 14 EU allergens); the apps translate the codes into ru/kk/en. Numbers are per serving.
/// </summary>
public static class Nutrition
{
    public static readonly IReadOnlyList<string> AllergenCodes =
    [
        "gluten", "crustaceans", "eggs", "fish", "peanuts", "soy", "milk", "nuts",
        "celery", "mustard", "sesame", "sulphites", "lupin", "molluscs"
    ];

    /// <summary>Keeps only known codes, lower-cased, without repeats, in the standard order. Null/empty = none.</summary>
    public static List<string> CleanAllergens(IEnumerable<string>? codes)
    {
        if (codes is null) return [];
        var wanted = codes.Select(c => (c ?? "").Trim().ToLowerInvariant()).ToHashSet();
        return AllergenCodes.Where(wanted.Contains).ToList();
    }

    public static string? Join(IEnumerable<string>? codes)
    {
        var clean = CleanAllergens(codes);
        return clean.Count == 0 ? null : string.Join(',', clean);
    }

    public static List<string> Split(string? stored) =>
        string.IsNullOrWhiteSpace(stored) ? [] : CleanAllergens(stored.Split(',', StringSplitOptions.RemoveEmptyEntries));

    /// <summary>A negative number makes no sense; treat it as "not given".</summary>
    public static decimal? NonNegative(decimal? v) => v is < 0 ? null : v;

    public static NutritionDto? ToDto(MenuItem i)
    {
        var allergens = Split(i.Allergens);
        if (allergens.Count == 0 && i.Calories is null && i.Protein is null && i.Fat is null && i.Carbs is null && i.WeightGrams is null)
            return null;
        return new NutritionDto(i.Calories, i.Protein, i.Fat, i.Carbs, i.WeightGrams, allergens);
    }

    public static void Apply(MenuItem i, NutritionDto? n)
    {
        i.Allergens = Join(n?.Allergens);
        i.Calories = n?.Calories is < 0 ? null : n?.Calories;
        i.Protein = NonNegative(n?.Protein);
        i.Fat = NonNegative(n?.Fat);
        i.Carbs = NonNegative(n?.Carbs);
        i.WeightGrams = n?.WeightGrams is < 0 ? null : n?.WeightGrams;
    }
}
