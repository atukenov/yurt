using Microsoft.EntityFrameworkCore;
using Yurt.Application.Common.Interfaces;
using Yurt.Domain.Entities;

namespace Yurt.Application.Features.Loyalty;

/// <summary>
/// The bonus wallet kept in our own database (Phase 6, <c>own_pos</c>), the counterpart of <see cref="IikoLoyaltyLedger"/>.
/// The balance is the sum of a customer's entries; a hold is a negative entry that a release cancels once.
/// Rows are cafe-scoped by the context's tenant filter, so one cafe can never see another's wallets.
/// </summary>
public class NativeLoyaltyLedger(IApplicationDbContext db) : ILoyaltyLedger
{
    public async Task<LedgerAccount?> GetAccountByPhoneAsync(string phone, CancellationToken ct = default)
    {
        var account = await db.LoyaltyAccounts.FirstOrDefaultAsync(a => a.Phone == phone, ct);
        if (account == null) return null;

        var balances = new List<LedgerBalance>();
        if (account.WalletId != null)
            balances.Add(new LedgerBalance(account.WalletId.Value, await BalanceAsync(account.Id, ct)));
        return new LedgerAccount(account.Id, account.Phone, balances);
    }

    public async Task<Guid> CreateOrUpdateAccountAsync(string phone, string? name, string? surname, CancellationToken ct = default)
    {
        var account = await db.LoyaltyAccounts.FirstOrDefaultAsync(a => a.Phone == phone, ct);
        if (account == null)
        {
            account = new LoyaltyAccount { Phone = phone, Name = name, Surname = surname };
            db.LoyaltyAccounts.Add(account);
        }
        else
        {
            account.Name = name;
            account.Surname = surname;
            account.UpdatedAt = DateTime.UtcNow;
        }
        await db.SaveChangesAsync(ct);
        return account.Id;
    }

    public async Task<Guid> EnrollAsync(Guid accountId, CancellationToken ct = default)
    {
        var account = await FindAsync(accountId, ct);
        if (account.WalletId == null)
        {
            account.WalletId = Guid.NewGuid();
            account.UpdatedAt = DateTime.UtcNow;
            await db.SaveChangesAsync(ct);
        }
        return account.WalletId.Value;
    }

    public async Task TopupAsync(Guid accountId, Guid walletId, decimal sum, string? comment, CancellationToken ct = default)
    {
        await RequireWalletAsync(accountId, walletId, ct);
        RequirePositive(sum);
        Add(accountId, LoyaltyEntryKind.Topup, sum, comment);
        await db.SaveChangesAsync(ct);
    }

    public async Task ChargeoffAsync(Guid accountId, Guid walletId, decimal sum, string? comment, CancellationToken ct = default)
    {
        var account = await RequireWalletAsync(accountId, walletId, ct);
        RequirePositive(sum);
        await RequireFundsAsync(accountId, sum, ct);
        Add(accountId, LoyaltyEntryKind.Chargeoff, -sum, comment);
        await SaveSpendAsync(account, ct);
    }

    public async Task<Guid> HoldAsync(Guid accountId, Guid walletId, decimal sum, string? comment, CancellationToken ct = default)
    {
        var account = await RequireWalletAsync(accountId, walletId, ct);
        RequirePositive(sum);
        await RequireFundsAsync(accountId, sum, ct);
        var hold = Add(accountId, LoyaltyEntryKind.Hold, -sum, comment);
        await SaveSpendAsync(account, ct);
        return hold.Id;
    }

    public async Task CancelHoldAsync(Guid holdId, CancellationToken ct = default)
    {
        var hold = await db.LoyaltyEntries.FirstOrDefaultAsync(e => e.Id == holdId && e.Kind == LoyaltyEntryKind.Hold, ct)
            ?? throw new InvalidOperationException("Hold not found.");

        // One atomic flip decides who releases: of two simultaneous calls only one sees a row change, so points come back once.
        var flipped = await db.LoyaltyEntries
            .Where(e => e.Id == holdId && !e.Released)
            .ExecuteUpdateAsync(u => u.SetProperty(e => e.Released, true).SetProperty(e => e.UpdatedAt, DateTime.UtcNow), ct);
        if (flipped == 0) return;

        Add(hold.AccountId, LoyaltyEntryKind.HoldRelease, -hold.Amount, hold.Comment);
        await db.SaveChangesAsync(ct);
    }

    public async Task<List<LedgerTransaction>> GetTransactionsAsync(
        Guid accountId, DateTime dateFromUtc, DateTime dateToUtc, int pageSize = 200, CancellationToken ct = default)
    {
        var entries = await db.LoyaltyEntries
            .Where(e => e.AccountId == accountId && e.CreatedAt >= dateFromUtc && e.CreatedAt <= dateToUtc)
            .OrderByDescending(e => e.CreatedAt)
            .Take(pageSize)
            .ToListAsync(ct);

        return entries.Select(e => new LedgerTransaction(
            e.Id, e.CreatedAt, e.Amount, OrderSum: null, OrderNumber: null, PosOrderId: null,
            e.Kind.ToString(), BalanceAfter: null, e.Comment)).ToList();
    }

    private async Task<LoyaltyAccount> FindAsync(Guid accountId, CancellationToken ct)
        => await db.LoyaltyAccounts.FirstOrDefaultAsync(a => a.Id == accountId, ct)
           ?? throw new InvalidOperationException("Loyalty account not found.");

    private async Task<LoyaltyAccount> RequireWalletAsync(Guid accountId, Guid walletId, CancellationToken ct)
    {
        var account = await FindAsync(accountId, ct);
        if (account.WalletId != walletId) throw new InvalidOperationException("Wallet does not belong to this account.");
        return account;
    }

    /// <summary>
    /// Saves a spend. The account row is touched so its version changes: if another spend for the same account got in
    /// between our balance check and this save, the save fails instead of letting the balance go below zero.
    /// </summary>
    private async Task SaveSpendAsync(LoyaltyAccount account, CancellationToken ct)
    {
        account.UpdatedAt = DateTime.UtcNow;
        try { await db.SaveChangesAsync(ct); }
        catch (DbUpdateConcurrencyException)
        {
            throw new InvalidOperationException("The balance changed at the same moment. Please try again.");
        }
    }

    private static void RequirePositive(decimal sum)
    {
        if (sum <= 0) throw new ArgumentOutOfRangeException(nameof(sum), "Amount must be positive.");
    }

    private async Task RequireFundsAsync(Guid accountId, decimal sum, CancellationToken ct)
    {
        if (await BalanceAsync(accountId, ct) < sum) throw new InvalidOperationException("Not enough bonus points.");
    }

    private async Task<decimal> BalanceAsync(Guid accountId, CancellationToken ct)
        => await db.LoyaltyEntries.Where(e => e.AccountId == accountId).SumAsync(e => (decimal?)e.Amount, ct) ?? 0m;

    private LoyaltyEntry Add(Guid accountId, LoyaltyEntryKind kind, decimal amount, string? comment)
    {
        var entry = new LoyaltyEntry { AccountId = accountId, Kind = kind, Amount = amount, Comment = comment };
        db.LoyaltyEntries.Add(entry);
        return entry;
    }
}
