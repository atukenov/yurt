using Yurt.Domain.Common;
using Yurt.Domain.Enums;

namespace Yurt.Domain.Entities;

/// <summary>
/// Whether one paid platform module is switched on for this deployment. A missing row, or an empty
/// table, means "enabled" (fail-open): licensing must never be able to take a live cafe offline by accident.
/// </summary>
public class ModuleLicense : BaseEntity, ITenantOwned
{
    public Guid TenantId { get; set; }

    /// <summary>One of <see cref="ModuleKeys"/>.</summary>
    public string ModuleKey { get; set; } = string.Empty;

    public bool Enabled { get; set; } = true;

    /// <summary>Null means the license does not expire. Expiry is handled with a grace period, data is never deleted.</summary>
    public DateTime? ValidUntil { get; set; }

    public ModuleLicenseSource Source { get; set; } = ModuleLicenseSource.Manual;

    public string? Notes { get; set; }
}
