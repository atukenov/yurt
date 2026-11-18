using Yurt.Domain.Entities;

namespace Yurt.Application.Common.Interfaces;

/// <summary>
/// One bank terminal protocol (Kaspi, Halyk, ...). The same adapter contract is used in two places: by the server when it
/// can reach the terminal itself, and by the cafe agent, which implements it in its own language (see
/// docs/card-terminals.md). Adapters must not throw for terminal problems: they return a <see cref="TerminalResult"/>.
/// Anything unclear after the request was sent must be reported as <see cref="TerminalOutcome.Unknown"/>.
/// </summary>
public interface ICardTerminalAdapter
{
    string Provider { get; }

    Task<TerminalResult> ChargeAsync(TerminalTarget target, TerminalRequest request, CancellationToken ct = default);

    Task<TerminalResult> RefundAsync(TerminalTarget target, TerminalRequest request, CancellationToken ct = default);
}

public record TerminalTarget(string Host, int Port);

/// <param name="Reference">Our own payment id, so a repeat after a lost answer is not charged twice.</param>
/// <param name="OriginalTransactionRef">Refund only: the bank number of the original charge.</param>
public record TerminalRequest(string Reference, decimal Amount, string? OriginalTransactionRef = null);

public enum TerminalOutcome
{
    Approved,
    Declined,
    /// <summary>The request never reached the terminal.</summary>
    Failed,
    /// <summary>The request was sent but the answer is missing.</summary>
    Unknown
}

public record TerminalResult(
    TerminalOutcome Outcome,
    string? ApprovalCode = null,
    string? TransactionRef = null,
    string? CardLast4 = null,
    string? Error = null)
{
    public static TerminalResult Approved(string? approval, string? transactionRef, string? last4)
        => new(TerminalOutcome.Approved, approval, transactionRef, last4);
    public static TerminalResult Declined(string error) => new(TerminalOutcome.Declined, Error: error);
    public static TerminalResult Failed(string error) => new(TerminalOutcome.Failed, Error: error);
    public static TerminalResult Unknown(string error) => new(TerminalOutcome.Unknown, Error: error);
}
