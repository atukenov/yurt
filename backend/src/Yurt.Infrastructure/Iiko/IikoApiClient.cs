using System.Net;
using System.Net.Http.Headers;
using System.Net.Http.Json;
using System.Text.Json;
using Microsoft.Extensions.Logging;
using Yurt.Application.Common.Interfaces;
using Yurt.Application.Features.Loyalty;

namespace Yurt.Infrastructure.Iiko;

/// <summary>
/// iikoCloud (iikoTransport) API client.
/// Authorization uses /api/v2/access_token (apiKey + appId + clientSecret) — NOT the legacy v1 apiLogin flow.
/// </summary>
public class IikoApiClient : IIikoApiClient
{
    private readonly HttpClient _http;
    private readonly IikoOptions _options;
    private readonly IikoTokenStore _tokenStore;
    private readonly ILogger<IikoApiClient> _logger;

    private static readonly JsonSerializerOptions Json = new(JsonSerializerDefaults.Web);

    public IikoApiClient(
        HttpClient http, IikoOptions options, IikoTokenStore tokenStore, ILogger<IikoApiClient> logger)
    {
        _http = http;
        _options = options;
        _tokenStore = tokenStore;
        _logger = logger;
        _http.BaseAddress = new Uri(_options.BaseUrl.TrimEnd('/') + "/");
        _http.Timeout = TimeSpan.FromSeconds(20);
    }

    // ── Auth (v2) ────────────────────────────────────────────────────────────

    private record TokenV2Request(string ApiKey, string AppId, string ClientSecret);
    private record TokenV2Response(string? CorrelationId, string? Token);

    private async Task<string> FetchTokenAsync(CancellationToken ct)
    {
        var resp = await _http.PostAsJsonAsync("api/v2/access_token",
            new TokenV2Request(_options.ApiKey, _options.AppId, _options.ClientSecret), Json, ct);

        if (!resp.IsSuccessStatusCode)
        {
            var body = await resp.Content.ReadAsStringAsync(ct);
            throw new IikoApiException(
                $"iiko v2 access_token failed with {(int)resp.StatusCode}: {Truncate(body)}", (int)resp.StatusCode);
        }

        var token = (await resp.Content.ReadFromJsonAsync<TokenV2Response>(Json, ct))?.Token;
        if (string.IsNullOrWhiteSpace(token))
            throw new IikoApiException("iiko v2 access_token returned an empty token.");
        return token;
    }

    private Task<string> GetTokenAsync(bool forceRefresh, CancellationToken ct) =>
        _tokenStore.GetOrRefreshAsync(
            FetchTokenAsync, TimeSpan.FromMinutes(_options.TokenLifetimeMinutes), forceRefresh, ct);

    // ── Core send with one retry on 401 ──────────────────────────────────────

    private async Task<HttpResponseMessage> SendAsync(string path, object body, CancellationToken ct)
    {
        for (var attempt = 0; ; attempt++)
        {
            var token = await GetTokenAsync(forceRefresh: attempt > 0, ct);

            using var req = new HttpRequestMessage(HttpMethod.Post, path)
            {
                Content = JsonContent.Create(body, options: Json)
            };
            req.Headers.Authorization = new AuthenticationHeaderValue("Bearer", token);

            var resp = await _http.SendAsync(req, ct);

            if (resp.StatusCode == HttpStatusCode.Unauthorized && attempt == 0)
            {
                _logger.LogInformation("iiko session token expired; refreshing and retrying {Path}", path);
                _tokenStore.Invalidate();
                resp.Dispose();
                continue;
            }

            return resp;
        }
    }

    private async Task<T> PostAsync<T>(string path, object body, CancellationToken ct)
    {
        var resp = await SendAsync(path, body, ct);
        using (resp)
        {
            if (!resp.IsSuccessStatusCode)
            {
                var errBody = await resp.Content.ReadAsStringAsync(ct);
                throw new IikoApiException(
                    $"iiko {path} failed with {(int)resp.StatusCode}: {Truncate(errBody)}", (int)resp.StatusCode);
            }

            var result = await resp.Content.ReadFromJsonAsync<T>(Json, ct);
            return result ?? throw new IikoApiException($"iiko {path} returned an empty body.");
        }
    }

