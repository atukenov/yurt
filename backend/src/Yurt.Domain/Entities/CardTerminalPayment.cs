using Yurt.Domain.Common;

namespace Yurt.Domain.Entities;

/// <summary>
/// One card charge or card refund sent to a bank terminal. The money is only confirmed when the terminal says so
/// (<see cref="CardTerminalPaymentStatus.Approved"/>); a timeout becomes <see cref="CardTerminalPaymentStatus.Unknown"/>,
/// never "paid" and never "failed", so staff check the terminal's own slip before charging again.
/// </summary>
public class CardTerminalPayment : BaseEntity, ITenantOwned
{
    public Guid TenantId { get; set; }

    public Guid TerminalId { get; set; }
    public CardTerminal Terminal { get; set; } = null!;

    public Guid LocationId { get; set; }

    /// <summary>The order this charge is for, when it is known (a counter sale links it after the terminal approves).</summary>
    public Guid? OrderId { get; set; }

    public CardTerminalPaymentKind Kind { get; set; }
    public CardTerminalPaymentStatus Status { get; set; } = CardTerminalPaymentStatus.Pending;

    /// <summary>For a refund: the approved charge it gives back.</summary>
    public Guid? OriginalPaymentId { get; set; }

    public decimal Amount { get; set; }

    /// <summary>When an agent took the request. A request nobody takes in time becomes Unknown/Failed (see the service).</summary>
    public DateTime? SentAt { get; set; }
    public DateTime? CompletedAt { get; set; }

    // Filled in from the terminal's answer.
    public string? ApprovalCode { get; set; }
    /// <summary>The bank's transaction number (RRN), needed for a refund at some banks.</summary>
    public string? TransactionRef { get; set; }
    /// <summary>Last four digits only, for example "1234". The full number never reaches Yurt.</summary>
    public string? CardLast4 { get; set; }
    public string? Error { get; set; }

    public string? CreatedBy { get; set; }
}

public enum CardTerminalPaymentKind
{
    Sale = 0,
    Refund = 1
}

public enum CardTerminalPaymentStatus
{
    /// <summary>Waiting for the terminal (or the agent).</summary>
    Pending = 0,
    /// <summary>An agent took it and is talking to the terminal.</summary>
    Sending = 1,
    Approved = 2,
    /// <summary>The terminal answered "no" (declined card, cancelled on the terminal). Nothing was charged.</summary>
    Declined = 3,
    /// <summary>Never reached the terminal (offline, wrong address). Nothing was charged.</summary>
    Failed = 4,
    /// <summary>Staff cancelled before the terminal got it.</summary>
    Cancelled = 5,
    /// <summary>Sent but no answer came. The card may or may not be charged: staff must look at the terminal.</summary>
    Unknown = 6
}
