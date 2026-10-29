using Asp.Versioning;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using Yurt.Application.Common.Interfaces;
using Yurt.Domain;
using Yurt.Domain.Enums;
using Yurt.WebApi.Common;

namespace Yurt.WebApi.Controllers.Admin;

[ApiVersion("1.0")]
[Route("api/v{version:apiVersion}/admin/audit-log")]
[ApiController]
[Authorize(Roles = "Admin,SuperAdmin")]
[RequireModule(ModuleKeys.StaffAudit)]
public class AdminAuditController(IApplicationDbContext db) : ControllerBase
{
    /// <summary>
    /// <c>scope=cafe</c>: the history of the request's cafe (a cafe admin's token only ever opens their own cafe; the
    /// operator picks one with X-Tenant). <c>scope=platform</c>: the operators' own actions across all cafes, SuperAdmin
    /// only. Without <c>scope</c>, an operator gets the platform history and a cafe admin their cafe's.
    /// </summary>
    [HttpGet]
    public async Task<IActionResult> GetAuditLog(
        [FromQuery] string? scope,
        [FromQuery] string? entityType,
        [FromQuery] Guid? adminId,
        [FromQuery] DateTime? from,
        [FromQuery] DateTime? to,
        [FromQuery] int page = 1,
        [FromQuery] int pageSize = 50,
        CancellationToken ct = default)
    {
        if (page < 1) page = 1;
        if (pageSize is < 1 or > 200) pageSize = 50;

        var isOperator = User.IsInRole(nameof(AdminRole.SuperAdmin));
        bool platform;
        switch (scope?.Trim().ToLowerInvariant())
        {
            case null or "": platform = isOperator; break;
            case "platform": platform = true; break;
            case "cafe": platform = false; break;
            default: return BadRequest(new { error = "scope must be 'cafe' or 'platform'." });
        }
        if (platform && !isOperator) return Forbid();

        var query = platform
            ? db.AuditLogs.IgnoreQueryFilters().Where(a => a.IsPlatform)
            : db.AuditLogs.Where(a => !a.IsPlatform);

        if (!string.IsNullOrWhiteSpace(entityType))
            query = query.Where(a => a.EntityType == entityType);
        if (adminId.HasValue)
            query = query.Where(a => a.PerformedByAdminId == adminId.Value);
        if (from.HasValue)
            query = query.Where(a => a.CreatedAt >= from.Value);
        if (to.HasValue)
            query = query.Where(a => a.CreatedAt <= to.Value);

        var total = await query.CountAsync(ct);
        var items = await query
            .OrderByDescending(a => a.CreatedAt)
            .Skip((page - 1) * pageSize)
            .Take(pageSize)
            .Select(a => new
            {
                a.Id,
                a.Action,
                a.EntityType,
                a.EntityId,
                a.PerformedByAdminId,
                a.PerformedByUsername,
                a.Details,
                a.IpAddress,
                a.CreatedAt
            })
            .ToListAsync(ct);

        return Ok(new { total, page, pageSize, items });
    }
}
