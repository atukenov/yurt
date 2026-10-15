using Yurt.Application.Common.Interfaces;
using Yurt.Domain.Entities;

namespace Yurt.Application.Features.AuditLog.Services;

public class AuditLogService : IAuditLogService
{
    private readonly IApplicationDbContext _db;
    private readonly ICurrentUserService _currentUser;

    public AuditLogService(IApplicationDbContext db, ICurrentUserService currentUser)
    {
        _db = db;
        _currentUser = currentUser;
    }

    public async Task LogAsync(
        string action, string entityType, string? entityId = null,
        string? details = null, CancellationToken ct = default)
    {
        _db.AuditLogs.Add(new Domain.Entities.AuditLog
        {
            Action = action,
            EntityType = entityType,
            EntityId = entityId,
            PerformedByAdminId = _currentUser.UserId,
            PerformedByUsername = _currentUser.Username ?? "system",
            Details = details
        });
        await _db.SaveChangesAsync(ct);
    }
}
