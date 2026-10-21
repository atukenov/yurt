using Microsoft.EntityFrameworkCore;
using Yurt.Application.Common.Interfaces;
using Yurt.Application.Common.Models;
using Yurt.Domain.Entities;

namespace Yurt.Application.Features.Stations;

/// <summary>Stations (Kitchen, Bar, ...) and which menu category is made at which station (Phase 6, <c>own_pos</c>).</summary>
public class StationService(IApplicationDbContext db, IAuditLogService audit)
{
    private const int MaxName = 50, MaxStations = 10;

    public async Task<Result<RoutingDto>> RoutingAsync(CancellationToken ct = default)
    {
        var stations = await db.Stations.OrderBy(s => s.SortOrder).ThenBy(s => s.Name).ToListAsync(ct);
        var categories = await db.MenuCategories.OrderBy(c => c.SortOrder).ThenBy(c => c.Name)
            .Select(c => new CategoryRoutingDto(c.Id, c.Name, c.StationId)).ToListAsync(ct);
        var counts = categories.Where(c => c.StationId != null).GroupBy(c => c.StationId!.Value).ToDictionary(g => g.Key, g => g.Count());
        var itemStations = await db.MenuItems.Where(i => i.Category.StationId != null)
            .Select(i => new { i.Id, StationId = i.Category.StationId!.Value }).ToDictionaryAsync(x => x.Id, x => x.StationId, ct);

        return Result<RoutingDto>.Success(new RoutingDto(
            stations.Select(s => new StationDto(s.Id, s.Name, counts.GetValueOrDefault(s.Id))).ToList(), categories, itemStations));
    }

    public async Task<Result<StationDto>> CreateAsync(SaveStationDto dto, CancellationToken ct = default)
    {
        var name = dto.Name?.Trim() ?? "";
        if (name.Length == 0 || name.Length > MaxName) return Result<StationDto>.Failure($"Name is required (up to {MaxName} characters).", 400);
        var existing = await db.Stations.ToListAsync(ct);
        if (existing.Count >= MaxStations) return Result<StationDto>.Failure("Too many stations.", 400);
        if (existing.Any(s => string.Equals(s.Name, name, StringComparison.OrdinalIgnoreCase)))
            return Result<StationDto>.Failure("A station with this name already exists.", 409);

        var station = new Station { Name = name, SortOrder = existing.Count };
        db.Stations.Add(station);
        await db.SaveChangesAsync(ct);
        await audit.LogAsync("StationCreated", "Station", station.Id.ToString(), name, ct);
        return Result<StationDto>.Success(new StationDto(station.Id, name, 0), 201);
    }

    public async Task<Result<bool>> DeleteAsync(Guid id, CancellationToken ct = default)
    {
        var station = await db.Stations.FirstOrDefaultAsync(s => s.Id == id, ct);
        if (station is null) return Result<bool>.Failure("Station not found.", 404);

        // Un-route its categories first so the delete never depends on the database rule alone.
        foreach (var c in await db.MenuCategories.Where(c => c.StationId == id).ToListAsync(ct)) c.StationId = null;
        db.Stations.Remove(station);
        await db.SaveChangesAsync(ct);
        await audit.LogAsync("StationDeleted", "Station", id.ToString(), station.Name, ct);
        return Result<bool>.Success(true);
    }

    /// <summary>Routes a menu category to a station, or clears it with a null station.</summary>
    public async Task<Result<CategoryRoutingDto>> SetCategoryStationAsync(Guid categoryId, SetCategoryStationDto dto, CancellationToken ct = default)
    {
        var category = await db.MenuCategories.FirstOrDefaultAsync(c => c.Id == categoryId, ct);
        if (category is null) return Result<CategoryRoutingDto>.Failure("Category not found.", 404);
        if (dto.StationId is { } sid && !await db.Stations.AnyAsync(s => s.Id == sid, ct))
            return Result<CategoryRoutingDto>.Failure("Station not found.", 404);

        category.StationId = dto.StationId;
        await db.SaveChangesAsync(ct);
        await audit.LogAsync("CategoryStationSet", "MenuCategory", categoryId.ToString(), dto.StationId?.ToString() ?? "none", ct);
        return Result<CategoryRoutingDto>.Success(new CategoryRoutingDto(category.Id, category.Name, category.StationId));
    }
}
