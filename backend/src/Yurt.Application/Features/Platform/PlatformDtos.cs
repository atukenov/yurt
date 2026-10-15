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
