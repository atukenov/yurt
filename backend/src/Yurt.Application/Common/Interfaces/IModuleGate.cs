using Yurt.Application.Features.Modules;

namespace Yurt.Application.Common.Interfaces;

/// <summary>
/// Answers "is this paid module switched on?". Implementations must fail open: if the lookup fails,
/// the table is empty or the key is unknown, the answer is yes.
/// </summary>
public interface IModuleGate
{
    Task<bool> IsEnabledAsync(string moduleKey, CancellationToken ct = default);

    /// <summary>Status of every known module (missing rows reported as active).</summary>
    Task<IReadOnlyList<ModuleStatus>> GetStatusesAsync(CancellationToken ct = default);

    /// <summary>Drops the cache so a change made through the database or admin tools applies immediately.</summary>
    void Invalidate();
}
