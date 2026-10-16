using System.Text.Json;
using System.Text.RegularExpressions;
using Microsoft.EntityFrameworkCore;
using Yurt.Application.Common.Interfaces;
using Yurt.Application.Common.Models;
using Yurt.Application.Features.Modules;
using Yurt.Domain;
using Yurt.Domain.Entities;

namespace Yurt.Application.Features.Platform;

/// <summary>Operator tools behind /platform: module licenses and maintenance mode. Every change is audited.</summary>
public class PlatformService
{
    public const int MaxMessageLength = 500;

    private readonly IApplicationDbContext _db;
    private readonly IAuditLogService _audit;
    private readonly IModuleGate _modules;
    private readonly IMaintenanceGate _maintenance;

    public PlatformService(IApplicationDbContext db, IAuditLogService audit, IModuleGate modules, IMaintenanceGate maintenance)
    {
        _db = db;
        _audit = audit;
        _modules = modules;
        _maintenance = maintenance;
    }

    public async Task<List<PlatformModuleDto>> GetModulesAsync(CancellationToken ct = default)
    {
        _modules.Invalidate(); // the operator must see what is in the database right now
        var statuses = await _modules.GetStatusesAsync(ct);
        var warnings = ModuleDependencies.GetWarnings(statuses.Where(s => s.IsEnabled).Select(s => s.Key).ToHashSet());
        return statuses.Select(s => ToDto(s, warnings)).ToList();
    }

    public async Task<Result<PlatformModuleDto>> UpdateModuleAsync(string key, UpdateModuleLicenseDto dto, CancellationToken ct = default)
    {
        if (!ModuleKeys.IsKnown(key))
            return Result<PlatformModuleDto>.Failure($"Unknown module '{key}'.", 404);
        if (key == ModuleKeys.Core && !dto.Enabled)
            return Result<PlatformModuleDto>.Failure("The core module cannot be switched off.", 400);
        if (dto.Notes is { Length: > 1000 })
            return Result<PlatformModuleDto>.Failure("Notes are too long (max 1000 characters).", 400);

        var license = await _db.ModuleLicenses.FirstOrDefaultAsync(m => m.ModuleKey == key, ct);
        if (license is null)
        {
            license = new ModuleLicense { ModuleKey = key };
            _db.ModuleLicenses.Add(license);
        }
        else
        {
            license.UpdatedAt = DateTime.UtcNow;
        }

        license.Enabled = dto.Enabled;
        license.ValidUntil = dto.ValidUntil is { } v ? DateTime.SpecifyKind(v, DateTimeKind.Utc) : null;
        license.Source = dto.Source;
        license.Notes = string.IsNullOrWhiteSpace(dto.Notes) ? null : dto.Notes.Trim();
        await _db.SaveChangesAsync(ct);
        _modules.Invalidate();

        await _audit.LogAsync("ModuleLicenseUpdated", "ModuleLicense", key,
            $"enabled={license.Enabled}; validUntil={license.ValidUntil:O}; source={license.Source}", ct);

        var all = await GetModulesAsync(ct);
        return Result<PlatformModuleDto>.Success(all.First(m => m.Key == key));
    }

    public Task<MaintenanceDto> GetMaintenanceAsync(CancellationToken ct = default)
    {
        _maintenance.Invalidate();
        return _maintenance.GetAsync(ct);
    }

    public async Task<Result<MaintenanceDto>> SetMaintenanceAsync(MaintenanceDto dto, CancellationToken ct = default)
    {
        var message = (dto.Message ?? string.Empty).Trim();
        if (message.Length > MaxMessageLength)
            return Result<MaintenanceDto>.Failure($"Message is too long (max {MaxMessageLength} characters).", 400);

        await UpsertAsync(PlatformSettingKeys.MaintenanceEnabled, dto.Enabled ? "true" : "false", ct);
        await UpsertAsync(PlatformSettingKeys.MaintenanceMessage, message, ct);
        await _db.SaveChangesAsync(ct);
        _maintenance.Invalidate();

        await _audit.LogAsync(dto.Enabled ? "MaintenanceEnabled" : "MaintenanceDisabled", "PlatformSetting",
            PlatformSettingKeys.MaintenanceEnabled, message.Length == 0 ? null : message, ct);
        return Result<MaintenanceDto>.Success(new MaintenanceDto(dto.Enabled, message));
    }

    private async Task UpsertAsync(string key, string value, CancellationToken ct)
    {
        var row = await _db.PlatformSettings.FirstOrDefaultAsync(s => s.Key == key, ct);
        if (row is null)
            _db.PlatformSettings.Add(new PlatformSetting { Key = key, Value = value });
        else
        {
            row.Value = value;
            row.UpdatedAt = DateTime.UtcNow;
        }
    }

