using Yurt.Application.Features.Modules;
using Yurt.Domain.Enums;

namespace Yurt.Application.Features.Platform;

public record PlatformModuleDto(
    string Key,
    bool Enabled,
    string State,
    DateTime? ValidUntil,
    DateTime? GraceEndsAt,
    string Source,
    string? Notes,
    List<string> Warnings);

public record UpdateModuleLicenseDto(
    bool Enabled,
    DateTime? ValidUntil,
    ModuleLicenseSource Source,
    string? Notes);

public record MaintenanceDto(bool Enabled, string Message);

public record IntegrationStatusDto(string Key, bool Configured, bool Enabled);

/// <summary>
/// A cafe's brand as served to the apps (Phase 4 white-label). Colours are "#rrggbb".
/// <see cref="PrimaryColor"/> is the main colour (the 500 step of the generated scale),
/// <see cref="AccentColor"/> the secondary one used for gradient ends.
/// </summary>
public record BrandDto(
    string Name,
    string PrimaryColor,
    string AccentColor,
    string? LogoUrl,
    string? SupportContact,
    string? IconUrl = null,
    string? SplashUrl = null)
{
    public static readonly BrandDto Default = new("Yurt", "#ffd119", "#ea580c", null, null);
}
