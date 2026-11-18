using System.Globalization;
using Microsoft.EntityFrameworkCore;
using Yurt.Application.Common.Interfaces;
using Yurt.Application.Common.Models;
using Yurt.Domain.Entities;
using Yurt.Domain.Enums;

namespace Yurt.Application.Features.Forecast;

/// <summary>
/// Sales forecast and daily prep plan of one branch. It looks at the same weekday over the last 8 weeks (days the branch
/// sold nothing are left out, the recent weeks count more) and works out how many of each dish to expect, hour by hour,
/// and how much of each ingredient those dishes will use (recipes and toppings), next to what the branch has now.
/// Combos count as the dishes inside them. Shows no money. Days follow the cafe's local time (Asia/Almaty).
/// </summary>
public class ForecastService(IApplicationDbContext db)
{
    private static readonly TimeZoneInfo CafeTime = TimeZoneInfo.FindSystemTimeZoneById("Asia/Almaty");

    private sealed record Made(Guid ItemId, Guid? VariantId, string Name, string? Size, int Quantity, IReadOnlyList<Guid> Toppings, bool IsCombo);

    private sealed class Day
    {
        public Dictionary<(Guid, Guid?), decimal[]> Dishes { get; } = [];
        public Dictionary<(Guid, Guid?), (string Name, string? Size)> Names { get; } = [];
        public Dictionary<Guid, decimal> Ingredients { get; } = [];
        public decimal[] Hourly { get; } = new decimal[24];
    }

    public static DateTime LocalNow(DateTime utc) => TimeZoneInfo.ConvertTimeFromUtc(DateTime.SpecifyKind(utc, DateTimeKind.Utc), CafeTime);

    public static bool TryParseDate(string? text, out DateTime date)
        => DateTime.TryParseExact(text, "yyyy-MM-dd", CultureInfo.InvariantCulture, DateTimeStyles.None, out date);