    // ── Customers / wallet ───────────────────────────────────────────────────

    private record CustomerInfoByPhoneRequest(string Type, string Phone, Guid OrganizationId);
    private record WalletBalanceDto(Guid Id, string? Name, int Type, decimal Balance);
    private record CustomerInfoResponse(Guid Id, string? Phone, List<WalletBalanceDto>? WalletBalances);

    public async Task<IikoCustomerInfo?> GetCustomerByPhoneAsync(string phone, CancellationToken ct = default)
    {
        var resp = await SendAsync("api/1/loyalty/iiko/customer/info",
            new CustomerInfoByPhoneRequest("phone", phone, _options.OrganizationId), ct);
        using (resp)
        {
            // iiko answers 400 for unknown customers — treat as "not registered", not an error
            if (resp.StatusCode == HttpStatusCode.BadRequest || resp.StatusCode == HttpStatusCode.NotFound)
                return null;

            if (!resp.IsSuccessStatusCode)
            {
                var body = await resp.Content.ReadAsStringAsync(ct);
                throw new IikoApiException(
                    $"iiko customer/info failed with {(int)resp.StatusCode}: {Truncate(body)}", (int)resp.StatusCode);
            }

            var info = await resp.Content.ReadFromJsonAsync<CustomerInfoResponse>(Json, ct);
            if (info == null) return null;

            return new IikoCustomerInfo(
                info.Id,
                info.Phone,
                (info.WalletBalances ?? []).Select(w => new IikoWalletBalance(w.Id, w.Name, w.Type, w.Balance)).ToList());
        }
    }

    private record CreateOrUpdateCustomerRequest(string Phone, string? Name, string? SurName, Guid OrganizationId);
    private record CreateOrUpdateCustomerResponse(Guid Id);

    public async Task<Guid> CreateOrUpdateCustomerAsync(
        string phone, string? name, string? surname, CancellationToken ct = default)
    {
        var resp = await PostAsync<CreateOrUpdateCustomerResponse>(
            "api/1/loyalty/iiko/customer/create_or_update",
            new CreateOrUpdateCustomerRequest(phone, name, surname, _options.OrganizationId), ct);
        return resp.Id;
    }

    private record AddToProgramRequest(Guid CustomerId, Guid ProgramId, Guid OrganizationId);
    private record AddToProgramResponse(Guid? UserWalletId, Guid? WalletId);

    public async Task<Guid> AddCustomerToProgramAsync(Guid iikoCustomerId, CancellationToken ct = default)
    {
        var resp = await PostAsync<AddToProgramResponse>(
            "api/1/loyalty/iiko/customer/program/add",
            new AddToProgramRequest(iikoCustomerId, _options.ProgramId, _options.OrganizationId), ct);

        // userWalletId is the customer's own balance-holding wallet (matches the ids
        // returned by customer/info's walletBalances); walletId is only the shared
        // program-level wallet definition and never holds an individual balance.
        return resp.UserWalletId
            ?? resp.WalletId
            ?? throw new IikoApiException("iiko program/add returned no wallet id.");
    }

    private record ChangeBalanceRequest(Guid CustomerId, Guid WalletId, decimal Sum, string? Comment, Guid OrganizationId);

    public async Task TopupAsync(
        Guid iikoCustomerId, Guid walletId, decimal sum, string? comment, CancellationToken ct = default)
    {
        var resp = await SendAsync("api/1/loyalty/iiko/customer/wallet/topup",
            new ChangeBalanceRequest(iikoCustomerId, walletId, sum, comment, _options.OrganizationId), ct);
        await EnsureSuccessAsync(resp, "wallet/topup", ct);
    }

