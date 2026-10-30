namespace Yurt.Application.Common.Interfaces;

public interface IAuditLogService
{
    /// <summary>Records an action inside the current cafe; the cafe's admins see it in their audit log.</summary>
    Task LogAsync(string action, string entityType, string? entityId = null, string? details = null, CancellationToken ct = default);

    /// <summary>Records a platform operator's action; only SuperAdmins see it (see <c>AuditLog.IsPlatform</c>).</summary>
    Task LogPlatformAsync(string action, string entityType, string? entityId = null, string? details = null, CancellationToken ct = default);
}
