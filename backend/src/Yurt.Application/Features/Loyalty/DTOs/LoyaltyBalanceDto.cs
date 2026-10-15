namespace Yurt.Application.Features.Loyalty.DTOs;

/// <param name="Enabled">Loyalty feature flag is on.</param>
/// <param name="Available">iiko responded — when false the client should show a "temporarily unavailable" state.</param>
/// <param name="Linked">Customer is linked to an iiko wallet.</param>
/// <param name="Balance">Current bonus balance, null when unavailable/unlinked.</param>
/// <param name="EarnPercent">Percent of order total earned as points.</param>
public record LoyaltyBalanceDto(
    bool Enabled,
    bool Available,
    bool Linked,
    decimal? Balance,
    decimal EarnPercent);
