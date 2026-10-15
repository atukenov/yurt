using Yurt.Application.Common.Interfaces;

namespace Yurt.IntegrationTests.Helpers;

/// <summary>
/// In-memory stand-in for iikoCloud: records wallet operations, keeps balances,
/// models holds (holds reduce the reported available balance, like real iiko),
/// and can inject failures per operation.
/// </summary>
public class FakeIikoApiClient : IIikoApiClient
{
    public static readonly Guid CustomerId = Guid.Parse("aaaaaaaa-1111-2222-3333-444444444444");
    public static readonly Guid WalletId   = Guid.Parse("bbbbbbbb-1111-2222-3333-444444444444");

    private readonly Dictionary<string, decimal> _balancesByPhone = [];
    private readonly HashSet<string> _knownPhones = [];
    private readonly Dictionary<Guid, decimal> _activeHolds = [];
    private string? _lastPhone;

    public readonly List<(Guid CustomerId, Guid WalletId, decimal Sum, string? Comment)> TopupCalls = [];
    public readonly List<(Guid CustomerId, Guid WalletId, decimal Sum, string? Comment)> ChargeoffCalls = [];
    public readonly List<(Guid CustomerId, Guid WalletId, decimal Sum, string? Comment)> HoldCalls = [];
    public readonly List<Guid> CancelHoldCalls = [];

    // Failure injection
    public bool FailCancelHold { get; set; }
    public bool FailChargeoff { get; set; }
    public bool FailTopup { get; set; }
    public bool FailPush { get; set; }
    public bool FailClose { get; set; }

    public readonly List<IikoCreateOrderRequest> PushCalls = [];
    public readonly List<Guid> CloseCalls = [];
    public Guid NextIikoOrderId { get; set; } = Guid.NewGuid();

    // Simulates iiko's program/add returning the shared program walletId instead of the
    // customer's own balance-holding userWalletId — observed for already-enrolled customers.
    public Guid? AddToProgramReturnsWrongWalletId { get; set; }

    public int ActiveHoldCount => _activeHolds.Count;

    public void SetBalance(string phone, decimal balance)
    {
        _balancesByPhone[phone] = balance;
        _knownPhones.Add(phone);
        _lastPhone = phone;
    }

    public Task<IikoCustomerInfo?> GetCustomerByPhoneAsync(string phone, CancellationToken ct = default)
    {
        // Mirrors real iiko: unregistered phones report "not found", not a zero-balance customer.
        if (!_knownPhones.Contains(phone)) return Task.FromResult<IikoCustomerInfo?>(null);

        _balancesByPhone.TryGetValue(phone, out var balance);
        var available = balance - _activeHolds.Values.Sum();
        return Task.FromResult<IikoCustomerInfo?>(new IikoCustomerInfo(
            CustomerId, phone,
            [new IikoWalletBalance(WalletId, "Bonus", 1, available)]));
    }

    public Task<Guid> CreateOrUpdateCustomerAsync(
        string phone, string? name, string? surname, CancellationToken ct = default)
    {
        _lastPhone = phone;
        _knownPhones.Add(phone);
        _balancesByPhone.TryAdd(phone, 0);
        return Task.FromResult(CustomerId);
    }

    public Task<Guid> AddCustomerToProgramAsync(Guid iikoCustomerId, CancellationToken ct = default)
        => Task.FromResult(AddToProgramReturnsWrongWalletId ?? WalletId);

    public Task TopupAsync(Guid iikoCustomerId, Guid walletId, decimal sum, string? comment, CancellationToken ct = default)
    {
        if (FailTopup) throw new IikoApiException("topup failed (injected)");
        TopupCalls.Add((iikoCustomerId, walletId, sum, comment));
        if (_lastPhone != null)
            _balancesByPhone[_lastPhone] = _balancesByPhone.GetValueOrDefault(_lastPhone) + sum;
        return Task.CompletedTask;
    }

    public Task ChargeoffAsync(Guid iikoCustomerId, Guid walletId, decimal sum, string? comment, CancellationToken ct = default)
    {
        if (FailChargeoff) throw new IikoApiException("chargeoff failed (injected)");
        ChargeoffCalls.Add((iikoCustomerId, walletId, sum, comment));
        if (_lastPhone != null)
            _balancesByPhone[_lastPhone] = _balancesByPhone.GetValueOrDefault(_lastPhone) - sum;
        return Task.CompletedTask;
    }

    public Task<Guid> HoldAsync(Guid iikoCustomerId, Guid walletId, decimal sum, string? comment, CancellationToken ct = default)
    {
        HoldCalls.Add((iikoCustomerId, walletId, sum, comment));
        var id = Guid.NewGuid();
        _activeHolds[id] = sum;
        return Task.FromResult(id);
    }

    public Task CancelHoldAsync(Guid holdTransactionId, CancellationToken ct = default)
    {
        if (FailCancelHold) throw new IikoApiException("cancel_hold failed (injected)");
        CancelHoldCalls.Add(holdTransactionId);
        _activeHolds.Remove(holdTransactionId);
        return Task.CompletedTask;
    }

    // ── Order push (Phase 3 Part B) ─────────────────────────────────────────

    public Task<List<IikoNomenclatureProduct>> GetNomenclatureAsync(CancellationToken ct = default)
        => Task.FromResult(new List<IikoNomenclatureProduct>());

    public Task<List<IikoPaymentType>> GetPaymentTypesAsync(CancellationToken ct = default)
        => Task.FromResult(new List<IikoPaymentType>());

    public Task<List<IikoTerminalGroup>> GetTerminalGroupsAsync(CancellationToken ct = default)
        => Task.FromResult(new List<IikoTerminalGroup>());

    public Task<Guid> CreateDeliveryOrderAsync(IikoCreateOrderRequest request, CancellationToken ct = default)
    {
        if (FailPush) throw new IikoApiException("deliveries/create failed (injected)");
        PushCalls.Add(request);
        return Task.FromResult(NextIikoOrderId);
    }

    public Task CloseDeliveryOrderAsync(Guid iikoOrderId, CancellationToken ct = default)
    {
        if (FailClose) throw new IikoApiException("deliveries/close failed (injected)");
        CloseCalls.Add(iikoOrderId);
        return Task.CompletedTask;
    }

    public Task RegisterWebhookAsync(string webhookUrl, string authToken, CancellationToken ct = default)
        => Task.CompletedTask;

    // ── Transaction history (issue #13) ─────────────────────────────────────

    public bool FailGetTransactions { get; set; }
    private readonly List<IikoTransaction> _transactions = [];

    /// <summary>Seeds the ledger returned by <see cref="GetCustomerTransactionsAsync"/>, e.g. offsite POS sales.</summary>
    public void SetTransactions(IEnumerable<IikoTransaction> transactions)
    {
        _transactions.Clear();
        _transactions.AddRange(transactions);
    }

    public Task<List<IikoTransaction>> GetCustomerTransactionsAsync(
        Guid iikoCustomerId, DateTime dateFromUtc, DateTime dateToUtc,
        int pageSize = 200, CancellationToken ct = default)
    {
        if (FailGetTransactions) throw new IikoApiException("customer/transactions/by_date failed (injected)");

        return Task.FromResult(_transactions
            .Where(t => t.WhenCreated >= dateFromUtc && t.WhenCreated <= dateToUtc)
            .OrderByDescending(t => t.WhenCreated)
            .Take(pageSize)
            .ToList());
    }
}
