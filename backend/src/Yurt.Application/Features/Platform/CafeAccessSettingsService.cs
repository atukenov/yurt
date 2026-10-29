using Microsoft.EntityFrameworkCore;
using Yurt.Application.Common.Interfaces;
using Yurt.Application.Common.Models;
using Yurt.Application.Features.Tenants;

namespace Yurt.Application.Features.Platform;

/// <summary>Limited mode of a cafe as the operator and the cafe's staff see it.</summary>
/// <param name="State">"full", "grace" or "limited".</param>
public record CafeAccessDto(string State, DateTime? PaidUntil, DateTime? LimitedFrom, bool LimitedByOperator, int GraceDays);

/// <param name="PaidUntil">End of the paid period (UTC). Null = not tracked, never limited by date.</param>
public record SaveCafeAccessDto(DateTime? PaidUntil, bool LimitedByOperator);

/// <summary>
/// Operator tool for the chosen cafe's limited mode: the paid-until date (billing sets it too) and the hand switch.
/// Every change is audited and the cafe list cache is cleared, so it applies at once.
/// </summary>
public class CafeAccessSettingsService(
    IApplicationDbContext db,
    ITenantContext tenant,
    ITenantDirectory directory,
    IAuditLogService audit,
    BillingOptions options)
{
    public Func<DateTime> Clock { get; init; } = () => DateTime.UtcNow;

    public async Task<Result<CafeAccessDto>> GetAsync(CancellationToken ct = default)
    {
        var cafe = await db.Tenants.AsNoTracking().FirstOrDefaultAsync(t => t.Id == tenant.TenantId, ct);
        return cafe is null
            ? Result<CafeAccessDto>.Failure("Cafe not found.", 404)
            : Result<CafeAccessDto>.Success(ToDto(cafe));
    }

    public async Task<Result<CafeAccessDto>> SaveAsync(SaveCafeAccessDto dto, CancellationToken ct = default)
    {
        var cafe = await db.Tenants.FirstOrDefaultAsync(t => t.Id == tenant.TenantId, ct);
        if (cafe is null) return Result<CafeAccessDto>.Failure("Cafe not found.", 404);

        var paidUntil = dto.PaidUntil is { } p ? DateTime.SpecifyKind(p.ToUniversalTime(), DateTimeKind.Utc) : (DateTime?)null;
        if (cafe.PaidUntil == paidUntil && cafe.LimitedByOperator == dto.LimitedByOperator)
            return Result<CafeAccessDto>.Success(ToDto(cafe));

        cafe.PaidUntil = paidUntil;
        cafe.LimitedByOperator = dto.LimitedByOperator;
        cafe.UpdatedAt = DateTime.UtcNow;
        await db.SaveChangesAsync(ct);
        directory.Invalidate();

        await audit.LogPlatformAsync("CafeAccessSaved", "Tenant", cafe.Id.ToString(),
            $"paid until {paidUntil?.ToString("yyyy-MM-dd HH:mm") ?? "not tracked"}, limited by hand {(dto.LimitedByOperator ? "on" : "off")}", ct);
        return Result<CafeAccessDto>.Success(ToDto(cafe));
    }

    private CafeAccessDto ToDto(Domain.Entities.Tenant cafe)
    {
        var s = CafeAccess.Evaluate(cafe, Clock(), options.GraceDays);
        return new CafeAccessDto(s.State.ToString().ToLowerInvariant(), s.PaidUntil, s.LimitedFrom, s.ByOperator, options.GraceDays);
    }
}
