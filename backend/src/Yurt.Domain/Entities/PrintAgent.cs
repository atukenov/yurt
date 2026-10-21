using Yurt.Domain.Common;

namespace Yurt.Domain.Entities;

/// <summary>
/// The print agent of one branch (Phase 6, <c>own_pos</c>): a small program on a computer in the cafe that fetches print
/// jobs from the server and sends them to printers on the cafe's own network, which a server in the cloud cannot reach.
/// It signs in with a key; only a hash of the key is stored.
/// </summary>
public class PrintAgent : BaseEntity, ITenantOwned
{
    public Guid TenantId { get; set; }

    public Guid LocationId { get; set; }
    public Location Location { get; set; } = null!;

    /// <summary>SHA-256 of the key, in hex.</summary>
    public string KeyHash { get; set; } = string.Empty;

    /// <summary>When the agent last asked for jobs. Null = never connected.</summary>
    public DateTime? LastSeenAt { get; set; }
}
