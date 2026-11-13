using Microsoft.EntityFrameworkCore;
using Yurt.Application.Common.Interfaces;
using Yurt.Application.Features.Menu.Services;
using Yurt.Application.Features.Stock;
using Yurt.Domain.Entities;

namespace Yurt.Application.Features.Combos;

/// <summary>
/// What the order paths (app orders and counter sales) need to know about combos: what is inside each one, whether it can
/// be sold at a branch right now, and the snapshot saved on the sold line. One place, so the two can never disagree.
/// </summary>
public sealed class ComboSales
{
    private readonly Dictionary<Guid, List<ComboPart>> _parts;
    private readonly Dictionary<Guid, MenuItemVariant> _variants;

    private ComboSales(Dictionary<Guid, List<ComboPart>> parts, Dictionary<Guid, MenuItemVariant> variants)
    {
        _parts = parts;
        _variants = variants;
    }

    public static readonly ComboSales None = new([], []);

    /// <summary>Loads the parts of every combo among <paramref name="items"/> (plain dishes are skipped).</summary>
    public static async Task<ComboSales> LoadAsync(IApplicationDbContext db, IEnumerable<MenuItem> items, CancellationToken ct)
    {
        var comboIds = items.Where(i => i.IsCombo).Select(i => i.Id).Distinct().ToList();
        if (comboIds.Count == 0) return None;

        var parts = (await db.ComboParts
                .Include(p => p.Item).ThenInclude(i => i.MenuItemLocations)
                .Where(p => comboIds.Contains(p.ComboId))
                .OrderBy(p => p.SortOrder).ThenBy(p => p.CreatedAt)
                .ToListAsync(ct))
            .GroupBy(p => p.ComboId).ToDictionary(g => g.Key, g => g.ToList());
        var variantIds = parts.Values.SelectMany(v => v).Where(p => p.VariantId != null).Select(p => p.VariantId!.Value).Distinct().ToList();
        var variants = variantIds.Count == 0
            ? []
            : await db.MenuItemVariants.Where(v => variantIds.Contains(v.Id)).ToDictionaryAsync(v => v.Id, ct);
        return new ComboSales(parts, variants);
    }

    public bool IsCombo(Guid menuItemId) => _parts.ContainsKey(menuItemId);

    public IReadOnlyList<ComboPart> PartsOf(Guid comboId) => _parts.GetValueOrDefault(comboId) ?? [];

    /// <summary>The dishes inside a combo for the stock checks (empty for a plain dish).</summary>
    public IReadOnlyList<ComboLinePart>? LinePartsOf(Guid menuItemId)
        => _parts.TryGetValue(menuItemId, out var p) && p.Count > 0
            ? p.Select(x => new ComboLinePart(x.ItemId, VariantOrNull(x)?.Id, x.Quantity)).ToList()
            : null;

    /// <summary>
    /// Why a combo can't be sold at the branch, or null when it can. A combo is off while any dish inside it is on the
    /// stop-list or not sold at the branch. <paramref name="checkAvailability"/> is false for a sale that already happened.
    /// </summary>
    public string? Problem(MenuItem combo, Guid? locationId, bool checkAvailability = true)
    {
        var parts = PartsOf(combo.Id);
        if (parts.Count == 0) return $"'{combo.Name}' has nothing in it yet.";
        if (!checkAvailability) return null;
        foreach (var p in parts)
        {
            if (!p.Item.IsAvailable)
                return $"'{combo.Name}' is not available right now: '{p.Item.Name}' is on the stop-list.";
            if (locationId is { } loc && p.Item.MenuItemLocations.Any() && p.Item.MenuItemLocations.All(l => l.LocationId != loc))
                return $"'{combo.Name}' is not available at this branch: '{p.Item.Name}' is not sold here.";
        }
        return null;
    }

    /// <summary>What a combo contains today, with the price of each piece bought separately at the branch.</summary>
    public List<OrderItemComboPart> Snapshot(MenuItem combo, Guid? locationId)
        => PartsOf(combo.Id).Select(p =>
        {
            var variant = VariantOrNull(p);
            return new OrderItemComboPart
            {
                ItemId = p.ItemId,
                ItemName = p.Item.Name,
                VariantId = variant?.Id,
                VariantLabel = variant?.Label,
                Quantity = p.Quantity,
                RegularPrice = variant?.Price ?? DishOptions.BranchPrice(p.Item, locationId),
            };
        }).ToList();

    // A size that was deleted since the combo was made falls back to the dish's normal portion.
    public MenuItemVariant? VariantOrNull(ComboPart p)
        => p.VariantId is { } v && _variants.TryGetValue(v, out var variant) && variant.MenuItemId == p.ItemId ? variant : null;
}
