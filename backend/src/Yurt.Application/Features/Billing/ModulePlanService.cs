using System.Text.Json;
using Microsoft.EntityFrameworkCore;
using Yurt.Application.Common.Interfaces;
using Yurt.Application.Common.Models;
using Yurt.Domain;
using Yurt.Domain.Entities;
using Yurt.Domain.Enums;

namespace Yurt.Application.Features.Billing;

/// <summary>
/// The Modules section of the Platform screen: which modules the cafe has and what each costs per month. One save
/// switches the modules on or off for the cafe and makes the monthly payment cover exactly the switched-on ones, so the
/// module list and the monthly price can never disagree. Core is always on; its price is the base fee.
/// </summary>
public class ModulePlanService(
    IApplicationDbContext db,
    ITenantContext tenant,
    IModuleGate modules,
    IAuditLogService audit,
    IMenuCacheService? menuCache = null)
{
    private static readonly JsonSerializerOptions PriceJson = new(JsonSerializerDefaults.Web);

    public async Task<ModulePlanDto> GetAsync(CancellationToken ct = default)
    {
        modules.Invalidate(); // the operator must see what is in the database right now
        var statuses = await modules.GetStatusesAsync(ct);
        var row = await db.CafeBillings.AsNoTracking().FirstOrDefaultAsync(ct);
        return ToDto(statuses.Where(s => s.IsEnabled).Select(s => s.Key).ToHashSet(), row);
    }

    public async Task<Result<ModulePlanDto>> SaveAsync(SaveModulePlanDto dto, CancellationToken ct = default)
    {
        var currency = (dto.Currency ?? "usd").Trim().ToLowerInvariant();
        if (!BillingService.Currencies.Contains(currency))
            return Result<ModulePlanDto>.Failure($"Currency must be one of: {string.Join(", ", BillingService.Currencies)}.", 400);

        var items = dto.Modules ?? [];
        if (items.FirstOrDefault(m => !ModuleKeys.IsKnown(m.Key)) is { } unknown)
            return Result<ModulePlanDto>.Failure($"Unknown module '{unknown.Key}'.", 400);
        if (items.GroupBy(m => m.Key).Any(g => g.Count() > 1))
            return Result<ModulePlanDto>.Failure("Each module may be listed once.", 400);
        if (items.Any(m => m.Price is { } p && !ValidPrice(p, allowZero: true)))
            return Result<ModulePlanDto>.Failure("Prices must be 0 or more, with at most 2 decimals.", 400);
        if (dto.PriceByHand && dto.MonthlyPrice is { } byHand && !ValidPrice(byHand, allowZero: false))
            return Result<ModulePlanDto>.Failure("Monthly price must be more than 0, with at most 2 decimals.", 400);

        // Modules not sent keep their current on/off state; core is always on.
        var statuses = await modules.GetStatusesAsync(ct);
        var wasOn = statuses.Where(s => s.IsEnabled).Select(s => s.Key).ToHashSet();
        var on = new HashSet<string>(wasOn) { ModuleKeys.Core };
        foreach (var m in items.Where(m => m.Key != ModuleKeys.Core))
        {
            if (m.On) on.Add(m.Key); else on.Remove(m.Key);
        }

        var row = await db.CafeBillings.FirstOrDefaultAsync(ct);
        if (row is null)
        {
            row = new CafeBilling();
            db.CafeBillings.Add(row);
        }

        var changed = ModuleKeys.All.Where(k => k != ModuleKeys.Core && wasOn.Contains(k) != on.Contains(k)).ToList();
        if (changed.Count > 0)
        {
            var licenses = await db.ModuleLicenses.ToListAsync(ct);
            foreach (var key in changed)
            {
                var license = licenses.FirstOrDefault(l => l.ModuleKey == key);
                if (license is null)
                {
                    license = new ModuleLicense { ModuleKey = key };
                    db.ModuleLicenses.Add(license);
                }
                else license.UpdatedAt = DateTime.UtcNow;

                license.Enabled = on.Contains(key);
                if (license.Enabled)
                {
                    // Switched on again after it ran out: it lasts as long as the paid month, or without an end date
                    // when the cafe pays nothing by card yet.
                    if (license.ValidUntil is { } until && until < DateTime.UtcNow)
                        license.ValidUntil = row.PaidUntil > DateTime.UtcNow ? row.PaidUntil : null;
                    license.Source = license.ValidUntil is null ? ModuleLicenseSource.Manual : license.Source;
                }
            }
        }

        var prices = items.Where(m => m.Price is not null).ToDictionary(m => m.Key, m => m.Price!.Value);
        // Prices of modules not sent are kept.
        foreach (var (key, price) in ReadPrices(row).Where(p => !items.Any(m => m.Key == p.Key)))
            prices[key] = price;

        row.Currency = currency;
        row.ModulePrices = prices.Count == 0 ? null : JsonSerializer.Serialize(
            ModuleKeys.All.Where(prices.ContainsKey).ToDictionary(k => k, k => prices[k]), PriceJson);
        row.ModuleKeys = string.Join(",", ModuleKeys.All.Where(k => k != ModuleKeys.Core && on.Contains(k)));
        row.PriceByHand = dto.PriceByHand;
        var total = Total(prices, on);
        row.MonthlyPrice = dto.PriceByHand ? dto.MonthlyPrice : total > 0 ? total : null;
        await db.SaveChangesAsync(ct);
        modules.Invalidate();
        // Stock counting hides sold-out items from the cached menu only while own_pos is on.
        if (changed.Contains(ModuleKeys.OwnPos) && menuCache != null) await menuCache.InvalidateAllMenuAsync();

        await audit.LogAsync("ModulePlanSaved", "CafeBilling", tenant.TenantId.ToString(),
            $"on={string.Join(",", ModuleKeys.All.Where(on.Contains))}; price={row.MonthlyPrice?.ToString() ?? "none"} {row.Currency}"
            + (row.PriceByHand ? " (by hand)" : ""), ct);
        return Result<ModulePlanDto>.Success(ToDto(on, row));
    }

    /// <summary>Core is the base fee and is always counted; other modules count while switched on.</summary>
    internal static decimal Total(IReadOnlyDictionary<string, decimal> prices, IReadOnlySet<string> on)
        => prices.Where(p => p.Key == ModuleKeys.Core || on.Contains(p.Key)).Sum(p => p.Value);

    private static bool ValidPrice(decimal price, bool allowZero)
        => (allowZero ? price >= 0 : price > 0) && price <= BillingService.MaxMonthlyPrice && decimal.Round(price, 2) == price;

    private static Dictionary<string, decimal> ReadPrices(CafeBilling? row)
    {
        if (string.IsNullOrEmpty(row?.ModulePrices)) return [];
        try
        {
            return (JsonSerializer.Deserialize<Dictionary<string, decimal>>(row.ModulePrices, PriceJson) ?? [])
                .Where(p => ModuleKeys.IsKnown(p.Key)).ToDictionary();
        }
        catch (JsonException)
        {
            return [];
        }
    }

    private static ModulePlanDto ToDto(IReadOnlySet<string> on, CafeBilling? row)
    {
        var prices = ReadPrices(row);
        return new ModulePlanDto(
            ModuleKeys.All.Select(k => new ModulePlanItemDto(k, k == ModuleKeys.Core || on.Contains(k), prices.TryGetValue(k, out var p) ? p : null)).ToList(),
            row?.Currency ?? "usd",
            BillingService.Currencies.ToList(),
            Total(prices, on),
            row?.MonthlyPrice,
            row?.PriceByHand ?? false);
    }
}