    public async Task<Result<ForecastDto>> GetAsync(
        Guid locationId, DateTime date, int adjustPercent, int bufferPercent, CancellationToken ct = default)
    {
        if (adjustPercent is < -100 or > ForecastMath.MaxPercent) return Result<ForecastDto>.Failure("Adjustment must be between -100 and 200 percent.", 422);
        if (bufferPercent is < 0 or > ForecastMath.MaxPercent) return Result<ForecastDto>.Failure("Buffer must be between 0 and 200 percent.", 422);
        if (!await db.Locations.AnyAsync(l => l.Id == locationId, ct)) return Result<ForecastDto>.NotFound("Location not found.");
        date = date.Date;

        // The same weekday, 1..8 weeks back.
        var dates = Enumerable.Range(1, ForecastMath.Weeks).Select(k => (Weeks: k, Date: date.AddDays(-7 * k))).ToList();
        var from = TimeZoneInfo.ConvertTimeToUtc(DateTime.SpecifyKind(dates[^1].Date, DateTimeKind.Unspecified), CafeTime);
        var to = TimeZoneInfo.ConvertTimeToUtc(DateTime.SpecifyKind(dates[0].Date.AddDays(1), DateTimeKind.Unspecified), CafeTime);
        var wanted = dates.Select(d => d.Date).ToHashSet();

        var orders = await db.Orders.AsNoTracking()
            .Where(o => o.Status == OrderStatus.Completed && o.LocationId == locationId
                && (o.CompletedAt ?? o.CreatedAt) >= from && (o.CompletedAt ?? o.CreatedAt) < to)
            .Select(o => new
            {
                At = o.CompletedAt ?? o.CreatedAt,
                Items = o.Items.Select(i => new
                {
                    i.MenuItemId, i.MenuItemName, i.VariantId, i.VariantLabel, i.Quantity,
                    Toppings = i.Toppings.Select(t => t.ToppingId).ToList(),
                    Parts = i.ComboParts.Select(p => new { p.ItemId, p.ItemName, p.VariantId, p.VariantLabel, p.Quantity }).ToList(),
                }).ToList(),
            })
            .ToListAsync(ct);

        var byDay = new Dictionary<DateTime, List<(DateTime Local, List<Made> Lines)>>();
        foreach (var o in orders)
        {
            var local = LocalNow(o.At);
            if (!wanted.Contains(local.Date)) continue;
            var lines = new List<Made>();
            foreach (var i in o.Items)
            {
                var combo = i.Parts.Count > 0;
                lines.Add(new Made(i.MenuItemId, i.VariantId, i.MenuItemName, i.VariantLabel, i.Quantity, i.Toppings, combo));
                foreach (var p in i.Parts)
                    lines.Add(new Made(p.ItemId, p.VariantId, p.ItemName, p.VariantLabel, i.Quantity * p.Quantity, [], false));
            }
            if (!byDay.TryGetValue(local.Date, out var list)) byDay[local.Date] = list = [];
            list.Add((local, lines));
        }

        // Recipes and topping uses, once, for every dish that was sold.
        var itemIds = byDay.Values.SelectMany(l => l).SelectMany(o => o.Lines).Select(m => m.ItemId).Distinct().ToList();
        var toppingIds = byDay.Values.SelectMany(l => l).SelectMany(o => o.Lines).SelectMany(m => m.Toppings).Distinct().ToList();
        var recipes = await db.RecipeLines.AsNoTracking().Where(r => itemIds.Contains(r.MenuItemId))
            .Select(r => new { r.MenuItemId, r.VariantId, r.IngredientId, r.Quantity }).ToListAsync(ct);
        var uses = toppingIds.Count == 0
            ? []
            : await db.ToppingUses.AsNoTracking().Where(u => toppingIds.Contains(u.ToppingId))
                .Select(u => new { u.ToppingId, u.IngredientId, u.Quantity }).ToListAsync(ct);

        var days = new List<(int WeeksAgo, DateTime Date, Day Day)>();
        foreach (var (weeksAgo, d) in dates)
        {
            if (!byDay.TryGetValue(d, out var list)) continue; // the branch sold nothing that day: closed, not a quiet day
            var day = new Day();
            foreach (var (local, lines) in list.OrderBy(x => x.Local))
            {
                foreach (var m in lines)
                {
                    if (!m.IsCombo)
                    {
                        var key = (m.ItemId, m.VariantId);
                        if (!day.Dishes.TryGetValue(key, out var hours)) day.Dishes[key] = hours = new decimal[24];
                        hours[local.Hour] += m.Quantity;
                        day.Hourly[local.Hour] += m.Quantity;
                        day.Names[key] = (m.Name, m.Size);
                        foreach (var r in recipes.Where(r => r.MenuItemId == m.ItemId && (r.VariantId == null || r.VariantId == m.VariantId)))
                            day.Ingredients[r.IngredientId] = day.Ingredients.GetValueOrDefault(r.IngredientId) + r.Quantity * m.Quantity;
                    }
                    foreach (var t in m.Toppings)
                        foreach (var u in uses.Where(u => u.ToppingId == t))
                            day.Ingredients[u.IngredientId] = day.Ingredients.GetValueOrDefault(u.IngredientId) + u.Quantity * m.Quantity;
                }
            }
            days.Add((weeksAgo, d, day));
        }

        decimal Avg(Func<Day, decimal> pick)
            => ForecastMath.WeightedAverage(days.Select(x => (pick(x.Day), ForecastMath.Weight(x.WeeksAgo))));
        decimal M(decimal raw) => ForecastMath.WithMargins(raw, adjustPercent, bufferPercent);

        var dishKeys = days.SelectMany(x => x.Day.Dishes.Keys).Distinct().ToList();
        var dishes = new List<ForecastDishDto>();
        foreach (var key in dishKeys)
        {
            // The weighted average of each hour, then summed into the four parts of the day.
            var parts = new decimal[4];
            for (var h = 0; h < 24; h++)
                parts[ForecastMath.DayPart(h)] += Avg(d => d.Dishes.TryGetValue(key, out var hours) ? hours[h] : 0m);
            var usual = parts.Sum();
            var latest = days.Where(x => x.Day.Names.ContainsKey(key)).OrderBy(x => x.WeeksAgo).First().Day.Names[key];
            dishes.Add(new ForecastDishDto(
                key.Item1, key.Item2, latest.Name, latest.Size,
                ForecastMath.Portions(M(usual)),
                ForecastMath.Portions(M(parts[0])), ForecastMath.Portions(M(parts[1])),
                ForecastMath.Portions(M(parts[2])), ForecastMath.Portions(M(parts[3])),
                Math.Round(usual, 1), days.Count(x => x.Day.Dishes.ContainsKey(key))));
        }
        dishes = dishes.OrderByDescending(d => d.Expected).ThenBy(d => d.Name).ToList();

        var hourly = Enumerable.Range(0, 24).Select(h => ForecastMath.Portions(M(Avg(d => d.Hourly[h])))).ToArray();
        int? peak = hourly.Max() > 0 ? Array.IndexOf(hourly, hourly.Max()) : null;

        var ingredientIds = days.SelectMany(x => x.Day.Ingredients.Keys).Distinct().ToList();
        var info = await db.Ingredients.AsNoTracking().Where(i => ingredientIds.Contains(i.Id))
            .Select(i => new { i.Id, i.Name, i.Unit }).ToListAsync(ct);
        var stock = await db.IngredientStocks.AsNoTracking()
            .Where(s => s.LocationId == locationId && ingredientIds.Contains(s.IngredientId))
            .ToDictionaryAsync(s => s.IngredientId, s => s.Quantity, ct);
        var ingredients = info
            .Select(i =>
            {
                var needed = Math.Round(M(Avg(d => d.Ingredients.GetValueOrDefault(i.Id))), 2);
                decimal? have = stock.TryGetValue(i.Id, out var q) ? q : null;
                return new ForecastIngredientDto(i.Id, i.Name, i.Unit, needed, have, have is { } h ? Math.Max(0m, needed - h) : 0m);
            })
            .Where(i => i.Needed > 0)
            .OrderByDescending(i => i.Missing > 0).ThenBy(i => i.Name)
            .ToList();

        var history = days.OrderBy(x => x.WeeksAgo)
            .Select(x => new ForecastDayDto(x.Date.ToString("yyyy-MM-dd", CultureInfo.InvariantCulture), (int)x.Day.Hourly.Sum()))
            .ToList();

        return Result<ForecastDto>.Success(new ForecastDto(
            date.ToString("yyyy-MM-dd", CultureInfo.InvariantCulture), (int)date.DayOfWeek, locationId,
            days.Count, ForecastMath.Confidence(days.Count), adjustPercent, bufferPercent,
            (int)Math.Ceiling(M(Avg(d => d.Hourly.Sum()))), hourly, peak, dishes, ingredients, history));
    }
}
