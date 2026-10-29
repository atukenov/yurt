using Yurt.Application.Common.Interfaces;
using Yurt.Domain.Entities;

namespace Yurt.Application.Features.Tenants;

public class BillingOptions
{
    /// <summary>Days a cafe keeps taking orders after <see cref="Tenant.PaidUntil"/> before limited mode starts.</summary>
    public int GraceDays { get; set; } = 7;
}

public enum CafeAccessState
{
    /// <summary>Paid up, or payment not tracked: everything works.</summary>
    Full = 0,
    /// <summary>Paid period is over but still inside the grace days: everything works, staff see a warning.</summary>
    Grace = 1,
    /// <summary>Limited mode: sign-in, menus and order history work; new orders are refused.</summary>
    Limited = 2,
}

/// <param name="LimitedFrom">When limited mode starts (or started) because of the date; null when not tracked.</param>
/// <param name="ByOperator">The operator switched limited mode on by hand.</param>
public record CafeAccessStatus(CafeAccessState State, DateTime? PaidUntil, DateTime? LimitedFrom, bool ByOperator)
{
    public static readonly CafeAccessStatus Open = new(CafeAccessState.Full, null, null, false);

    public bool IsLimited => State == CafeAccessState.Limited;
}

/// <summary>
/// Limited mode for a cafe that stopped paying. It only reads two fields on the cafe, <see cref="Tenant.PaidUntil"/>
/// (set by billing) and <see cref="Tenant.LimitedByOperator"/> (set by hand in /platform), so it does not care how a
/// payment is detected.
/// </summary>
public static class CafeAccess
{
    /// <summary>Code in the 403 body when a request is refused because of limited mode.</summary>
    public const string RefusedCode = "cafe_limited";

    public const string RefusedMessage = "This cafe is not taking new orders right now.";

    public static CafeAccessStatus Evaluate(Tenant? tenant, DateTime nowUtc, int graceDays)
    {
        if (tenant is null) return CafeAccessStatus.Open;
        var limitedFrom = tenant.PaidUntil?.AddDays(Math.Max(0, graceDays));
        var state = tenant.LimitedByOperator || (limitedFrom is { } from && nowUtc >= from)
            ? CafeAccessState.Limited
            : tenant.PaidUntil is { } paid && nowUtc >= paid ? CafeAccessState.Grace : CafeAccessState.Full;
        return new CafeAccessStatus(state, tenant.PaidUntil, limitedFrom, tenant.LimitedByOperator);
    }
}

/// <summary>
/// Limited-mode state of the cafe the current request belongs to. Reads the short-lived cafe list cache, so it costs
/// no database query on most requests. Fails open: if the cafe list cannot be loaded, the cafe counts as paid up.
/// </summary>
public class CafeAccessService(ITenantContext tenant, ITenantDirectory directory, BillingOptions options)
{
    /// <summary>Clock, replaceable in tests.</summary>
    public Func<DateTime> Clock { get; init; } = () => DateTime.UtcNow;

    public async Task<CafeAccessStatus> GetAsync(CancellationToken ct = default)
    {
        IReadOnlyList<Tenant> tenants;
        try { tenants = await directory.GetActiveAsync(ct); }
        catch (Exception) when (!ct.IsCancellationRequested) { return CafeAccessStatus.Open; }
        return CafeAccess.Evaluate(tenants.FirstOrDefault(t => t.Id == tenant.TenantId), Clock(), options.GraceDays);
    }
}
