using Microsoft.EntityFrameworkCore;
using Yurt.Application.Common.Interfaces;
using Yurt.Application.Common.Models;
using Yurt.Application.Features.Menu.DTOs;
using Yurt.Domain.Entities;

namespace Yurt.Application.Features.Menu.Services;

/// <summary>Admin side of time menus: the windows, what is tied to them, and their prices.</summary>
public class MenuScheduleService(IApplicationDbContext db, IAuditLogService audit, IMenuCacheService menuCache)
{
    public const int MaxSchedules = 30;

    public async Task<List<AdminMenuScheduleDto>> ListAsync(CancellationToken ct = default)
    {
        var schedules = await db.MenuSchedules.Include(s => s.Prices).OrderBy(s => s.Name).ToListAsync(ct);
        var cats = (await db.MenuCategories.Where(c => c.ScheduleId != null).Select(c => new { c.Id, S = c.ScheduleId!.Value }).ToListAsync(ct))
            .ToLookup(c => c.S, c => c.Id);
        var items = (await db.MenuItems.Where(i => i.ScheduleId != null).Select(i => new { i.Id, S = i.ScheduleId!.Value }).ToListAsync(ct))
            .ToLookup(i => i.S, i => i.Id);
        var now = DateTime.UtcNow;
        var zones = await db.Locations.Select(l => new { l.Id, l.TimeZone }).ToDictionaryAsync(l => l.Id, l => l.TimeZone, ct);

        return schedules.Select(s =>
        {
            var zone = TimedMenu.ZoneOf(s.LocationId is { } l && zones.TryGetValue(l, out var z) ? z : null);
            var running = s.IsActive && TimedMenu.IsRunning(s, TimeZoneInfo.ConvertTimeFromUtc(now, zone));
            return Map(s, cats[s.Id].ToList(), items[s.Id].ToList(), running);
        }).ToList();
    }

