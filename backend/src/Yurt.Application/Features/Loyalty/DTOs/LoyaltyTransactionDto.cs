namespace Yurt.Application.Features.Loyalty.DTOs;

/// <summary>
/// One offsite (in-shop counter) purchase, reassembled from the one-or-two raw iiko ledger
/// rows it produces (a "RefillWalletFromOrder" earn row and/or a "PayFromWallet" spend row,
/// both sharing the same POS order) into a single customer-facing breakdown.
/// </summary>
/// <param name="WhenCreated">When the purchase happened, in UTC.</param>
/// <param name="OrderNumber">POS order number, when known.</param>
/// <param name="OrderTotal">Full order total in KZT, when iiko reported it on the underlying transaction(s).</param>
/// <param name="KztPaid">Portion of <see cref="OrderTotal"/> paid in cash/card — <c>OrderTotal - BonusSpent</c>, when <see cref="OrderTotal"/> is known.</param>
/// <param name="BonusSpent">Bonus points redeemed against this purchase (0 if none).</param>
/// <param name="BonusEarned">Bonus points credited from this purchase's cash/card-paid portion (0 if none).</param>
/// <param name="BalanceAfter">Wallet balance immediately after this purchase, if iiko reported it.</param>
public record OffsitePurchaseDto(
    DateTime WhenCreated,
    int? OrderNumber,
    decimal? OrderTotal,
    decimal? KztPaid,
    decimal BonusSpent,
    decimal BonusEarned,
    decimal? BalanceAfter);

/// <param name="Enabled">Loyalty feature flag is on.</param>
/// <param name="Available">iiko responded — when false the client should show a "temporarily unavailable" state.</param>
/// <param name="Linked">Customer is linked to an iiko wallet.</param>
/// <param name="Transactions">Offsite purchases only — app-order earn/spend already appears in order history.</param>
public record LoyaltyHistoryDto(
    bool Enabled,
    bool Available,
    bool Linked,
    List<OffsitePurchaseDto> Transactions);
