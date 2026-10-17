using Yurt.Domain.Common;

namespace Yurt.Domain.Entities;

/// <summary>
/// One cafe (customer of the platform). Phase 5 adds a TenantId to the cafe's data table by table; until a table has it,
/// everything belongs to <see cref="DefaultId"/>, so a deployment with a single cafe behaves exactly as before.
/// </summary>
public class Tenant : BaseEntity
{
    /// <summary>The cafe every existing row belongs to. Fixed so migrations and seeds can refer to it.</summary>
    public static readonly Guid DefaultId = Guid.Parse("22222222-0000-0000-0000-000000000001");

    public const string DefaultSlug = "default";

    /// <summary>Short lowercase key (letters, digits, dashes), unique. Used in subdomains and logs.</summary>
    public string Slug { get; set; } = string.Empty;

    public string Name { get; set; } = string.Empty;

    /// <summary>Host this cafe is served on (for example order.mycafe.kz). Null until a domain is assigned.</summary>
    public string? Domain { get; set; }

    /// <summary>A switched-off tenant is skipped when a request is matched to a cafe. Data is never deleted.</summary>
    public bool IsActive { get; set; } = true;

    /// <summary>Where this cafe's bonus wallet lives: "iiko" or "native". Null keeps today's behaviour (iiko). Phase 6.</summary>
    public string? LoyaltyLedger { get; set; }
}