    public async Task<Result<AdminMenuScheduleDto>> SaveAsync(Guid? id, SaveMenuScheduleDto dto, CancellationToken ct = default)
    {
        var name = dto.Name?.Trim() ?? "";
        if (name.Length is 0 or > 100) return Result<AdminMenuScheduleDto>.Failure("The time menu needs a name (up to 100 characters).", 400);
        if (dto.Days is < 1 or > 127) return Result<AdminMenuScheduleDto>.Failure("Choose at least one day of the week.", 400);
        if (!TimedMenu.TryParseMinute(dto.Start, false, out var start) || !TimedMenu.TryParseMinute(dto.End, true, out var end))
            return Result<AdminMenuScheduleDto>.Failure("Times must look like 07:30 (the end may be 24:00).", 400);
        if (dto.LocationId is { } loc && !await db.Locations.AnyAsync(l => l.Id == loc, ct))
            return Result<AdminMenuScheduleDto>.Failure("Branch not found.", 400);

        MenuSchedule? row = null;
        if (id is { } sid)
        {
            row = await db.MenuSchedules.Include(s => s.Prices).FirstOrDefaultAsync(s => s.Id == sid, ct);
            if (row is null) return Result<AdminMenuScheduleDto>.NotFound();
        }
        else if (await db.MenuSchedules.CountAsync(ct) >= MaxSchedules)
            return Result<AdminMenuScheduleDto>.Failure($"A cafe can have up to {MaxSchedules} time menus.", 400);

        // What the window controls. A dish or category can sit in one window only.
        List<Guid> wantedCats = dto.CategoryIds ?? [];
        List<Guid> wantedItems = dto.ItemIds ?? [];
        var catIds = await db.MenuCategories.Where(c => wantedCats.Contains(c.Id)).Select(c => c.Id).ToListAsync(ct);
        var itemIds = await db.MenuItems.Where(i => wantedItems.Contains(i.Id)).Select(i => i.Id).ToListAsync(ct);

        var prices = new List<MenuTimePrice>();
        foreach (var p in dto.Prices ?? [])
        {
            if ((p.MenuItemId is null) == (p.CategoryId is null))
                return Result<AdminMenuScheduleDto>.Failure("Each price is for one dish or one category.", 400);
            if ((p.Price is null) == (p.PercentOff is null))
                return Result<AdminMenuScheduleDto>.Failure("Each price needs either a new price or a percent off.", 400);
            if (p.Price is < 0 || p.PercentOff is <= 0 or > 100)
                return Result<AdminMenuScheduleDto>.Failure("A price cannot be negative and a discount must be between 1 and 100 percent.", 400);
            var exists = p.MenuItemId is { } mi
                ? await db.MenuItems.AnyAsync(i => i.Id == mi, ct)
                : await db.MenuCategories.AnyAsync(c => c.Id == p.CategoryId, ct);
            if (!exists) return Result<AdminMenuScheduleDto>.Failure("A dish or category in the prices is no longer on the menu.", 400);
            prices.Add(new MenuTimePrice { MenuItemId = p.MenuItemId, CategoryId = p.CategoryId, Price = p.Price, PercentOff = p.PercentOff });
        }
        // One rule per dish or category.
        if (prices.GroupBy(p => (p.MenuItemId, p.CategoryId)).Any(g => g.Count() > 1))
            return Result<AdminMenuScheduleDto>.Failure("A dish or category can have only one price in a time menu.", 400);

        var created = row is null;
        if (row is null) { row = new MenuSchedule(); db.MenuSchedules.Add(row); }
        row.Name = name;
        row.Days = dto.Days;
        row.StartMinute = start;
        row.EndMinute = end;
        row.LocationId = dto.LocationId;
        row.IsActive = dto.IsActive;
        row.UpdatedAt = DateTime.UtcNow;

        foreach (var old in row.Prices.ToList()) db.MenuTimePrices.Remove(old);
        foreach (var p in prices) { p.ScheduleId = row.Id; db.MenuTimePrices.Add(p); }

        // Tie the chosen ones to this window; let go of the ones it used to hold.
        foreach (var c in await db.MenuCategories.Where(c => c.ScheduleId == row.Id || catIds.Contains(c.Id)).ToListAsync(ct))
            c.ScheduleId = catIds.Contains(c.Id) ? row.Id : null;
        foreach (var i in await db.MenuItems.Where(i => i.ScheduleId == row.Id || itemIds.Contains(i.Id)).ToListAsync(ct))
            i.ScheduleId = itemIds.Contains(i.Id) ? row.Id : null;

        await db.SaveChangesAsync(ct);
        await audit.LogAsync(created ? "MenuScheduleCreated" : "MenuScheduleUpdated", "MenuSchedule", row.Id.ToString(), name, ct);
        await menuCache.InvalidateAllMenuAsync();
        var saved = (await ListAsync(ct)).First(s => s.Id == row.Id);
        return Result<AdminMenuScheduleDto>.Success(saved, created ? 201 : 200);
    }

    public async Task<Result<bool>> DeleteAsync(Guid id, CancellationToken ct = default)
    {
        var row = await db.MenuSchedules.FirstOrDefaultAsync(s => s.Id == id, ct);
        if (row is null) return Result<bool>.NotFound();
        db.MenuSchedules.Remove(row); // dishes and categories tied to it go back on the menu all day; its prices go away
        await db.SaveChangesAsync(ct);
        await audit.LogAsync("MenuScheduleDeleted", "MenuSchedule", id.ToString(), row.Name, ct);
        await menuCache.InvalidateAllMenuAsync();
        return Result<bool>.Success(true);
    }

    private static AdminMenuScheduleDto Map(MenuSchedule s, List<Guid> cats, List<Guid> items, bool running) => new(
        s.Id, s.Name, s.Days, TimedMenu.FormatMinute(s.StartMinute), TimedMenu.FormatMinute(s.EndMinute), s.LocationId, s.IsActive,
        cats, items, s.Prices.Select(p => new TimePriceDto(p.MenuItemId, p.CategoryId, p.Price, p.PercentOff)).ToList(), running);
}
