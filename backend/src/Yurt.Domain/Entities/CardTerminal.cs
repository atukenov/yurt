using Yurt.Domain.Common;

namespace Yurt.Domain.Entities;

/// <summary>
/// A bank card terminal at one branch (Phase 6, <c>own_pos</c>): the till sends it the amount and reads the result back.
/// Off by default: a branch with no terminal row keeps taking card payments by hand, as before. Real bank terminals sit
/// on the cafe's own network, so they are normally reached through the branch's agent (<see cref="ViaAgent"/>).
/// </summary>
public class CardTerminal : BaseEntity, ITenantOwned
{
    public Guid TenantId { get; set; }

    public Guid LocationId { get; set; }
    public Location Location { get; set; } = null!;

    public string Name { get; set; } = string.Empty;

    /// <summary>Lowercase adapter name, for example "simulator", "kaspi" or "halyk" (see CardTerminalProviders).</summary>
    public string Provider { get; set; } = string.Empty;

    /// <summary>Address of the terminal on the cafe network, such as 192.168.1.60. Empty for cloud terminals.</summary>
    public string Host { get; set; } = string.Empty;
    public int Port { get; set; } = 8080;

    /// <summary>The branch agent talks to the terminal instead of the server (needed when the server is in the cloud).</summary>
    public bool ViaAgent { get; set; } = true;

    /// <summary>Switched off = the till does not offer it. Nothing is deleted.</summary>
    public bool Active { get; set; } = true;
}
