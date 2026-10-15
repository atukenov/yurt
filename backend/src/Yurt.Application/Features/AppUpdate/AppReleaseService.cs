using Microsoft.EntityFrameworkCore;
using Yurt.Application.Common.Interfaces;
using Yurt.Application.Common.Models;
using Yurt.Domain.Entities;

namespace Yurt.Application.Features.AppUpdate;

public class AppReleaseService
{
    private readonly IApplicationDbContext _db;
    private readonly IAuditLogService _audit;
    private readonly AppUpdateOptions _options;

    public AppReleaseService(IApplicationDbContext db, IAuditLogService audit, AppUpdateOptions options)
    {
        _db = db;
        _audit = audit;
        _options = options;
    }

    public async Task<List<AppReleaseDto>> GetAllAsync(CancellationToken ct = default)
    {
        var releases = await _db.AppReleases.ToListAsync(ct);
        return releases
            .OrderByDescending(r => r.Version, Comparer<string>.Create(AppVersion.Compare))
            .Select(MapToDto)
            .ToList();
    }

    /// <summary>Public update info: config minimums raised by any mandatory release, plus the newest release's notes.</summary>
    public async Task<AppUpdateInfoDto> GetUpdateInfoAsync(CancellationToken ct = default)
    {
        var releases = await _db.AppReleases.ToListAsync(ct);
        var latest = releases.OrderByDescending(r => r.Version, Comparer<string>.Create(AppVersion.Compare)).FirstOrDefault();
        var mandatoryMin = releases.Where(r => r.IsMandatory)
            .Select(r => r.Version)
            .Aggregate(string.Empty, (acc, v) => AppVersion.Max(acc, v));

        return new AppUpdateInfoDto
        {
            MinVersionIos = AppVersion.Max(_options.MinVersionIos, mandatoryMin),
            MinVersionAndroid = AppVersion.Max(_options.MinVersionAndroid, mandatoryMin),
            StoreUrlIos = _options.StoreUrlIos,
            StoreUrlAndroid = _options.StoreUrlAndroid,
            LatestVersion = latest?.Version ?? string.Empty,
            LatestNotesEn = latest?.NotesEn ?? string.Empty,
            LatestNotesRu = latest?.NotesRu ?? string.Empty,
            LatestNotesKk = latest?.NotesKk ?? string.Empty,
        };
    }

    public async Task<Result<AppReleaseDto>> CreateAsync(SaveAppReleaseDto dto, CancellationToken ct = default)
    {
        var error = Validate(dto);
        if (error != null) return Result<AppReleaseDto>.Failure(error, 422);

        var version = dto.Version.Trim();
        if (await _db.AppReleases.AnyAsync(r => r.Version == version, ct))
            return Result<AppReleaseDto>.Failure($"Version {version} already exists.", 422);

        var release = new AppRelease();
        Apply(release, dto);
        _db.AppReleases.Add(release);
        await _db.SaveChangesAsync(ct);
        await _audit.LogAsync("AppReleaseCreated", "AppRelease", release.Id.ToString(), release.Version, ct);
        return Result<AppReleaseDto>.Success(MapToDto(release), 201);
    }

    public async Task<Result<AppReleaseDto>> UpdateAsync(Guid id, SaveAppReleaseDto dto, CancellationToken ct = default)
    {
        var error = Validate(dto);
        if (error != null) return Result<AppReleaseDto>.Failure(error, 422);

        var release = await _db.AppReleases.FirstOrDefaultAsync(r => r.Id == id, ct);
        if (release == null) return Result<AppReleaseDto>.NotFound();

        var version = dto.Version.Trim();
        if (await _db.AppReleases.AnyAsync(r => r.Version == version && r.Id != id, ct))
            return Result<AppReleaseDto>.Failure($"Version {version} already exists.", 422);

        Apply(release, dto);
        release.UpdatedAt = DateTime.UtcNow;
        await _db.SaveChangesAsync(ct);
        await _audit.LogAsync("AppReleaseUpdated", "AppRelease", id.ToString(), release.Version, ct);
        return Result<AppReleaseDto>.Success(MapToDto(release));
    }

    public async Task<Result<bool>> DeleteAsync(Guid id, CancellationToken ct = default)
    {
        var release = await _db.AppReleases.FirstOrDefaultAsync(r => r.Id == id, ct);
        if (release == null) return Result<bool>.NotFound();
        var version = release.Version;
        _db.AppReleases.Remove(release);
        await _db.SaveChangesAsync(ct);
        await _audit.LogAsync("AppReleaseDeleted", "AppRelease", id.ToString(), version, ct);
        return Result<bool>.Success(true);
    }

    private static string? Validate(SaveAppReleaseDto dto)
    {
        if (!AppVersion.IsValid(dto.Version)) return "Version must look like 5.3.0 (numbers separated by dots).";
        if (string.IsNullOrWhiteSpace(dto.NotesEn)) return "English release notes are required.";
        return null;
    }

    // Missing RU/KK notes fall back to English so the popup is never empty.
    private static void Apply(AppRelease r, SaveAppReleaseDto dto)
    {
        r.Version = dto.Version.Trim();
        r.NotesEn = dto.NotesEn.Trim();
        r.NotesRu = string.IsNullOrWhiteSpace(dto.NotesRu) ? r.NotesEn : dto.NotesRu.Trim();
        r.NotesKk = string.IsNullOrWhiteSpace(dto.NotesKk) ? r.NotesEn : dto.NotesKk.Trim();
        r.IsMandatory = dto.IsMandatory;
    }

    private static AppReleaseDto MapToDto(AppRelease r)
        => new(r.Id, r.Version, r.NotesEn, r.NotesRu, r.NotesKk, r.IsMandatory, r.CreatedAt);
}