    public async Task ChargeoffAsync(
        Guid iikoCustomerId, Guid walletId, decimal sum, string? comment, CancellationToken ct = default)
    {
        var resp = await SendAsync("api/1/loyalty/iiko/customer/wallet/chargeoff",
            new ChangeBalanceRequest(iikoCustomerId, walletId, sum, comment, _options.OrganizationId), ct);
        await EnsureSuccessAsync(resp, "wallet/chargeoff", ct);
    }

    private record HoldRequest(Guid CustomerId, Guid WalletId, decimal Sum, string? Comment, Guid OrganizationId);
    private record HoldResponse(Guid TransactionId);

    public async Task<Guid> HoldAsync(
        Guid iikoCustomerId, Guid walletId, decimal sum, string? comment, CancellationToken ct = default)
    {
        var resp = await PostAsync<HoldResponse>("api/1/loyalty/iiko/customer/wallet/hold",
            new HoldRequest(iikoCustomerId, walletId, sum, comment, _options.OrganizationId), ct);
        return resp.TransactionId;
    }

    private record CancelHoldRequest(Guid TransactionId, Guid OrganizationId);

    public async Task CancelHoldAsync(Guid holdTransactionId, CancellationToken ct = default)
    {
        var resp = await SendAsync("api/1/loyalty/iiko/customer/wallet/cancel_hold",
            new CancelHoldRequest(holdTransactionId, _options.OrganizationId), ct);
        await EnsureSuccessAsync(resp, "wallet/cancel_hold", ct);
    }

    // ── Order push (reporting/kitchen-routing side-channel) ─────────────────────

    private record NomenclatureRequest(Guid OrganizationId);
    private record NomenclatureSizePriceDto(Guid? SizeId, NomenclaturePriceDto Price);
    private record NomenclaturePriceDto(decimal CurrentPrice);
    private record NomenclatureProductDto(Guid Id, string? Name, string? Type, bool IsDeleted, List<NomenclatureSizePriceDto>? SizePrices);
    private record NomenclatureSizeDto(Guid Id, string? Name);
    private record NomenclatureResponse(List<NomenclatureProductDto>? Products, List<NomenclatureSizeDto>? Sizes);

    public async Task<List<IikoNomenclatureProduct>> GetNomenclatureAsync(CancellationToken ct = default)
    {
        // Deprecated but still the endpoint deliveries/create's productId docs point to;
        // startRevision omitted (null) always fetches the full current catalog.
        var resp = await PostAsync<NomenclatureResponse>(
            "api/1/nomenclature", new NomenclatureRequest(_options.OrganizationId), ct);

        var sizeNames = (resp.Sizes ?? []).ToDictionary(s => s.Id, s => s.Name);

        return (resp.Products ?? [])
            .Where(p => !p.IsDeleted && p.Type is "dish" or "good")
            .Select(p =>
            {
                var sizes = (p.SizePrices ?? [])
                    .Where(sp => sp.SizeId.HasValue)
                    .Select(sp => new IikoNomenclatureSize(
                        sp.SizeId!.Value, sizeNames.GetValueOrDefault(sp.SizeId.Value)))
                    .ToList();
                var price = p.SizePrices?.FirstOrDefault()?.Price.CurrentPrice ?? 0m;
                return new IikoNomenclatureProduct(p.Id, p.Name ?? "", price, sizes);
            })
            .ToList();
    }

    private record PaymentTypesRequest(List<Guid> OrganizationIds);
    private record PaymentTypeDto(Guid? Id, string? Name);
    private record PaymentTypesResponse(List<PaymentTypeDto>? PaymentTypes);

    public async Task<List<IikoPaymentType>> GetPaymentTypesAsync(CancellationToken ct = default)
    {
        var resp = await PostAsync<PaymentTypesResponse>(
            "api/1/payment_types", new PaymentTypesRequest([_options.OrganizationId]), ct);

        return (resp.PaymentTypes ?? [])
            .Where(p => p.Id.HasValue)
            .Select(p => new IikoPaymentType(p.Id!.Value, p.Name ?? ""))
            .ToList();
    }

