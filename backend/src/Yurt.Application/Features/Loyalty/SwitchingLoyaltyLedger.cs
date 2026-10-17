using Yurt.Application.Common.Interfaces;

namespace Yurt.Application.Features.Loyalty;

/// <summary>The two wallet choices a cafe can have (stored in <c>Tenant.LoyaltyLedger</c>).</summary>
public static class LoyaltyLedgerModes
{
    public const string Iiko = "iiko";
    public const string Native = "native";

    public static bool IsValid(string? mode) => mode is Iiko or Native;
}

/// <summary>
/// The wallet the rest of the app talks to. Each call goes to the iiko wallet or the native one, depending on the
/// current cafe's setting; a cafe with no setting keeps using iiko, exactly as before.
/// </summary>
public class SwitchingLoyaltyLedger(
    ITenantContext tenant,
    ITenantDirectory directory,
    IikoLoyaltyLedger iikoLedger,
    NativeLoyaltyLedger nativeLedger) : ILoyaltyLedger, ILoyaltyLedgerChoice
{
    public bool UsesNativeLedger
        => directory.GetActive().FirstOrDefault(t => t.Id == tenant.TenantId)?.LoyaltyLedger == LoyaltyLedgerModes.Native;

    private ILoyaltyLedger Current => UsesNativeLedger ? nativeLedger : iikoLedger;

    public Task<LedgerAccount?> GetAccountByPhoneAsync(string phone, CancellationToken ct = default)
        => Current.GetAccountByPhoneAsync(phone, ct);

    public Task<Guid> CreateOrUpdateAccountAsync(string phone, string? name, string? surname, CancellationToken ct = default)
        => Current.CreateOrUpdateAccountAsync(phone, name, surname, ct);

    public Task<Guid> EnrollAsync(Guid accountId, CancellationToken ct = default)
        => Current.EnrollAsync(accountId, ct);

    public Task TopupAsync(Guid accountId, Guid walletId, decimal sum, string? comment, CancellationToken ct = default)
        => Current.TopupAsync(accountId, walletId, sum, comment, ct);

    public Task ChargeoffAsync(Guid accountId, Guid walletId, decimal sum, string? comment, CancellationToken ct = default)
        => Current.ChargeoffAsync(accountId, walletId, sum, comment, ct);

    public Task<Guid> HoldAsync(Guid accountId, Guid walletId, decimal sum, string? comment, CancellationToken ct = default)
        => Current.HoldAsync(accountId, walletId, sum, comment, ct);

    public Task CancelHoldAsync(Guid holdId, CancellationToken ct = default)
        => Current.CancelHoldAsync(holdId, ct);

    public Task<List<LedgerTransaction>> GetTransactionsAsync(
        Guid accountId, DateTime dateFromUtc, DateTime dateToUtc, int pageSize = 200, CancellationToken ct = default)
        => Current.GetTransactionsAsync(accountId, dateFromUtc, dateToUtc, pageSize, ct);
}
