using Yurt.Domain.Common;

namespace Yurt.Domain.Entities;

/// <summary>
/// One official tax receipt (фискальный чек) for one order, sent to the cafe's cloud cash register (Phase 6,
/// <c>own_pos</c>). A sale never waits for it: the row is queued, sent in the background and retried until it goes
/// through. An order has at most one sale receipt and one return receipt.
/// </summary>
public class FiscalReceipt : BaseEntity, ITenantOwned
{
    public Guid TenantId { get; set; }

    public Guid OrderId { get; set; }
    public Order Order { get; set; } = null!;

    /// <summary>The branch of the order, kept here so the cashier queue and shift close do not need the order.</summary>
    public Guid LocationId { get; set; }

    public FiscalReceiptKind Kind { get; set; }
    public FiscalReceiptStatus Status { get; set; } = FiscalReceiptStatus.Pending;

    /// <summary>What the guest paid in money (total less bonus points).</summary>
    public decimal Amount { get; set; }

    /// <summary>Provider name at the time the receipt was queued, for example "webkassa".</summary>
    public string Provider { get; set; } = string.Empty;

    public int Attempts { get; set; }
    public DateTime NextAttemptAt { get; set; } = DateTime.UtcNow;

    /// <summary>Why the last attempt failed, or why the receipt was skipped. Shown to staff.</summary>
    public string? LastError { get; set; }

    // Filled in once the provider accepted the receipt.
    public DateTime? SentAt { get; set; }
    public string? RegisterNumber { get; set; }
    public string? CheckNumber { get; set; }
    public string? TicketUrl { get; set; }
}

public enum FiscalReceiptKind
{
    Sale = 0,
    Return = 1
}

public enum FiscalReceiptStatus
{
    /// <summary>Waiting to be sent, or sending failed and it will be tried again. Staff see "not fiscalized".</summary>
    Pending = 0,
    Sent = 1,
    /// <summary>Not sent on purpose (paid only with a skipped method such as Kaspi with Kaspi Kassa on, or only with points).</summary>
    Skipped = 2
}
