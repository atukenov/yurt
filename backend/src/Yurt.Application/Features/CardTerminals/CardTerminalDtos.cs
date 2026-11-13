using Yurt.Domain.Entities;

namespace Yurt.Application.Features.CardTerminals;

public record CardTerminalDto(Guid Id, Guid LocationId, string Name, string Provider, string Host, int Port, bool ViaAgent, bool Active);

public record SaveCardTerminalDto(Guid LocationId, string Name, string Provider, string? Host, int? Port, bool ViaAgent = true, bool Active = true);

public record StartCardPaymentDto(Guid TerminalId, decimal Amount, Guid? OrderId = null);

public record CardPaymentDto(
    Guid Id, Guid TerminalId, Guid? OrderId, CardTerminalPaymentKind Kind, CardTerminalPaymentStatus Status,
    decimal Amount, string? ApprovalCode, string? TransactionRef, string? CardLast4, string? Error,
    DateTime CreatedAt, DateTime? CompletedAt);

/// <summary>What staff decide after looking at the terminal's slip for an Unknown payment.</summary>
public record ResolveCardPaymentDto(bool Charged, string? TransactionRef = null);

/// <summary>A request handed to the agent. It talks to the terminal at <paramref name="Host"/>:<paramref name="Port"/>.</summary>
public record AgentTerminalJobDto(
    Guid Id, string Provider, string Host, int Port, CardTerminalPaymentKind Kind, decimal Amount, string? OriginalTransactionRef);

/// <summary>The agent's report. <paramref name="Outcome"/> is approved, declined, failed or unknown.</summary>
public record AgentTerminalResultDto(string Outcome, string? ApprovalCode, string? TransactionRef, string? CardLast4, string? Error);
