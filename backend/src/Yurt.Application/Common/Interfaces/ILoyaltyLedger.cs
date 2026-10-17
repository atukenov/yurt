namespace Yurt.Application.Common.Interfaces;

/// <summary>
/// The customer's bonus wallet, whatever system keeps it (Phase 6). Today the only adapter is iiko
/// (<c>IikoLoyaltyLedger</c>); the self-developed <c>own_pos</c> module will add a ledger in our own database.
/// Calls throw on transport or ledger errors; <c>LoyaltyService</c> decides which failures are fatal.
/// </summary>
public interface ILoyaltyLedger
{
    /// <summary>The account for a phone number with its wallet balances, or null if there is none yet.</summary>
    Task<LedgerAccount?> GetAccountByPhoneAsync(string phone, CancellationToken ct = default);

    /// <summary>Create or update the account. Returns the account id.</summary>
    Task<Guid> CreateOrUpdateAccountAsync(string phone, string? name, string? surname, CancellationToken ct = default);

    /// <summary>Enroll the account into the loyalty program. Returns the wallet id.</summary>
    Task<Guid> EnrollAsync(Guid accountId, CancellationToken ct = default);

    /// <summary>Credit the wallet.</summary>
    Task TopupAsync(Guid accountId, Guid walletId, decimal sum, string? comment, CancellationToken ct = default);

    /// <summary>Debit the wallet.</summary>
    Task ChargeoffAsync(Guid accountId, Guid walletId, decimal sum, string? comment, CancellationToken ct = default);

    /// <summary>Reserve an amount on the wallet. Returns the hold id.</summary>
    Task<Guid> HoldAsync(Guid accountId, Guid walletId, decimal sum, string? comment, CancellationToken ct = default);

    /// <summary>Release a previously placed hold.</summary>
    Task CancelHoldAsync(Guid holdId, CancellationToken ct = default);

    /// <summary>Wallet movements over a date range, newest first (first page only).</summary>
    Task<List<LedgerTransaction>> GetTransactionsAsync(
        Guid accountId, DateTime dateFromUtc, DateTime dateToUtc, int pageSize = 200, CancellationToken ct = default);
}

public record LedgerBalance(Guid WalletId, decimal Balance);

public record LedgerAccount(Guid Id, string? Phone, List<LedgerBalance> Balances);

public record LedgerTransaction(
    Guid Id,
    DateTime WhenCreated,
    decimal Sum,
    decimal? OrderSum,
    int? OrderNumber,
    Guid? PosOrderId,
    string? TypeName,
    decimal? BalanceAfter,
    string? Comment);
