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
/// <see cref="LogoDarkUrl"/> is an optional logo for dark mode; the apps fall back to <see cref="LogoUrl"/> without it.
/// <see cref="FontUrl"/> is an optional https link to the cafe's own font file (woff2, woff, ttf or otf);
/// the apps load it and fall back to <see cref="Font"/> while it loads or when it cannot be loaded.
/// </summary>
public record BrandDto(
    string Name,
    string PrimaryColor,
    string AccentColor,
    string? LogoUrl,
    string? SupportContact,
    string? IconUrl = null,
    string? SplashUrl = null,
    string? WelcomeText = null,
    IReadOnlyList<BrandLink>? Links = null,
    string? Radius = null,
    string? Font = null,
    string? LogoDarkUrl = null,
    string? FontUrl = null)
{
    public static readonly BrandDto Default = new("Yurt", "#ffd119", "#ea580c", null, null);
}

/// <summary>Allowed values for <see cref="BrandDto.Radius"/> and <see cref="BrandDto.Font"/>; null means the default.</summary>
public static class BrandChoices
{
    public static readonly string[] Radius = ["square", "default", "round"];
    public static readonly string[] Font = ["default", "system", "serif"];
    public static readonly string[] FontFileExtensions = [".woff2", ".woff", ".ttf", ".otf"];
}

/// <summary>A labelled link shown in the customer app (Instagram, website, ...).</summary>
public record BrandLink(string Label, string Url);

/// <summary>A cafe as listed in the operator's /platform screen.</summary>
public record CafeDto(Guid Id, string Slug, string Name, string? Domain, bool IsActive);

/// <summary>Everything needed to open a new cafe: its key, name, optional web address and the first admin login.</summary>
public record CreateCafeDto(string Slug, string Name, string? Domain, string AdminUsername, string AdminPassword);

public record SetCafeActiveDto(bool IsActive);

/// <summary>Whether a cafe has its own value for a secret. The value itself is never returned.</summary>
public record CafeSecretStatusDto(string Key, bool IsSet);

public record SetCafeSecretDto(string? Value);

public record LoyaltyImportDto(int Customers, int Imported, int Skipped, int Failed, decimal PointsImported);

public record LoyaltyMismatchDto(string Phone, decimal Iiko, decimal Native);

public record LoyaltyCompareDto(int Customers, int Matching, int Mismatched, int Failed, List<LoyaltyMismatchDto> Mismatches);

/// <summary>Where the cafe's bonus wallet lives ("iiko" or "native") and whether iiko is available to this cafe.</summary>
public record LoyaltyLedgerDto(string Mode, bool IikoAvailable);

public record SetLoyaltyLedgerDto(string? Mode);

/// <summary>State of one go-live item: done, missing, warn (works but not ready for real money) or off (module off, not needed).</summary>
public static class GoLiveState
{
    public const string Done = "done";
    public const string Missing = "missing";
    public const string Warn = "warn";
    public const string Off = "off";
}

/// <summary>
/// One line of the go-live checklist. <see cref="Missing"/> names what is still to do (setting or secret names, never
/// values); <see cref="Count"/> is how many menu items or branches there are; <see cref="Branches"/> lists branches
/// that still have no cash register number.
/// </summary>
public record GoLiveItemDto(string Key, string State, List<string> Missing, int? Count = null, List<string>? Branches = null);

/// <summary>What the chosen cafe still needs before it goes live, after the settings shared by every cafe.</summary>
public record GoLiveChecklistDto(string Slug, string Name, List<GoLiveItemDto> Server, List<GoLiveItemDto> Cafe);
