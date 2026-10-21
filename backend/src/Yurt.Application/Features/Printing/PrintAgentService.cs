using System.Security.Cryptography;
using System.Text;
using Microsoft.EntityFrameworkCore;
using Yurt.Application.Common.Interfaces;
using Yurt.Application.Common.Models;
using Yurt.Domain.Entities;

namespace Yurt.Application.Features.Printing;

/// <summary>
/// The print agent of each branch (Phase 6, <c>own_pos</c>): staff make a key for it, and the agent uses the key to
/// fetch the jobs of its branch's agent printers and to say how each one went.
/// </summary>
public class PrintAgentService(IApplicationDbContext db, ITenantContext tenant, IAuditLogService audit)
{
    /// <summary>Jobs handed to an agent per request.</summary>
    private const int BatchSize = 10;

    public async Task<List<PrintAgentDto>> ListAsync(CancellationToken ct = default)
        => await db.PrintAgents.AsNoTracking()
            .Select(a => new PrintAgentDto(a.LocationId, true, a.LastSeenAt)).ToListAsync(ct);

    /// <summary>
    /// Makes a new key for a branch's agent; an older key stops working. The key starts with the cafe's short name and a
    /// dot, so the agent can tell the server which cafe it belongs to with the key alone.
    /// </summary>
    public async Task<Result<PrintAgentKeyDto>> CreateKeyAsync(Guid locationId, CancellationToken ct = default)
    {
        if (!await db.Locations.AnyAsync(l => l.Id == locationId, ct)) return Result<PrintAgentKeyDto>.NotFound("Branch not found.");
        var key = $"{tenant.Slug}.{Convert.ToBase64String(RandomNumberGenerator.GetBytes(32)).TrimEnd('=').Replace('+', '-').Replace('/', '_')}";

        var agent = await db.PrintAgents.FirstOrDefaultAsync(a => a.LocationId == locationId, ct);
        if (agent is null) db.PrintAgents.Add(agent = new PrintAgent { LocationId = locationId });
        agent.KeyHash = Hash(key);
        agent.LastSeenAt = null;
        await db.SaveChangesAsync(ct);
        await audit.LogAsync("PrintAgentKeyCreated", "Location", locationId.ToString(), null, ct);
        return Result<PrintAgentKeyDto>.Success(new PrintAgentKeyDto(locationId, key), 201);
    }

    public async Task<Result<bool>> RevokeAsync(Guid locationId, CancellationToken ct = default)
    {
        var agent = await db.PrintAgents.FirstOrDefaultAsync(a => a.LocationId == locationId, ct);
        if (agent is null) return Result<bool>.NotFound("This branch has no print agent key.");
        db.PrintAgents.Remove(agent);
        await db.SaveChangesAsync(ct);
        await audit.LogAsync("PrintAgentKeyRemoved", "Location", locationId.ToString(), null, ct);
        return Result<bool>.Success(true);
    }

    /// <summary>
    /// Hands the agent the waiting jobs of its branch's agent printers, oldest first, and marks them as being sent so
    /// nobody else takes them. A job the agent never reports back on is marked failed after a while (see PrintService).
    /// </summary>
    public async Task<Result<List<AgentJobDto>>> ClaimAsync(string? key, CancellationToken ct = default)
    {
        var agent = await FindAsync(key, ct);
        if (agent is null) return Result<List<AgentJobDto>>.Unauthorized("Unknown print agent key.");
        var now = DateTime.UtcNow;
        await db.PrintAgents.Where(a => a.Id == agent.Id).ExecuteUpdateAsync(s => s.SetProperty(a => a.LastSeenAt, now), ct);

        var tooOld = now - PrintService.MaxAge;
        var candidates = await db.PrintJobs.AsNoTracking()
            .Where(j => j.Status == PrintJob.Pending && j.Printer.ViaAgent && j.Printer.LocationId == agent.LocationId && j.CreatedAt >= tooOld)
            .OrderBy(j => j.CreatedAt).Take(BatchSize)
            .Select(j => new AgentJobDto(j.Id, j.Printer.Host, j.Printer.Port, j.Data))
            .ToListAsync(ct);

        var claimed = new List<AgentJobDto>();
        foreach (var job in candidates)
        {
            var updated = await db.PrintJobs.Where(j => j.Id == job.Id && j.Status == PrintJob.Pending)
                .ExecuteUpdateAsync(s => s.SetProperty(j => j.Status, PrintJob.Sending)
                    .SetProperty(j => j.Attempts, j => j.Attempts + 1).SetProperty(j => j.UpdatedAt, now), ct);
            if (updated == 1) claimed.Add(job);
        }
        return Result<List<AgentJobDto>>.Success(claimed);
    }

    /// <summary>The agent says whether a job it took printed. A failed job goes back in the queue until it runs out of tries.</summary>
    public async Task<Result<bool>> ReportAsync(string? key, Guid jobId, AgentJobResultDto dto, CancellationToken ct = default)
    {
        var agent = await FindAsync(key, ct);
        if (agent is null) return Result<bool>.Unauthorized("Unknown print agent key.");
        var job = await db.PrintJobs.AsNoTracking()
            .Where(j => j.Id == jobId && j.Printer.ViaAgent && j.Printer.LocationId == agent.LocationId)
            .Select(j => new { j.Status, j.Attempts }).FirstOrDefaultAsync(ct);
        if (job is null) return Result<bool>.NotFound("Job not found.");
        if (job.Status != PrintJob.Sending) return Result<bool>.Failure("This job is not being sent.", 409);

        var status = PrintService.StatusAfter(dto.Ok, job.Attempts);
        var error = dto.Ok ? null : PrintService.Short(string.IsNullOrWhiteSpace(dto.Error) ? "The print agent could not print." : dto.Error);
        var done = dto.Ok ? DateTime.UtcNow : (DateTime?)null;
        await db.PrintJobs.Where(j => j.Id == jobId)
            .ExecuteUpdateAsync(s => s.SetProperty(j => j.Status, status).SetProperty(j => j.LastError, error)
                .SetProperty(j => j.PrintedAt, done).SetProperty(j => j.UpdatedAt, DateTime.UtcNow), ct);
        return Result<bool>.Success(true);
    }

    private async Task<PrintAgent?> FindAsync(string? key, CancellationToken ct)
    {
        if (string.IsNullOrWhiteSpace(key) || key.Length > 200) return null;
        var hash = Hash(key.Trim());
        return await db.PrintAgents.AsNoTracking().FirstOrDefaultAsync(a => a.KeyHash == hash, ct);
    }

    private static string Hash(string key) => Convert.ToHexStringLower(SHA256.HashData(Encoding.UTF8.GetBytes(key)));
}
