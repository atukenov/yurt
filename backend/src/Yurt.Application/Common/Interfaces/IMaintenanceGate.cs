using Yurt.Application.Features.Platform;

namespace Yurt.Application.Common.Interfaces;

/// <summary>Maintenance-mode switch with a short cache. Fail-open: a failed lookup means "not in maintenance".</summary>
public interface IMaintenanceGate
{
    Task<MaintenanceDto> GetAsync(CancellationToken ct = default);
    void Invalidate();
}
