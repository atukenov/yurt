using Yurt.Application.Common.Interfaces;

namespace Yurt.Infrastructure.CardTerminals;

/// <summary>
/// A pretend bank terminal for tests and demos. The amount decides the answer: ends in 13 = declined, ends in 99 =
/// no answer (Unknown), ends in 77 = terminal offline (Failed), anything else = approved. Never registered on a live
/// server (see <c>CardTerminals:AllowSimulator</c>).
/// </summary>
public class SimulatorCardTerminal : ICardTerminalAdapter
{
    public string Provider => "simulator";

    public Task<TerminalResult> ChargeAsync(TerminalTarget target, TerminalRequest request, CancellationToken ct = default)
        => Task.FromResult(Answer(request));

    public Task<TerminalResult> RefundAsync(TerminalTarget target, TerminalRequest request, CancellationToken ct = default)
        => Task.FromResult(string.IsNullOrEmpty(request.OriginalTransactionRef)
            ? TerminalResult.Declined("No bank number for the original payment.")
            : Answer(request with { Amount = 0 }));

    private static TerminalResult Answer(TerminalRequest request)
    {
        var last = (long)request.Amount % 100;
        if (request.Amount > 0 && last == 13) return TerminalResult.Declined("Card declined (simulator).");
        if (request.Amount > 0 && last == 99) return TerminalResult.Unknown("No answer (simulator).");
        if (request.Amount > 0 && last == 77) return TerminalResult.Failed("Terminal offline (simulator).");
        var id = request.Reference.Length >= 8 ? request.Reference[..8] : request.Reference;
        return TerminalResult.Approved("SIM" + id[..Math.Min(3, id.Length)].ToUpperInvariant(), "RRN" + id, "4242");
    }
}
