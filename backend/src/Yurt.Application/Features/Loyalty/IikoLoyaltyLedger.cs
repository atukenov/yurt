using Yurt.Application.Common.Interfaces;

namespace Yurt.Application.Features.Loyalty;

/// <summary>The iiko adapter for <see cref="ILoyaltyLedger"/>: a thin translation over <see cref="IIikoApiClient"/>.</summary>
public class IikoLoyaltyLedger(IIikoApiClient iiko) : ILoyaltyLedger
{
    public async Task<LedgerAccount?> GetAccountByPhoneAsync(string phone, CancellationToken ct = default)
    {
        var info = await iiko.GetCustomerByPhoneAsync(phone, ct);
        return info == null
            ? null
            : new LedgerAccount(info.Id, info.Phone, info.WalletBalances.Select(w => new LedgerBalance(w.Id, w.Balance)).ToList());
    }

    public Task<Guid> CreateOrUpdateAccountAsync(string phone, string? name, string? surname, CancellationToken ct = default)
        => iiko.CreateOrUpdateCustomerAsync(phone, name, surname, ct);

    public Task<Guid> EnrollAsync(Guid accountId, CancellationToken ct = default)
        => iiko.AddCustomerToProgramAsync(accountId, ct);

    public Task TopupAsync(Guid accountId, Guid walletId, decimal sum, string? comment, CancellationToken ct = default)
        => iiko.TopupAsync(accountId, walletId, sum, comment, ct);

    public Task ChargeoffAsync(Guid accountId, Guid walletId, decimal sum, string? comment, CancellationToken ct = default)
        => iiko.ChargeoffAsync(accountId, walletId, sum, comment, ct);

    public Task<Guid> HoldAsync(Guid accountId, Guid walletId, decimal sum, string? comment, CancellationToken ct = default)
        => iiko.HoldAsync(accountId, walletId, sum, comment, ct);

    public Task CancelHoldAsync(Guid holdId, CancellationToken ct = default)
        => iiko.CancelHoldAsync(holdId, ct);

    public async Task<List<LedgerTransaction>> GetTransactionsAsync(
        Guid accountId, DateTime dateFromUtc, DateTime dateToUtc, int pageSize = 200, CancellationToken ct = default)
    {
        var items = await iiko.GetCustomerTransactionsAsync(accountId, dateFromUtc, dateToUtc, pageSize, ct);
        return items.Select(t => new LedgerTransaction(
            t.Id, t.WhenCreated, t.Sum, t.OrderSum, t.OrderNumber, t.PosOrderId, t.TypeName, t.BalanceAfter, t.Comment)).ToList();
    }
}
