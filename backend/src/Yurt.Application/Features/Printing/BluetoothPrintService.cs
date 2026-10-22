using Microsoft.EntityFrameworkCore;
using Yurt.Application.Common.Interfaces;
using Yurt.Application.Common.Models;
using Yurt.Domain.Entities;

namespace Yurt.Application.Features.Printing;

/// <summary>
/// Bluetooth printers (Phase 6, <c>own_pos</c>): a tablet in the cafe runs the admin app and is paired with the printer.
/// Every few seconds the app asks for the jobs of the printers it is connected to, sends them over Bluetooth and says how
/// each one went. The server never talks to these printers itself.
/// </summary>
public class BluetoothPrintService(IApplicationDbContext db)
{
    /// <summary>Jobs handed to a tablet per request.</summary>
    private const int BatchSize = 10;

    /// <summary>A tablet names at most this many printers per request.</summary>
    public const int MaxPrinters = 30;

    /// <summary>Hands the tablet the waiting jobs of the Bluetooth printers it says it is connected to, oldest first.</summary>
    public async Task<Result<List<BluetoothJobDto>>> ClaimAsync(BluetoothClaimDto dto, CancellationToken ct = default)
    {
        var ids = dto.PrinterIds?.Distinct().ToList() ?? [];
        if (ids.Count == 0) return Result<List<BluetoothJobDto>>.Success([]);
        if (ids.Count > MaxPrinters) return Result<List<BluetoothJobDto>>.Failure("Too many printers.", 400);

        var now = DateTime.UtcNow;
        var candidates = await PrintJobClaims.Waiting(db, now)
            .Where(j => j.Printer.ViaBluetooth && ids.Contains(j.PrinterId))
            .Take(BatchSize)
            .Select(j => new BluetoothJobDto(j.Id, j.PrinterId, j.Kind, j.Data))
            .ToListAsync(ct);
        return Result<List<BluetoothJobDto>>.Success(await PrintJobClaims.ClaimAsync(db, candidates, j => j.Id, now, ct));
    }

    /// <summary>The tablet says whether a job it took printed.</summary>
    public Task<Result<bool>> ReportAsync(Guid jobId, AgentJobResultDto dto, CancellationToken ct = default)
        => PrintJobClaims.ReportAsync(db, db.PrintJobs.Where(j => j.Printer.ViaBluetooth), jobId, dto,
            "The tablet could not print over Bluetooth.", ct);
}