    private static readonly Regex HexColor = new("^#[0-9a-fA-F]{6}$", RegexOptions.Compiled);
    private static readonly JsonSerializerOptions BrandJson = new(JsonSerializerDefaults.Web);

    /// <summary>The stored brand, or the Yurt default when none is saved (or the saved value is unreadable).</summary>
    public Task<BrandDto> GetBrandAsync(CancellationToken ct = default) => BrandReader.ReadAsync(_db, ct);

    public async Task<Result<BrandDto>> SetBrandAsync(BrandDto dto, CancellationToken ct = default)
    {
        var brand = dto with
        {
            Name = (dto.Name ?? string.Empty).Trim(),
            PrimaryColor = (dto.PrimaryColor ?? string.Empty).Trim().ToLowerInvariant(),
            AccentColor = (dto.AccentColor ?? string.Empty).Trim().ToLowerInvariant(),
            LogoUrl = string.IsNullOrWhiteSpace(dto.LogoUrl) ? null : dto.LogoUrl.Trim(),
            SupportContact = string.IsNullOrWhiteSpace(dto.SupportContact) ? null : dto.SupportContact.Trim(),
            IconUrl = string.IsNullOrWhiteSpace(dto.IconUrl) ? null : dto.IconUrl.Trim(),
            SplashUrl = string.IsNullOrWhiteSpace(dto.SplashUrl) ? null : dto.SplashUrl.Trim(),
            WelcomeText = string.IsNullOrWhiteSpace(dto.WelcomeText) ? null : dto.WelcomeText.Trim(),
            Radius = NormalizeChoice(dto.Radius),
            Font = NormalizeChoice(dto.Font),
            Links = dto.Links?
                .Where(l => !string.IsNullOrWhiteSpace(l.Label) || !string.IsNullOrWhiteSpace(l.Url))
                .Select(l => new BrandLink((l.Label ?? string.Empty).Trim(), (l.Url ?? string.Empty).Trim()))
                .ToList(),
        };
        if (ValidateBrand(brand) is { } error)
            return Result<BrandDto>.Failure(error, 400);

        await UpsertAsync(PlatformSettingKeys.Brand, JsonSerializer.Serialize(brand, BrandJson), ct);
        await _db.SaveChangesAsync(ct);
        await _audit.LogAsync("BrandUpdated", "PlatformSetting", PlatformSettingKeys.Brand,
            $"name={brand.Name}; primary={brand.PrimaryColor}; accent={brand.AccentColor}", ct);
        return Result<BrandDto>.Success(brand);
    }

    internal static string? ValidateBrand(BrandDto b)
    {
        if (b.Name.Length is 0 or > 60) return "Name is required (max 60 characters).";
        if (!HexColor.IsMatch(b.PrimaryColor)) return "Primary colour must look like #rrggbb.";
        if (!HexColor.IsMatch(b.AccentColor)) return "Accent colour must look like #rrggbb.";
        if (b.LogoUrl is { } logo && !IsHttpsUrl(logo)) return "Logo must be an https URL (max 500 characters).";
        if (b.IconUrl is { } icon && !IsHttpsUrl(icon)) return "Icon must be an https URL (max 500 characters).";
        if (b.SplashUrl is { } splash && !IsHttpsUrl(splash)) return "Splash image must be an https URL (max 500 characters).";
        if (b.WelcomeText is { Length: > 120 }) return "Welcome text is too long (max 120 characters).";
        if (b.Radius is { } radius && !BrandChoices.Radius.Contains(radius)) return "Corner style must be square, default or round.";
        if (b.Font is { } font && !BrandChoices.Font.Contains(font)) return "Font must be default, system or serif.";
        if (b.Links is { } links)
        {
            if (links.Count > 6) return "At most 6 links.";
            if (links.Any(l => l.Label.Length is 0 or > 30 || !IsHttpsUrl(l.Url)))
                return "Each link needs a label (max 30 characters) and an https URL.";
        }
        if (b.SupportContact is { Length: > 200 }) return "Support contact is too long (max 200 characters).";
        return null;
    }

    /// <summary>Blank or "default" means no override.</summary>
    private static string? NormalizeChoice(string? value)
        => string.IsNullOrWhiteSpace(value) || value.Trim().Equals("default", StringComparison.OrdinalIgnoreCase)
            ? null : value.Trim().ToLowerInvariant();

    private static bool IsHttpsUrl(string value)
        => value.Length <= 500 && Uri.TryCreate(value, UriKind.Absolute, out var uri) && uri.Scheme == Uri.UriSchemeHttps;

    private static PlatformModuleDto ToDto(ModuleStatus s, Dictionary<string, List<string>> warnings)
        => new(s.Key, s.IsEnabled, s.State.ToString().ToLowerInvariant(), s.ValidUntil, s.GraceEndsAt,
            s.Source, s.Notes, warnings.GetValueOrDefault(s.Key) ?? []);
}
