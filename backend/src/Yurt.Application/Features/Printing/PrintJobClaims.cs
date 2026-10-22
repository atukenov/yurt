using Microsoft.EntityFrameworkCore;
using Yurt.Application.Common.Interfaces;
using Yurt.Application.Common.Models;
using Yurt.Domain.Entities;

namespace Yurt.Application.Features.Printing;

/// <summary>
/// Taking waiting jobs and reporting how they went, shared by the senders that live in the cafe: the print agent and a
/// tablet with Bluetooth printers. A job is marked as being sent before it is handed out, so two senders never print it
/// twice; a sender that never reports back has its job marked failed after a while (see PrintService).
/// </summary>
internal static class PrintJobClaims
{
    /// <summary>Waiting jobs that are still fresh enough to print, oldest first.</summary>
    public static IQueryable<PrintJob> Waiting(IApplicationDbContext db, DateTime now)
    {
        var tooOld = now - PrintService.MaxAge;
        var drawerTooOld = now - PrintService.DrawerMaxAge;
        return db.PrintJobs.AsNoTracking()
            .Where(j => j.Status == PrintJob.Pending && j.CreatedAt >= tooOld)
            .Where(j => j.Kind != PrintJob.DrawerKind || j.CreatedAt >= drawerTooOld)
            .OrderBy(j => j.CreatedAt);
    }

    /// <summary>Marks the candidates as being sent and keeps the ones nobody else took first.</summary>
    public static async Task<List<T>> ClaimAsync<T>(IApplicationDbContext db, List<T> candidates, Func<T, Guid> id, DateTime now, CancellationToken ct)
    {
        var claimed = new List<T>();
        foreach (var job in candidates)
        {
            var jobId = id(job);
            var updated = await db.PrintJobs.Where(j => j.Id == jobId && j.Status == PrintJob.Pending)
                .ExecuteUpdateAsync(s => s.SetProperty(j => j.Status, PrintJob.Sending)
                    .SetProperty(j => j.Attempts, j => j.Attempts + 1).SetProperty(j => j.UpdatedAt, now), ct);
            if (updated == 1) claimed.Add(job);
        }
        return claimed;
    }

    /// <summary>
    /// Records whether a job the sender took printed. <paramref name="mine"/> is the jobs this sender may report on. A
    /// failed job goes back in the queue until it runs out of tries.
    /// </summary>
    public static async Task<Result<bool>> ReportAsync(
        IApplicationDbContext db, IQueryable<PrintJob> mine, Guid jobId, AgentJobResultDto dto, string defaultError, CancellationToken ct)
    {
        var job = await mine.AsNoTracking().Where(j => j.Id == jobId)
            .Select(j => new { j.Status, j.Attempts }).FirstOrDefaultAsync(ct);
        if (job is null) return Result<bool>.NotFound("Job not found.");
        if (job.Status != PrintJob.Sending) return Result<bool>.Failure("This job is not being sent.", 409);

        var status = PrintService.StatusAfter(dto.Ok, job.Attempts);
        var error = dto.Ok ? null : PrintService.Short(string.IsNullOrWhiteSpace(dto.Error) ? defaultError : dto.Error);
        var done = dto.Ok ? DateTime.UtcNow : (DateTime?)null;
        await db.PrintJobs.Where(j => j.Id == jobId)
            .ExecuteUpdateAsync(s => s.SetProperty(j => j.Status, status).SetProperty(j => j.LastError, error)
                .SetProperty(j => j.PrintedAt, done).SetProperty(j => j.UpdatedAt, DateTime.UtcNow), ct);
        return Result<bool>.Success(true);
    }
}
