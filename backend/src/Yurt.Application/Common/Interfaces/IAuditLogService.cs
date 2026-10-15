namespace Yurt.Application.Common.Interfaces;

public interface IAuditLogService
{
    Task LogAsync(string action, string entityType, string? entityId = null, string? details = null, CancellationToken ct = default);
}
