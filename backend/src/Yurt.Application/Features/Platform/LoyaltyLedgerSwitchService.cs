using Microsoft.EntityFrameworkCore;
using Yurt.Application.Common.Interfaces;
using Yurt.Application.Common.Models;
using Yurt.Application.Features.Loyalty;
using Yurt.Domain.Entities;

namespace Yurt.Application.Features.Platform;

/// <summary>
/// Operator tool that picks where a cafe's bonus wallet lives (Phase 6): iiko or our own database. A cafe that has iiko
/// can only move to the own wallet while the comparison shows every balance matches, so no customer loses points.
/// Run it when no orders are in flight: a hold placed in one wallet cannot be released in the other.
/// </summary>
public class LoyaltyLedgerSwitchService(
    IApplicationDbContext db,
    IikoOptions options,
    ITenantContext tenant,
    ITenantDirectory directory,
    IAuditLogService audit,
    LoyaltyImportService import)
{
    public async Task<Result<LoyaltyLedgerDto>> GetAsync(CancellationToken ct = default)
    {
        var cafe = await CafeAsync(ct);
        return cafe is null
            ? Result<LoyaltyLedgerDto>.Failure("Cafe not found.", 404)
            : Result<LoyaltyLedgerDto>.Success(ToDto(cafe));
    }

    public async Task<Result<LoyaltyLedgerDto>> SetAsync(string? mode, CancellationToken ct = default)
    {
        if (!LoyaltyLedgerModes.IsValid(mode))
            return Result<LoyaltyLedgerDto>.Failure("Mode must be \"iiko\" or \"native\".", 400);

        var cafe = await CafeAsync(ct);
        if (cafe is null) return Result<LoyaltyLedgerDto>.Failure("Cafe not found.", 404);

        var iikoOn = options.IsEnabledFor(tenant.TenantId);
        if (mode == LoyaltyLedgerModes.Iiko && !iikoOn)
            return Result<LoyaltyLedgerDto>.Failure("iiko loyalty is not enabled for this cafe.", 400);

        if (mode == LoyaltyLedgerModes.Native && iikoOn)
        {
            var report = await import.CompareAsync(ct);
            if (!report.Succeeded) return Result<LoyaltyLedgerDto>.Failure(report.Error ?? "Comparison failed.", report.StatusCode);
            var r = report.Data!;
            if (r.Failed > 0)
                return Result<LoyaltyLedgerDto>.Failure($"{r.Failed} balances could not be checked. Try again.", 409);
            if (r.Mismatched > 0)
                return Result<LoyaltyLedgerDto>.Failure(
                    $"{r.Mismatched} balances differ between iiko and the own wallet. Run the import and check again.", 409);
        }

        cafe.LoyaltyLedger = mode;
        cafe.UpdatedAt = DateTime.UtcNow;
        await db.SaveChangesAsync(ct);
        directory.Invalidate();

        await audit.LogAsync("LoyaltyLedgerSwitched", "Tenant", cafe.Id.ToString(), $"Bonus wallet is now {mode}", ct);
        return Result<LoyaltyLedgerDto>.Success(ToDto(cafe));
    }

    private Task<Tenant?> CafeAsync(CancellationToken ct)
        => db.Tenants.FirstOrDefaultAsync(t => t.Id == tenant.TenantId, ct);

    private LoyaltyLedgerDto ToDto(Tenant cafe)
        => new(cafe.LoyaltyLedger ?? LoyaltyLedgerModes.Iiko, options.IsEnabledFor(cafe.Id));
}
