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

    public Task LogAsync(
        string action, string entityType, string? entityId = null,
        string? details = null, CancellationToken ct = default)
        => AddAsync(action, entityType, entityId, details, isPlatform: false, ct);

    public Task LogPlatformAsync(
        string action, string entityType, string? entityId = null,
        string? details = null, CancellationToken ct = default)
        => AddAsync(action, entityType, entityId, details, isPlatform: true, ct);

    private async Task AddAsync(string action, string entityType, string? entityId, string? details, bool isPlatform, CancellationToken ct)
    {
        _db.AuditLogs.Add(new Domain.Entities.AuditLog
        {
            Action = action,
            EntityType = entityType,
            EntityId = entityId,
            PerformedByAdminId = _currentUser.UserId,
            PerformedByUsername = _currentUser.Username ?? "system",
            Details = details,
            IsPlatform = isPlatform
        });
        await _db.SaveChangesAsync(ct);
    }
}
