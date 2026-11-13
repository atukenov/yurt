using System.Net;
using System.Security.Cryptography;
using System.Text;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;
using Yurt.Application.Common.Interfaces;
using Yurt.Application.Common.Models;
using Yurt.Domain.Entities;

namespace Yurt.Application.Features.CardTerminals;

/// <summary>
/// Card terminals (Phase 6, <c>own_pos</c>): the till sends an amount to a bank terminal and reads the result back.
/// Rules that keep the money safe:
/// a charge is Approved only when the terminal says so; a missing answer is Unknown, never "paid" or "failed";
/// an Unknown charge blocks a new charge on the same terminal until staff look at the slip and resolve it;
/// a refund is only for an Approved charge and never for more than is left.
/// Nothing here runs unless a branch has a terminal row, so a cafe that does not use terminals is unaffected.
/// </summary>
public class CardTerminalService(
    IApplicationDbContext db,
    IEnumerable<ICardTerminalAdapter> allAdapters,
    IConfiguration configuration,
    ICurrentUserService user,
    IAuditLogService audit)
{
    /// <summary>Adapters this server may use. The simulator approves fake cards, so it counts only when the config allows it.</summary>
    private IEnumerable<ICardTerminalAdapter> adapters => allAdapters.Where(a =>
        a.Provider != CardTerminalProviders.Simulator || string.Equals(configuration["CardTerminals:AllowSimulator"], "true", StringComparison.OrdinalIgnoreCase));

    private const int MaxTerminals = 30;
    private const decimal MaxAmount = 100_000_000m;

    /// <summary>An agent should take a request within this time, or it is marked Failed (the agent is off).</summary>
    public static readonly TimeSpan PickupTimeout = TimeSpan.FromMinutes(2);

    /// <summary>The agent should answer within this time after taking a request, or it is marked Unknown.</summary>
    public static readonly TimeSpan AnswerTimeout = TimeSpan.FromMinutes(3);

    // ---- Terminals -------------------------------------------------------------------------------------------------

    public async Task<List<CardTerminalDto>> ListAsync(CancellationToken ct = default)
        => (await db.CardTerminals.AsNoTracking().OrderBy(t => t.Name).ToListAsync(ct)).Select(ToDto).ToList();

    public async Task<Result<CardTerminalDto>> CreateAsync(SaveCardTerminalDto dto, CancellationToken ct = default)
    {
        if (await db.CardTerminals.CountAsync(ct) >= MaxTerminals) return Result<CardTerminalDto>.Failure("Too many terminals.");
        var terminal = new CardTerminal();
        var error = await ApplyAsync(terminal, dto, ct);
        if (error is not null) return Result<CardTerminalDto>.Failure(error);
        db.CardTerminals.Add(terminal);
        await db.SaveChangesAsync(ct);
        await audit.LogAsync("CardTerminalCreated", "CardTerminal", terminal.Id.ToString(), $"{terminal.Name} {terminal.Provider}", ct);
        return Result<CardTerminalDto>.Success(ToDto(terminal), 201);
    }

    public async Task<Result<CardTerminalDto>> UpdateAsync(Guid id, SaveCardTerminalDto dto, CancellationToken ct = default)
    {
        var terminal = await db.CardTerminals.FirstOrDefaultAsync(t => t.Id == id, ct);
        if (terminal is null) return Result<CardTerminalDto>.NotFound("Terminal not found.");
        var error = await ApplyAsync(terminal, dto, ct);
        if (error is not null) return Result<CardTerminalDto>.Failure(error);
        await db.SaveChangesAsync(ct);
        await audit.LogAsync("CardTerminalUpdated", "CardTerminal", id.ToString(), $"{terminal.Name} {terminal.Provider}", ct);
        return Result<CardTerminalDto>.Success(ToDto(terminal));
    }

    public async Task<Result<bool>> DeleteAsync(Guid id, CancellationToken ct = default)
    {
        var terminal = await db.CardTerminals.FirstOrDefaultAsync(t => t.Id == id, ct);
        if (terminal is null) return Result<bool>.NotFound("Terminal not found.");
        if (await db.CardTerminalPayments.AnyAsync(p => p.TerminalId == id, ct))
            return Result<bool>.Failure("This terminal has payments in the history. Switch it off instead of deleting it.");
        db.CardTerminals.Remove(terminal);
        await db.SaveChangesAsync(ct);
        await audit.LogAsync("CardTerminalDeleted", "CardTerminal", id.ToString(), terminal.Name, ct);
        return Result<bool>.Success(true);
    }

    private async Task<string?> ApplyAsync(CardTerminal terminal, SaveCardTerminalDto dto, CancellationToken ct)
    {
        var name = (dto.Name ?? string.Empty).Trim();
        if (name.Length is 0 or > 50) return "Give the terminal a name of up to 50 characters.";
        var provider = (dto.Provider ?? string.Empty).Trim().ToLowerInvariant();
        if (!CardTerminalProviders.All.Contains(provider)) return "Unknown terminal type.";
        if (!await db.Locations.AnyAsync(l => l.Id == dto.LocationId, ct)) return "Branch not found.";

        var host = (dto.Host ?? string.Empty).Trim();
        var port = dto.Port ?? 8080;
        if (host.Length > 0 && !IsPlainHost(host)) return "The address must be an IP address or a name, like 192.168.1.60.";
        if (port is < 1 or > 65535) return "The port must be between 1 and 65535.";
        // The simulator approves fake cards, so it exists only where the server is told to allow it (tests, demos, dev.sh).
        if (provider == CardTerminalProviders.Simulator && !adapters.Any(a => a.Provider == provider)) return "The simulator terminal is not enabled on this server.";
        if (provider != CardTerminalProviders.Simulator && host.Length == 0) return "Enter the terminal's address on the cafe network.";
        // A cloud server can only reach a terminal that is public, which no bank terminal is: it must go through the agent.
        if (!dto.ViaAgent && !adapters.Any(a => a.Provider == provider)) return "This terminal type can only be used through the cafe agent.";

        terminal.LocationId = dto.LocationId;
        terminal.Name = name;
        terminal.Provider = provider;
        terminal.Host = host;
        terminal.Port = port;
        terminal.ViaAgent = dto.ViaAgent;
        terminal.Active = dto.Active;
        return null;
    }

    private static bool IsPlainHost(string host)
        => host.Length <= 100 && (IPAddress.TryParse(host, out _) || host.All(c => char.IsAsciiLetterOrDigit(c) || c is '.' or '-'));

    // ---- Payments --------------------------------------------------------------------------------------------------

    public async Task<Result<CardPaymentDto>> StartAsync(StartCardPaymentDto dto, CancellationToken ct = default)
    {
        if (dto.Amount <= 0 || dto.Amount > MaxAmount) return Result<CardPaymentDto>.Failure("Enter an amount above zero.");
        // The terminals take whole tenge (Paloma rounds the same way); refuse cents rather than silently charge another sum.
        if (dto.Amount != decimal.Truncate(dto.Amount)) return Result<CardPaymentDto>.Failure("The amount must be in whole tenge.");

        var terminal = await db.CardTerminals.FirstOrDefaultAsync(t => t.Id == dto.TerminalId, ct);
        if (terminal is null) return Result<CardPaymentDto>.NotFound("Terminal not found.");
        if (!terminal.Active) return Result<CardPaymentDto>.Failure("This terminal is switched off.");

        await SweepAsync(terminal.Id, ct);
        var busy = await db.CardTerminalPayments.AnyAsync(p => p.TerminalId == terminal.Id
            && (p.Status == CardTerminalPaymentStatus.Pending || p.Status == CardTerminalPaymentStatus.Sending
                || p.Status == CardTerminalPaymentStatus.Unknown), ct);
        if (busy) return Result<CardPaymentDto>.Failure("The terminal is busy or has a payment that needs checking first.", 409);

        var payment = new CardTerminalPayment
        {
            TerminalId = terminal.Id, LocationId = terminal.LocationId, OrderId = dto.OrderId,
            Kind = CardTerminalPaymentKind.Sale, Amount = dto.Amount, CreatedBy = user.UserId?.ToString()
        };
        db.CardTerminalPayments.Add(payment);
        await db.SaveChangesAsync(ct);
        await audit.LogAsync("CardPaymentStarted", "CardTerminalPayment", payment.Id.ToString(), $"{dto.Amount}", ct);

        await RunDirectAsync(terminal, payment, ct);
        return Result<CardPaymentDto>.Success(ToDto(payment), 201);
    }

    public async Task<Result<CardPaymentDto>> RefundAsync(Guid paymentId, CancellationToken ct = default)
    {
        var original = await db.CardTerminalPayments.FirstOrDefaultAsync(p => p.Id == paymentId, ct);
        if (original is null) return Result<CardPaymentDto>.NotFound("Payment not found.");
        if (original.Kind != CardTerminalPaymentKind.Sale || original.Status != CardTerminalPaymentStatus.Approved)
            return Result<CardPaymentDto>.Failure("Only an approved card payment can be refunded.");

        var terminal = await db.CardTerminals.FirstOrDefaultAsync(t => t.Id == original.TerminalId, ct);
        if (terminal is null || !terminal.Active) return Result<CardPaymentDto>.Failure("The terminal of this payment is switched off.");

        await SweepAsync(terminal.Id, ct);
        var refunds = await db.CardTerminalPayments
            .Where(p => p.OriginalPaymentId == original.Id
                && (p.Status == CardTerminalPaymentStatus.Approved || p.Status == CardTerminalPaymentStatus.Pending
                    || p.Status == CardTerminalPaymentStatus.Sending || p.Status == CardTerminalPaymentStatus.Unknown))
            .AnyAsync(ct);
        if (refunds) return Result<CardPaymentDto>.Failure("This payment already has a refund (or one is waiting).", 409);
        var busy = await db.CardTerminalPayments.AnyAsync(p => p.TerminalId == terminal.Id
            && (p.Status == CardTerminalPaymentStatus.Pending || p.Status == CardTerminalPaymentStatus.Sending
                || p.Status == CardTerminalPaymentStatus.Unknown), ct);
        if (busy) return Result<CardPaymentDto>.Failure("The terminal is busy or has a payment that needs checking first.", 409);

        var refund = new CardTerminalPayment
        {
            TerminalId = terminal.Id, LocationId = terminal.LocationId, OrderId = original.OrderId,
            Kind = CardTerminalPaymentKind.Refund, OriginalPaymentId = original.Id, Amount = original.Amount,
            CreatedBy = user.UserId?.ToString()
        };
        db.CardTerminalPayments.Add(refund);
        await db.SaveChangesAsync(ct);
        await audit.LogAsync("CardRefundStarted", "CardTerminalPayment", refund.Id.ToString(), $"{refund.Amount}", ct);

        await RunDirectAsync(terminal, refund, ct);
        return Result<CardPaymentDto>.Success(ToDto(refund), 201);
    }

    /// <summary>The till asks for the answer; it keeps asking every second or two until the status is no longer Pending/Sending.</summary>
    public async Task<Result<CardPaymentDto>> GetAsync(Guid id, CancellationToken ct = default)
    {
        var terminalId = await db.CardTerminalPayments.Where(p => p.Id == id).Select(p => (Guid?)p.TerminalId).FirstOrDefaultAsync(ct);
        if (terminalId is null) return Result<CardPaymentDto>.NotFound("Payment not found.");
        await SweepAsync(terminalId.Value, ct);
        var payment = await db.CardTerminalPayments.AsNoTracking().FirstAsync(p => p.Id == id, ct);
        return Result<CardPaymentDto>.Success(ToDto(payment));
    }

    public async Task<List<CardPaymentDto>> RecentAsync(int limit = 30, CancellationToken ct = default)
        => (await db.CardTerminalPayments.AsNoTracking().OrderByDescending(p => p.CreatedAt)
            .Take(Math.Clamp(limit, 1, 100)).ToListAsync(ct)).Select(ToDto).ToList();

    /// <summary>Cancel a request nobody has taken yet. Once the terminal has it, cancelling is done on the terminal.</summary>
    public async Task<Result<CardPaymentDto>> CancelAsync(Guid id, CancellationToken ct = default)
    {
        var changed = await db.CardTerminalPayments
            .Where(p => p.Id == id && p.Status == CardTerminalPaymentStatus.Pending)
            .ExecuteUpdateAsync(s => s
                .SetProperty(p => p.Status, CardTerminalPaymentStatus.Cancelled)
                .SetProperty(p => p.CompletedAt, DateTime.UtcNow), ct);
        var payment = await db.CardTerminalPayments.AsNoTracking().FirstOrDefaultAsync(p => p.Id == id, ct);
        if (payment is null) return Result<CardPaymentDto>.NotFound("Payment not found.");
        if (changed == 0) return Result<CardPaymentDto>.Failure("The terminal already has this payment. Cancel it on the terminal.", 409);
        await audit.LogAsync("CardPaymentCancelled", "CardTerminalPayment", id.ToString(), null, ct);
        return Result<CardPaymentDto>.Success(ToDto(payment));
    }

    /// <summary>
    /// Staff looked at the terminal's slip or the bank app for an Unknown payment and say whether the card was charged.
    /// This is the only way out of Unknown, and it is written to the audit log.
    /// </summary>
    public async Task<Result<CardPaymentDto>> ResolveAsync(Guid id, ResolveCardPaymentDto dto, CancellationToken ct = default)
    {
        var payment = await db.CardTerminalPayments.FirstOrDefaultAsync(p => p.Id == id, ct);
        if (payment is null) return Result<CardPaymentDto>.NotFound("Payment not found.");
        if (payment.Status != CardTerminalPaymentStatus.Unknown) return Result<CardPaymentDto>.Failure("Only a payment that needs checking can be resolved.");
        payment.Status = dto.Charged ? CardTerminalPaymentStatus.Approved : CardTerminalPaymentStatus.Failed;
        payment.Error = dto.Charged ? null : "Checked by staff: the card was not charged.";
        payment.TransactionRef = string.IsNullOrWhiteSpace(dto.TransactionRef) ? payment.TransactionRef : dto.TransactionRef.Trim()[..Math.Min(dto.TransactionRef.Trim().Length, 40)];
        payment.CompletedAt = DateTime.UtcNow;
        await db.SaveChangesAsync(ct);
        await audit.LogAsync("CardPaymentResolved", "CardTerminalPayment", id.ToString(), dto.Charged ? "charged" : "not charged", ct);
        return Result<CardPaymentDto>.Success(ToDto(payment));
    }

    // ---- Server talks to the terminal itself (simulator, or a future adapter that can reach the terminal) -------------

    private async Task RunDirectAsync(CardTerminal terminal, CardTerminalPayment payment, CancellationToken ct)
    {
        if (terminal.ViaAgent) return; // the agent picks it up
        var adapter = adapters.FirstOrDefault(a => a.Provider == terminal.Provider);
        if (adapter is null)
        {
            Finish(payment, TerminalResult.Failed("No adapter for this terminal type."));
            await db.SaveChangesAsync(ct);
            return;
        }

        string? originalRef = null;
        if (payment.Kind == CardTerminalPaymentKind.Refund)
            originalRef = await db.CardTerminalPayments.Where(p => p.Id == payment.OriginalPaymentId).Select(p => p.TransactionRef).FirstOrDefaultAsync(ct);

        payment.Status = CardTerminalPaymentStatus.Sending;
        payment.SentAt = DateTime.UtcNow;
        await db.SaveChangesAsync(ct);

        TerminalResult result;
        try
        {
            var target = new TerminalTarget(terminal.Host, terminal.Port);
            var request = new TerminalRequest(payment.Id.ToString("N"), payment.Amount, originalRef);
            result = payment.Kind == CardTerminalPaymentKind.Sale
                ? await adapter.ChargeAsync(target, request, ct)
                : await adapter.RefundAsync(target, request, ct);
        }
        catch (OperationCanceledException) { result = TerminalResult.Unknown("The request was interrupted before an answer came."); }
        catch (Exception) { result = TerminalResult.Unknown("The terminal adapter failed after the request was sent."); }

        Finish(payment, result);
        await db.SaveChangesAsync(CancellationToken.None);
    }

    private static void Finish(CardTerminalPayment payment, TerminalResult result)
    {
        payment.Status = result.Outcome switch
        {
            TerminalOutcome.Approved => CardTerminalPaymentStatus.Approved,
            TerminalOutcome.Declined => CardTerminalPaymentStatus.Declined,
            TerminalOutcome.Failed => CardTerminalPaymentStatus.Failed,
            _ => CardTerminalPaymentStatus.Unknown
        };
        payment.ApprovalCode = Clip(result.ApprovalCode, 20);
        payment.TransactionRef = Clip(result.TransactionRef, 40);
        payment.CardLast4 = result.CardLast4 is { Length: 4 } l && l.All(char.IsAsciiDigit) ? l : null;
        payment.Error = result.Outcome == TerminalOutcome.Approved ? null : Clip(result.Error, 200) ?? "No details from the terminal.";
        payment.CompletedAt = DateTime.UtcNow;
    }

    // ---- Agent ------------------------------------------------------------------------------------------------------

    /// <summary>Hands the branch agent its waiting requests (one at a time per terminal) and marks them Sending.</summary>
    public async Task<Result<List<AgentTerminalJobDto>>> ClaimAsync(string? key, CancellationToken ct = default)
    {
        var agent = await FindAgentAsync(key, ct);
        if (agent is null) return Result<List<AgentTerminalJobDto>>.Unauthorized("Unknown agent key.");
        var now = DateTime.UtcNow;
        await db.PrintAgents.Where(a => a.Id == agent.Id).ExecuteUpdateAsync(s => s.SetProperty(a => a.LastSeenAt, now), ct);
        await SweepLocationAsync(agent.LocationId, ct);

        var waiting = await db.CardTerminalPayments.AsNoTracking()
            .Where(p => p.LocationId == agent.LocationId && p.Status == CardTerminalPaymentStatus.Pending
                && p.Terminal.ViaAgent && p.Terminal.Active)
            .OrderBy(p => p.CreatedAt).Take(5)
            .Select(p => new { p.Id, p.Terminal.Provider, p.Terminal.Host, p.Terminal.Port, p.Kind, p.Amount, p.OriginalPaymentId })
            .ToListAsync(ct);

        var jobs = new List<AgentTerminalJobDto>();
        foreach (var w in waiting)
        {
            var taken = await db.CardTerminalPayments
                .Where(p => p.Id == w.Id && p.Status == CardTerminalPaymentStatus.Pending)
                .ExecuteUpdateAsync(s => s
                    .SetProperty(p => p.Status, CardTerminalPaymentStatus.Sending)
                    .SetProperty(p => p.SentAt, now), ct);
            if (taken == 0) continue; // someone else took it
            string? originalRef = null;
            if (w.OriginalPaymentId is { } oid)
                originalRef = await db.CardTerminalPayments.Where(p => p.Id == oid).Select(p => p.TransactionRef).FirstOrDefaultAsync(ct);
            jobs.Add(new AgentTerminalJobDto(w.Id, w.Provider, w.Host, w.Port, w.Kind, w.Amount, originalRef));
        }
        return Result<List<AgentTerminalJobDto>>.Success(jobs);
    }

    public async Task<Result<bool>> ReportAsync(string? key, Guid id, AgentTerminalResultDto dto, CancellationToken ct = default)
    {
        var agent = await FindAgentAsync(key, ct);
        if (agent is null) return Result<bool>.Unauthorized("Unknown agent key.");
        var payment = await db.CardTerminalPayments.FirstOrDefaultAsync(p => p.Id == id && p.LocationId == agent.LocationId, ct);
        if (payment is null) return Result<bool>.NotFound("Payment not found.");
        // An answer after staff already resolved or cancelled it is ignored, so a late report can never undo their decision.
        if (payment.Status != CardTerminalPaymentStatus.Sending && payment.Status != CardTerminalPaymentStatus.Unknown)
            return Result<bool>.Success(false);

        var outcome = (dto.Outcome ?? string.Empty).Trim().ToLowerInvariant() switch
        {
            "approved" => TerminalOutcome.Approved,
            "declined" => TerminalOutcome.Declined,
            "failed" => TerminalOutcome.Failed,
            _ => TerminalOutcome.Unknown
        };
        Finish(payment, new TerminalResult(outcome, dto.ApprovalCode, dto.TransactionRef, dto.CardLast4, dto.Error));
        await db.SaveChangesAsync(ct);
        return Result<bool>.Success(true);
    }

    private async Task<PrintAgent?> FindAgentAsync(string? key, CancellationToken ct)
    {
        if (string.IsNullOrWhiteSpace(key) || key.Length > 200) return null;
        var hash = Convert.ToHexStringLower(SHA256.HashData(Encoding.UTF8.GetBytes(key.Trim())));
        return await db.PrintAgents.AsNoTracking().FirstOrDefaultAsync(a => a.KeyHash == hash, ct);
    }

    // ---- Timeouts ---------------------------------------------------------------------------------------------------

    private Task SweepAsync(Guid terminalId, CancellationToken ct) => SweepWhereAsync(p => p.TerminalId == terminalId, ct);

    private Task SweepLocationAsync(Guid locationId, CancellationToken ct) => SweepWhereAsync(p => p.LocationId == locationId, ct);

    /// <summary>
    /// Pending too long: nobody took it, nothing was sent, so Failed. Sending too long: it may have been charged, so Unknown.
    /// Direct (server-side) payments never wait here because they finish in the same request.
    /// </summary>
    private async Task SweepWhereAsync(System.Linq.Expressions.Expression<Func<CardTerminalPayment, bool>> scope, CancellationToken ct)
    {
        var now = DateTime.UtcNow;
        var pickupCutoff = now - PickupTimeout;
        var answerCutoff = now - AnswerTimeout;
        await db.CardTerminalPayments.Where(scope)
            .Where(p => p.Status == CardTerminalPaymentStatus.Pending && p.CreatedAt < pickupCutoff)
            .ExecuteUpdateAsync(s => s
                .SetProperty(p => p.Status, CardTerminalPaymentStatus.Failed)
                .SetProperty(p => p.Error, "The cafe agent did not pick this up. Nothing was charged.")
                .SetProperty(p => p.CompletedAt, now), ct);
        await db.CardTerminalPayments.Where(scope)
            .Where(p => p.Status == CardTerminalPaymentStatus.Sending && p.SentAt < answerCutoff)
            .ExecuteUpdateAsync(s => s
                .SetProperty(p => p.Status, CardTerminalPaymentStatus.Unknown)
                .SetProperty(p => p.Error, "No answer from the terminal. Check its slip or the bank app before charging again.")
                .SetProperty(p => p.CompletedAt, now), ct);
    }

    // ---- Mapping ----------------------------------------------------------------------------------------------------

    private static string? Clip(string? s, int max)
    {
        s = s?.Trim();
        return string.IsNullOrEmpty(s) ? null : s.Length <= max ? s : s[..max];
    }

    private static CardTerminalDto ToDto(CardTerminal t)
        => new(t.Id, t.LocationId, t.Name, t.Provider, t.Host, t.Port, t.ViaAgent, t.Active);

    internal static CardPaymentDto ToDto(CardTerminalPayment p)
        => new(p.Id, p.TerminalId, p.OrderId, p.Kind, p.Status, p.Amount, p.ApprovalCode, p.TransactionRef,
            p.CardLast4, p.Error, p.CreatedAt, p.CompletedAt);
}
