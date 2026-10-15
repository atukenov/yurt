using Microsoft.EntityFrameworkCore;
using Yurt.Application.Common.Interfaces;
using Yurt.Application.Common.Models;
using Yurt.Application.Features.Locations.DTOs;
using Yurt.Domain.Entities;

namespace Yurt.Application.Features.Locations.Services;

public class LocationService
{
    private readonly IApplicationDbContext _db;
    private readonly IAuditLogService _audit;

    public LocationService(IApplicationDbContext db, IAuditLogService audit)
    {
        _db = db;
        _audit = audit;
    }

    public async Task<List<LocationDto>> GetActiveLocationsAsync(CancellationToken ct = default)
        => await _db.Locations
            .Where(l => l.IsActive)
            .OrderBy(l => l.Name)
            .Select(l => new LocationDto(
                l.Id,
                l.Name,
                l.Address, l.WorkingHours, l.ContactPhone, l.IsActive, l.TwoGisUrl))
            .ToListAsync(ct);

    public async Task<List<AdminLocationDto>> GetAllLocationsAsync(CancellationToken ct = default)
        => await _db.Locations
            .OrderBy(l => l.Name)
            .Select(l => new AdminLocationDto(
                l.Id, l.Name,
                l.Address, l.WorkingHours, l.ContactPhone, l.IsActive, l.IikoTerminalGroupId, l.TwoGisUrl))
            .ToListAsync(ct);

    public async Task<Result<AdminLocationDto>> GetByIdAsync(Guid id, CancellationToken ct = default)
    {
        var loc = await _db.Locations.FindAsync([id], ct);
        if (loc == null) return Result<AdminLocationDto>.NotFound();
        return Result<AdminLocationDto>.Success(MapToAdminDto(loc));
    }

    public async Task<Result<AdminLocationDto>> CreateAsync(CreateLocationDto dto, CancellationToken ct = default)
    {
        if (!TryNormalizeTwoGisUrl(dto.TwoGisUrl, out var twoGisUrl))
            return Result<AdminLocationDto>.Failure(TwoGisUrlError, 422);

        var loc = new Location
        {
            Name = dto.Name,
            Address = dto.Address,
            WorkingHours = dto.WorkingHours,
            ContactPhone = dto.ContactPhone,
            IikoTerminalGroupId = dto.IikoTerminalGroupId,
            TwoGisUrl = twoGisUrl
        };
        _db.Locations.Add(loc);
        await _db.SaveChangesAsync(ct);
        await _audit.LogAsync("LocationCreated", "Location", loc.Id.ToString(), loc.Name, ct);
        return Result<AdminLocationDto>.Success(MapToAdminDto(loc), 201);
    }

    public async Task<Result<AdminLocationDto>> UpdateAsync(Guid id, UpdateLocationDto dto, CancellationToken ct = default)
    {
        if (!TryNormalizeTwoGisUrl(dto.TwoGisUrl, out var twoGisUrl))
            return Result<AdminLocationDto>.Failure(TwoGisUrlError, 422);

        var loc = await _db.Locations.FindAsync([id], ct);
        if (loc == null) return Result<AdminLocationDto>.NotFound();

        loc.Name = dto.Name;
        loc.Address = dto.Address;
        loc.WorkingHours = dto.WorkingHours;
        loc.ContactPhone = dto.ContactPhone;
        loc.IsActive = dto.IsActive;
        loc.IikoTerminalGroupId = dto.IikoTerminalGroupId;
        loc.TwoGisUrl = twoGisUrl;
        loc.UpdatedAt = DateTime.UtcNow;

        await _db.SaveChangesAsync(ct);
        await _audit.LogAsync("LocationUpdated", "Location", id.ToString(), loc.Name, ct);
        return Result<AdminLocationDto>.Success(MapToAdminDto(loc));
    }

    public async Task<Result<bool>> DeleteAsync(Guid id, CancellationToken ct = default)
    {
        var loc = await _db.Locations.FindAsync([id], ct);
        if (loc == null) return Result<bool>.NotFound();
        var name = loc.Name;
        _db.Locations.Remove(loc);
        await _db.SaveChangesAsync(ct);
        await _audit.LogAsync("LocationDeleted", "Location", id.ToString(), name, ct);
        return Result<bool>.Success(true);
    }

    private const string TwoGisUrlError = "2GIS link must be a valid http(s) link to 2gis (e.g. https://2gis.kz/… or https://go.2gis.com/…).";

    // Empty = no link. Otherwise only absolute http(s) URLs on a 2gis host are accepted, so a
    // stored value can never be a javascript:/data: URL or point at an unrelated site.
    private static bool TryNormalizeTwoGisUrl(string? raw, out string? normalized)
    {
        normalized = null;
        if (string.IsNullOrWhiteSpace(raw)) return true;
        var value = raw.Trim();
        if (value.Length > 1000) return false;
        if (!Uri.TryCreate(value, UriKind.Absolute, out var uri)) return false;
        if (uri.Scheme != Uri.UriSchemeHttps && uri.Scheme != Uri.UriSchemeHttp) return false;
        var host = uri.Host.ToLowerInvariant();
        var isTwoGis = host.StartsWith("2gis.") || host.Contains(".2gis.");
        if (!isTwoGis) return false;
        normalized = uri.AbsoluteUri;
        return true;
    }

    private static AdminLocationDto MapToAdminDto(Location l)
        => new(l.Id, l.Name, l.Address, l.WorkingHours, l.ContactPhone, l.IsActive, l.IikoTerminalGroupId, l.TwoGisUrl);
}
