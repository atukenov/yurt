using Microsoft.EntityFrameworkCore;
using Yurt.Application.Common.Interfaces;
using Yurt.Domain.Entities;

namespace Yurt.Application.Features.Menu.Services;

/// <summary>One option a dish offers at a branch, with the price it has there.</summary>
public sealed record DishOption(MenuTopping Topping, decimal Price);

/// <summary>How many options of one group a guest must / may pick.</summary>
public sealed record OptionRule(string Key, string Title, bool Required, int Min, int? Max);

/// <summary>
/// Works out which options a dish offers and what the guest has to pick. One place, used by the menu the guest sees
/// and by the checks when an order is made, so the two can never disagree.
/// </summary>
public static class DishOptions
{
    /// <summary>
    /// Options per dish. A dish with options chosen by hand offers only those; otherwise it offers its category's.
    /// Prices are the branch prices when a branch is given. Options that are switched off or sold out are left out
    /// unless <paramref name="includeUnavailable"/> is set (a sale that already happened).
    /// </summary>
    public static async Task<Dictionary<Guid, List<DishOption>>> LoadAsync(
        IApplicationDbContext db, IReadOnlyCollection<MenuItem> items, Guid? locationId,
        ICollection<Guid>? soldOut, bool includeUnavailable, CancellationToken ct)
    {
        var result = items.ToDictionary(i => i.Id, _ => new List<DishOption>());
        if (items.Count == 0) return result;

        var itemIds = items.Select(i => i.Id).ToList();
        var categoryIds = items.Select(i => i.CategoryId).Distinct().ToList();

        var byDish = (await db.MenuItemToppings
                .Include(l => l.Topping).ThenInclude(t => t.LocationPrices)
                .Where(l => itemIds.Contains(l.MenuItemId)).ToListAsync(ct))
            .ToLookup(l => l.MenuItemId, l => l.Topping);
        var byCategory = (await db.MenuToppingCategories
                .Include(l => l.Topping).ThenInclude(t => t.LocationPrices)
                .Where(l => categoryIds.Contains(l.CategoryId)).ToListAsync(ct))
            .ToLookup(l => l.CategoryId, l => l.Topping);

        foreach (var item in items)
        {
            if (item.IsCombo) continue; // a combo has a set price and offers no add-ons
            var source = byDish.Contains(item.Id) ? byDish[item.Id] : byCategory[item.CategoryId];
            foreach (var t in source.DistinctBy(t => t.Id))
            {
                if (!includeUnavailable && (!t.IsAvailable || soldOut?.Contains(t.Id) == true)) continue;
                var price = locationId is { } loc
                    ? t.LocationPrices.FirstOrDefault(p => p.LocationId == loc)?.Price ?? t.Price
                    : t.Price;
                result[item.Id].Add(new DishOption(t, price));
            }
        }
        return result;
    }

    /// <summary>The price of a dish at a branch (its own price when the branch has no special one).</summary>
    public static decimal BranchPrice(MenuItem item, Guid? locationId)
        => locationId is { } loc ? item.MenuItemLocations.FirstOrDefault(l => l.LocationId == loc)?.Price ?? item.Price : item.Price;

    /// <summary>The rule of a group. A group nobody has set up keeps the old meaning: at most one, may be none.</summary>
    public static OptionRule RuleFor(string key, MenuOptionGroup? row, string lang = "en")
    {
        if (row is null) return new OptionRule(key, key, false, 0, 1);
        var title = TitleFor(row, lang);
        var min = Math.Max(row.MinSelect, row.Required ? 1 : 0);
        var max = row.MaxSelect is { } m ? Math.Max(m, min) : (int?)null;
        return new OptionRule(key, string.IsNullOrWhiteSpace(title) ? key : title, row.Required || min > 0, min, max);
    }

    private static string? TitleFor(MenuOptionGroup g, string lang)
        => lang switch
        {
            "ru" when !string.IsNullOrWhiteSpace(g.TitleRu) => g.TitleRu,
            "kk" when !string.IsNullOrWhiteSpace(g.TitleKk) => g.TitleKk,
            _ => g.Title,
        };

    /// <summary>The rules for the groups used by the given options.</summary>
    public static async Task<Dictionary<string, MenuOptionGroup>> LoadGroupsAsync(
        IApplicationDbContext db, IEnumerable<string?> keys, CancellationToken ct)
    {
        var wanted = keys.Where(k => !string.IsNullOrEmpty(k)).Select(k => k!).Distinct().ToList();
        if (wanted.Count == 0) return [];
        return await db.MenuOptionGroups.Where(g => wanted.Contains(g.Key)).ToDictionaryAsync(g => g.Key, ct);
    }

    /// <summary>
    /// Checks the options picked for one dish line. Returns a message for the guest, or null when it is fine.
    /// Only groups the dish actually offers count; a group with nothing left to pick (all sold out) never blocks.
    /// </summary>
    public static string? Check(
        string dishName, IReadOnlyCollection<DishOption> offered, IReadOnlyCollection<Guid> picked,
        IReadOnlyDictionary<string, MenuOptionGroup> groups)
    {
        foreach (var g in offered.Where(o => o.Topping.Group != null).GroupBy(o => o.Topping.Group!))
        {
            var rule = RuleFor(g.Key, groups.GetValueOrDefault(g.Key));
            var count = g.Count(o => picked.Contains(o.Topping.Id));
            var min = Math.Min(rule.Min, g.Count());
            if (count < min)
                return min == 1 && rule.Max == 1
                    ? $"Please choose {rule.Title} for '{dishName}'."
                    : $"Please choose at least {min} of {rule.Title} for '{dishName}'.";
            if (rule.Max is { } max && count > max)
                return max == 1
                    ? $"Only one choice of {rule.Title} is allowed for '{dishName}'."
                    : $"At most {max} choices of {rule.Title} are allowed for '{dishName}'.";
        }
        return null;
    }
}
