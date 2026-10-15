namespace Yurt.Application.Features.Modules;

public class ModuleOptions
{
    /// <summary>Days an expired license keeps working (with a warning) before it behaves as disabled.</summary>
    public int GraceDays { get; set; } = 14;

    /// <summary>How long module state is cached in memory. Changes in the database show up within this time.</summary>
    public int CacheSeconds { get; set; } = 60;
}

public enum ModuleState
{
    /// <summary>On, and not expired (or no expiry set).</summary>
    Active = 0,
    /// <summary>Expired but still inside the grace period: works, with a warning.</summary>
    Grace = 1,
    /// <summary>Expired and past the grace period: behaves as disabled.</summary>
    Expired = 2,
    /// <summary>Switched off.</summary>
    Disabled = 3
}

public record ModuleStatus(
    string Key,
    ModuleState State,
    DateTime? ValidUntil,
    DateTime? GraceEndsAt,
    string Source,
    string? Notes)
{
    public bool IsEnabled => State is ModuleState.Active or ModuleState.Grace;
}
