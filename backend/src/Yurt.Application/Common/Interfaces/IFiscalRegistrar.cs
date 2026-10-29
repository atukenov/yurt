using Yurt.Domain.Entities;
using Yurt.Domain.Enums;

namespace Yurt.Application.Common.Interfaces;

/// <summary>
/// A cloud cash register that turns a sale into an official tax receipt (Phase 6, <c>own_pos</c>). One adapter per
/// provider (Webkassa, ReKassa). Yurt is not a cash register itself; the provider talks to the fiscal data operator.
/// Adapters must not throw for provider errors: they return a failed <see cref="FiscalResult"/> and the receipt
/// stays in the queue to be tried again, so a provider outage never blocks a sale.
/// </summary>
public interface IFiscalRegistrar
{
    /// <summary>Lowercase provider name stored on the cafe, for example "webkassa" or "rekassa".</summary>
    string Provider { get; }

    /// <summary>Sends one sale or return receipt. The provider opens its fiscal shift by itself if needed.</summary>
    Task<FiscalResult> SendReceiptAsync(FiscalCredentials credentials, FiscalReceiptRequest receipt, CancellationToken ct = default);

    /// <summary>Closes the fiscal shift of one register (fiscal Z report).</summary>
    Task<FiscalResult> CloseShiftAsync(FiscalCredentials credentials, string registerNumber, CancellationToken ct = default);
}

/// <summary>The cafe's login at the provider (from the cafe's encrypted secrets).</summary>
/// <param name="Login">Empty for providers where each register logs in by its own number (ReKassa).</param>
/// <param name="Pin">Register PIN, only for providers that ask for it to close the shift (ReKassa).</param>
public record FiscalCredentials(string Login, string Password, string? ApiKey, string? Pin = null);

/// <param name="ExternalId">Our own id for the receipt, so the provider can spot a repeat after a lost answer.</param>
/// <param name="VatPercent">Null when the cafe does not pay VAT.</param>
public record FiscalReceiptRequest(
    string RegisterNumber,
    FiscalReceiptKind Kind,
    string ExternalId,
    IReadOnlyList<FiscalLine> Lines,
    IReadOnlyList<FiscalPayment> Payments,
    int? VatPercent);

/// <summary>One receipt line. <paramref name="Discount"/> is money off the whole line (order discount and points spread over the lines).</summary>
public record FiscalLine(string Name, int Quantity, decimal UnitPrice, decimal Discount);

public record FiscalPayment(PaymentMethod Method, decimal Amount);

public record FiscalResult(bool Succeeded, string? CheckNumber = null, string? TicketUrl = null, string? Error = null)
{
    public static FiscalResult Ok(string? checkNumber, string? ticketUrl) => new(true, checkNumber, ticketUrl);
    public static FiscalResult Fail(string error) => new(false, Error: error);
}
