namespace Yurt.Application.Common.Interfaces;

/// <summary>
/// Low-level client for the iikoCloud (iikoTransport) API.
/// All calls throw <see cref="IikoApiException"/> on transport or API errors —
/// callers decide whether loyalty failures are fatal for their flow.
/// </summary>
public interface IIikoApiClient
{
    /// <summary>Get customer info (incl. wallet balances) by phone. Null if not registered in iiko.</summary>
    Task<IikoCustomerInfo?> GetCustomerByPhoneAsync(string phone, CancellationToken ct = default);

    /// <summary>Create or update the iiko customer record. Returns the iiko customer id.</summary>
    Task<Guid> CreateOrUpdateCustomerAsync(
        string phone, string? name, string? surname, CancellationToken ct = default);

    /// <summary>Enroll the customer into the configured loyalty program. Returns the program wallet id.</summary>
    Task<Guid> AddCustomerToProgramAsync(Guid iikoCustomerId, CancellationToken ct = default);

    /// <summary>Credit (refill) the customer's wallet balance.</summary>
    Task TopupAsync(Guid iikoCustomerId, Guid walletId, decimal sum, string? comment, CancellationToken ct = default);

    /// <summary>Debit (withdraw) from the customer's wallet balance.</summary>
    Task ChargeoffAsync(Guid iikoCustomerId, Guid walletId, decimal sum, string? comment, CancellationToken ct = default);

    /// <summary>Hold (reserve) an amount on the customer's wallet. Returns the hold transaction id.</summary>
    Task<Guid> HoldAsync(Guid iikoCustomerId, Guid walletId, decimal sum, string? comment, CancellationToken ct = default);

    /// <summary>Release a previously placed hold.</summary>
    Task CancelHoldAsync(Guid holdTransactionId, CancellationToken ct = default);

    // ── Order push (reporting/kitchen-routing side-channel; see IikoOrderSyncService) ──

    /// <summary>iiko's product/size catalog, for the admin menu-mapping UI. Modifiers are excluded.</summary>
    Task<List<IikoNomenclatureProduct>> GetNomenclatureAsync(CancellationToken ct = default);

    /// <summary>Payment types configured for the organization — for the admin "paid in app" reference lookup.</summary>
    Task<List<IikoPaymentType>> GetPaymentTypesAsync(CancellationToken ct = default);

    /// <summary>Terminal groups configured for the organization — for the per-location routing picker.</summary>
    Task<List<IikoTerminalGroup>> GetTerminalGroupsAsync(CancellationToken ct = default);

    /// <summary>
    /// Push an order into iiko as a real delivery order (self-pickup, one-time customer —
    /// not bound to the iiko loyalty customer, so iiko's own loyalty engine never touches
    /// the wallet the rest of this client already manages). Returns the created iiko order id.
    /// </summary>
    Task<Guid> CreateDeliveryOrderAsync(IikoCreateOrderRequest request, CancellationToken ct = default);

    /// <summary>Close a previously created delivery order.</summary>
    Task CloseDeliveryOrderAsync(Guid iikoOrderId, CancellationToken ct = default);

    /// <summary>Register (or update) the webhook endpoint iiko calls for delivery order update/error events.</summary>
    Task RegisterWebhookAsync(string webhookUrl, string authToken, CancellationToken ct = default);

    // ── Transaction history (issue #13) ─────────────────────────────────────

    /// <summary>
    /// Bonus-wallet transaction ledger for a customer over a date range — covers both
    /// app-driven wallet ops (topup/chargeoff/hold) and offsite (in-shop counter)
    /// purchases where staff applied the customer's card/phone at checkout.
    /// Wraps POST /api/1/loyalty/iiko/customer/transactions/by_date; returns the first
    /// page only (<paramref name="pageSize"/> items), newest first.
    /// </summary>
    Task<List<IikoTransaction>> GetCustomerTransactionsAsync(
        Guid iikoCustomerId, DateTime dateFromUtc, DateTime dateToUtc,
        int pageSize = 200, CancellationToken ct = default);
}

public record IikoWalletBalance(Guid Id, string? Name, int Type, decimal Balance);

public record IikoCustomerInfo(Guid Id, string? Phone, List<IikoWalletBalance> WalletBalances);

public record IikoNomenclatureSize(Guid ProductSizeId, string? Name);

public record IikoNomenclatureProduct(Guid ProductId, string Name, decimal Price, List<IikoNomenclatureSize> Sizes);

public record IikoPaymentType(Guid Id, string Name);

public record IikoTerminalGroup(Guid Id, string Name);

public record IikoOrderItemRequest(Guid ProductId, Guid? ProductSizeId, decimal Amount, decimal Price, string? Comment);

public record IikoCreateOrderRequest(
    Guid TerminalGroupId,
    string CustomerPhone,
    string? CustomerName,
    List<IikoOrderItemRequest> Items,
    decimal PaymentSum,
    string? Comment);

public record IikoTransaction(
    Guid Id,
    DateTime WhenCreated,
    decimal Sum,
    decimal? OrderSum,
    int? OrderNumber,
    Guid? PosOrderId,
    string? TypeName,
    bool? IsDelivery,
    decimal? BalanceBefore,
    decimal? BalanceAfter,
    string? Comment);

public class IikoApiException : Exception
{
    public int? StatusCode { get; }

    public IikoApiException(string message, int? statusCode = null, Exception? inner = null)
        : base(message, inner) => StatusCode = statusCode;
}