    private record TerminalGroupsRequest(List<Guid> OrganizationIds);
    private record TerminalGroupDto(Guid Id, string Name);
    private record TerminalGroupsWrapperDto(Guid OrganizationId, List<TerminalGroupDto>? Items);
    private record TerminalGroupsResponse(List<TerminalGroupsWrapperDto>? TerminalGroups);

    public async Task<List<IikoTerminalGroup>> GetTerminalGroupsAsync(CancellationToken ct = default)
    {
        var resp = await PostAsync<TerminalGroupsResponse>(
            "api/1/terminal_groups", new TerminalGroupsRequest([_options.OrganizationId]), ct);

        return (resp.TerminalGroups ?? [])
            .SelectMany(w => w.Items ?? [])
            .Select(g => new IikoTerminalGroup(g.Id, g.Name))
            .ToList();
    }

    private record CreateOrderCustomer(string Type, string Name);
    private record CreateOrderItem(string Type, decimal Amount, Guid ProductId, Guid? ProductSizeId, decimal Price, string? Comment);
    private record CreateOrderPayment(string PaymentTypeKind, decimal Sum, Guid PaymentTypeId, bool IsProcessedExternally);
    private record CreateDeliveryOrder(
        string Phone, string OrderServiceType, CreateOrderCustomer Customer,
        List<CreateOrderItem> Items, List<CreateOrderPayment> Payments, string? Comment);
    private record CreateDeliveryOrderRequest(Guid OrganizationId, Guid TerminalGroupId, CreateDeliveryOrder Order);
    private record CreationErrorDto(string? Message);
    private record CreateOrderInfoDto(Guid Id, string CreationStatus, CreationErrorDto? ErrorInfo);
    private record CreateDeliveryOrderResponse(Guid CorrelationId, CreateOrderInfoDto OrderInfo);
    private record CommandStatusRequest(Guid OrganizationId, Guid CorrelationId);
    private record CommandStatusResponse(string State);

    public async Task<Guid> CreateDeliveryOrderAsync(IikoCreateOrderRequest request, CancellationToken ct = default)
    {
        var order = new CreateDeliveryOrder(
            Phone: request.CustomerPhone,
            OrderServiceType: "DeliveryByClient",
            // One-time customer: kept out of iiko's own loyalty processing entirely — the
            // wallet earn/spend flow in this client already owns that, and double-binding
            // the order to the loyalty customer would double-credit bonus on close.
            Customer: new CreateOrderCustomer("one-time", request.CustomerName ?? request.CustomerPhone),
            Items: request.Items.Select(i => new CreateOrderItem(
                "Product", i.Amount, i.ProductId, i.ProductSizeId, i.Price, i.Comment)).ToList(),
            Payments: [new CreateOrderPayment("External", request.PaymentSum, _options.PaymentTypeId, true)],
            Comment: request.Comment);

        var resp = await PostAsync<CreateDeliveryOrderResponse>(
            "api/1/deliveries/create",
            new CreateDeliveryOrderRequest(_options.OrganizationId, request.TerminalGroupId, order), ct);

        var info = resp.OrderInfo;
        if (info.CreationStatus == "InProgress")
            info = await PollCommandStatusAsync(resp.CorrelationId, info, ct);

        if (info.CreationStatus != "Success")
            throw new IikoApiException(
                $"iiko deliveries/create did not succeed: {info.CreationStatus} — {info.ErrorInfo?.Message}");

        return info.Id;
    }

    private async Task<CreateOrderInfoDto> PollCommandStatusAsync(
        Guid correlationId, CreateOrderInfoDto lastKnown, CancellationToken ct)
    {
        for (var attempt = 0; attempt < 10; attempt++)
        {
            await Task.Delay(TimeSpan.FromSeconds(1), ct);
            var status = await PostAsync<CommandStatusResponse>(
                "api/1/commands/status",
                new CommandStatusRequest(_options.OrganizationId, correlationId), ct);
            if (status.State != "InProgress")
                return lastKnown with { CreationStatus = status.State };
        }
        return lastKnown; // still InProgress after the bounded wait — caller treats as failure/retry
    }

