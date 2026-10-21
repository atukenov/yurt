using Yurt.Domain.Common;

namespace Yurt.Domain.Entities;

/// <summary>
/// A cashier shift at one branch (Phase 6, <c>own_pos</c>): opened with the cash in the drawer, closed with the cash
/// counted. A branch has at most one open shift. The Z report is worked out from the completed orders in the shift window.
/// </summary>
public class Shift : BaseEntity, ITenantOwned
{
    public Guid TenantId { get; set; }

    public Guid LocationId { get; set; }
    public Location Location { get; set; } = null!;

    public DateTime OpenedAt { get; set; } = DateTime.UtcNow;
    public string OpenedBy { get; set; } = string.Empty;
    public decimal OpeningCash { get; set; }

    /// <summary>Null while the shift is open.</summary>
    public DateTime? ClosedAt { get; set; }
    public string? ClosedBy { get; set; }
    public decimal? CountedCash { get; set; }
    public string? Note { get; set; }

    /// <summary>When the fiscal shift at the cloud cash register was closed (fiscal Z report). Null = not done or fiscal receipts are off.</summary>
    public DateTime? FiscalZReportAt { get; set; }

    /// <summary>Why closing the fiscal shift failed; cleared when it succeeds.</summary>
    public string? FiscalZReportError { get; set; }

    public ICollection<CashMovement> Movements { get; set; } = new List<CashMovement>();
}

/// <summary>Cash put into or taken out of the drawer during a shift (change float, supplier payment, bank drop).</summary>
public class CashMovement : BaseEntity, ITenantOwned
{
    public Guid TenantId { get; set; }

    public Guid ShiftId { get; set; }
    public Shift Shift { get; set; } = null!;

    /// <summary>Always positive; <see cref="IsOut"/> says which way it went.</summary>
    public decimal Amount { get; set; }
    public bool IsOut { get; set; }
    public string Reason { get; set; } = string.Empty;
    public string CreatedBy { get; set; } = string.Empty;
}