    private record CloseOrderRequest(Guid OrganizationId, Guid OrderId);

    public async Task CloseDeliveryOrderAsync(Guid iikoOrderId, CancellationToken ct = default)
    {
        var resp = await SendAsync("api/1/deliveries/close",
            new CloseOrderRequest(_options.OrganizationId, iikoOrderId), ct);
        await EnsureSuccessAsync(resp, "deliveries/close", ct);
    }

    // ── Transaction history (issue #13) ─────────────────────────────────────

    private record TransactionsByPeriodRequest(
        Guid CustomerId, string DateFrom, string DateTo, int PageNumber, int PageSize, Guid OrganizationId);
    private record TransactionReportItemDto(
        Guid Id, string WhenCreated, decimal Sum, decimal? OrderSum, int? OrderNumber, Guid? PosOrderId,
        string? TypeName, bool? IsDelivery, decimal? BalanceBefore, decimal? BalanceAfter, string? Comment);
    private record TransactionsByPeriodResponse(List<TransactionReportItemDto>? Transactions);

    public async Task<List<IikoTransaction>> GetCustomerTransactionsAsync(
        Guid iikoCustomerId, DateTime dateFromUtc, DateTime dateToUtc,
        int pageSize = 200, CancellationToken ct = default)
    {
        var resp = await PostAsync<TransactionsByPeriodResponse>(
            "api/1/loyalty/iiko/customer/transactions/by_date",
            new TransactionsByPeriodRequest(
                iikoCustomerId, FormatIikoDate(dateFromUtc), FormatIikoDate(dateToUtc),
                PageNumber: 0, pageSize, _options.OrganizationId), ct);

        return (resp.Transactions ?? [])
            .Select(t => new IikoTransaction(
                t.Id, ParseIikoDate(t.WhenCreated), t.Sum, t.OrderSum, t.OrderNumber, t.PosOrderId,
                t.TypeName, t.IsDelivery, t.BalanceBefore, t.BalanceAfter, t.Comment))
            .OrderByDescending(t => t.WhenCreated)
            .ToList();
    }

    private static string FormatIikoDate(DateTime dt) => dt.ToString("yyyy-MM-dd HH:mm:ss.fff");

    private static DateTime ParseIikoDate(string s) => DateTime.Parse(
        s, System.Globalization.CultureInfo.InvariantCulture,
        System.Globalization.DateTimeStyles.AssumeUniversal | System.Globalization.DateTimeStyles.AdjustToUniversal);

    private record WebhookFilter(List<string> WebHooksEventType);
    private record RegisterWebhookRequest(Guid OrganizationId, string WebHooksUri, string AuthToken, WebhookFilter WebHooksFilter);

    public async Task RegisterWebhookAsync(string webhookUrl, string authToken, CancellationToken ct = default)
    {
        var resp = await SendAsync("api/1/webhooks/update_settings",
            new RegisterWebhookRequest(
                _options.OrganizationId, webhookUrl, authToken,
                new WebhookFilter(["DeliveryOrderUpdate", "DeliveryOrderError"])), ct);
        await EnsureSuccessAsync(resp, "webhooks/update_settings", ct);
    }

    // ── Helpers ──────────────────────────────────────────────────────────────

    private static async Task EnsureSuccessAsync(HttpResponseMessage resp, string op, CancellationToken ct)
    {
        using (resp)
        {
            if (resp.IsSuccessStatusCode) return;
            var body = await resp.Content.ReadAsStringAsync(ct);
            throw new IikoApiException(
                $"iiko {op} failed with {(int)resp.StatusCode}: {Truncate(body)}", (int)resp.StatusCode);
        }
    }

    private static string Truncate(string s) => s.Length <= 500 ? s : s[..500];
}
