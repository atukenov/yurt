using System.Collections.Concurrent;
using System.Net;
using System.Net.Http.Headers;
using System.Net.Http.Json;
using System.Text.Json;
using System.Text.Json.Serialization;
using Microsoft.Extensions.Logging;
using Yurt.Application.Common.Interfaces;
using Yurt.Application.Features.Fiscal;
using Yurt.Domain.Entities;
using Yurt.Domain.Enums;

namespace Yurt.Infrastructure.Fiscal;

/// <summary>Server-wide ReKassa settings (section "ReKassa"). Each register's password comes from the cafe's secrets.</summary>
public class ReKassaOptions
{
    /// <summary>Live server. Use the test server ReKassa gives with a partner test account.</summary>
    public string BaseUrl { get; set; } = "https://app.rekassa.kz";

    /// <summary>
    /// Yurt's partner key at ReKassa. One key for the whole platform; a cafe's own <c>Fiscal:ApiKey</c> secret wins
    /// when it is set.
    /// </summary>
    public string? ApiKey { get; set; }

    /// <summary>How long a login token is reused before logging in again.</summary>
    public int TokenLifetimeMinutes { get; set; } = 12 * 60;
}

/// <summary>
/// ReKassa cloud cash register adapter. Unlike Webkassa there is no cafe-wide login: every register logs in by itself
/// with its serial number (the branch's register number in Yurt) and the "password for third-party apps" made in the
/// ReKassa cabinet (the cafe's <c>Fiscal:Password</c> secret). Closing the shift also needs the register PIN
/// (<c>Fiscal:Pin</c>). ReKassa opens the fiscal shift by itself on the first ticket.
///
/// NOT VERIFIED against ReKassa yet: the paths, field names and codes below follow the shape of the Kazakh fiscal
/// data protocol that ReKassa is built on, as known from other POS systems. The partner API documentation and a test
/// account are given on request; check every item marked "unverified" against them before a cafe goes live.
/// </summary>
public class ReKassaFiscalRegistrar : IFiscalRegistrar
{
    // Unverified: operation, payment, item and tax names of the fiscal data protocol.
    public const string OperationSell = "OPERATION_SELL";
    public const string OperationSellReturn = "OPERATION_SELL_RETURN";
    public const string PaymentCash = "PAYMENT_CASH";
    public const string PaymentCard = "PAYMENT_CARD";
    public const string PaymentMobile = "PAYMENT_MOBILE";
    public const int TaxVat = 100;
    // Unverified: unit code 796 = "piece" in the Kazakh unit list.
    public const string UnitPiece = "796";

    private static readonly JsonSerializerOptions Json = new()
    {
        PropertyNamingPolicy = JsonNamingPolicy.CamelCase,
        DefaultIgnoreCondition = JsonIgnoreCondition.WhenWritingNull,
    };

    private readonly HttpClient _http;
    private readonly ReKassaOptions _options;
    private readonly ReKassaSessionCache _sessions;
    private readonly ILogger<ReKassaFiscalRegistrar> _logger;

    public ReKassaFiscalRegistrar(HttpClient http, ReKassaOptions options, ReKassaSessionCache sessions, ILogger<ReKassaFiscalRegistrar> logger)
    {
        _http = http;
        _options = options;
        _sessions = sessions;
        _logger = logger;
        _http.BaseAddress = new Uri(_options.BaseUrl.TrimEnd('/') + "/");
        _http.Timeout = TimeSpan.FromSeconds(30);
    }

    public string Provider => FiscalProviders.ReKassa;

    public async Task<FiscalResult> SendReceiptAsync(FiscalCredentials credentials, FiscalReceiptRequest receipt, CancellationToken ct = default)
    {
        var answer = await CallAsync(credentials, receipt.RegisterNumber,
            id => $"api/crs/{id}/tickets", BuildTicket(receipt, DateTime.UtcNow), receipt.ExternalId, ct);
        if (answer.Error is not null) return FiscalResult.Fail(answer.Error);
        var data = answer.Data;
        // Unverified: names of the ticket number and the link to the receipt on the fiscal data operator's site.
        return FiscalResult.Ok(
            Text(data, "ticketNumber") ?? Text(data, "fiscalAttribute") ?? Text(data, "id"),
            Text(data, "ticketUrl") ?? Text(data, "qrCode"));
    }

    public async Task<FiscalResult> CloseShiftAsync(FiscalCredentials credentials, string registerNumber, CancellationToken ct = default)
    {
        if (string.IsNullOrWhiteSpace(credentials.Pin))
            return FiscalResult.Fail("ReKassa needs the register PIN to close the shift (Secrets: Fiscal:Pin).");
        // Unverified: path and the name of the PIN field.
        var answer = await CallAsync(credentials, registerNumber,
            id => $"api/crs/{id}/shifts/close", new Dictionary<string, object?> { ["pincode"] = credentials.Pin }, null, ct);
        return answer.Error is null
            ? FiscalResult.Ok(Text(answer.Data, "shiftNumber"), null)
            : FiscalResult.Fail(answer.Error);
    }

    /// <summary>
    /// The ticket body ReKassa expects. Money is whole tenge ("bills") plus tiyn ("coins"), quantities are in
    /// thousandths. The discount of each line is taken off the line sum.
    /// </summary>
    public static Dictionary<string, object?> BuildTicket(FiscalReceiptRequest r, DateTime nowUtc)
    {
        var vat = r.VatPercent is > 0 ? r.VatPercent.Value : (int?)null;
        var items = r.Lines.Select(l =>
        {
            var lineSum = l.UnitPrice * l.Quantity - l.Discount;
            var commodity = new Dictionary<string, object?>
            {
                ["name"] = l.Name,
                ["sectionCode"] = "0",
                ["quantity"] = l.Quantity * 1000,
                ["price"] = Money(l.UnitPrice),
                ["sum"] = Money(lineSum),
                ["measureUnitCode"] = UnitPiece,
            };
            // Dictionaries keep null values when serialized, so optional parts are only added when there is something.
            if (l.Discount > 0) commodity["discount"] = new Dictionary<string, object?> { ["sum"] = Money(l.Discount) };
            if (vat is not null)
                commodity["taxes"] = new[]
                {
                    new Dictionary<string, object?>
                    {
                        ["type"] = TaxVat,
                        ["percent"] = vat.Value * 1000, // unverified: percent in thousandths
                        ["sum"] = Money(Math.Round(lineSum * vat.Value / (100m + vat.Value), 2, MidpointRounding.AwayFromZero)),
                        ["isInTotalSum"] = true,
                    },
                };
            return new Dictionary<string, object?> { ["type"] = "ITEM_TYPE_COMMODITY", ["commodity"] = commodity };
        }).ToList();

        // Two parts paid with methods of the same ReKassa type are one payment line.
        var payments = r.Payments.GroupBy(p => PaymentType(p.Method))
            .Select(g => new Dictionary<string, object?> { ["type"] = g.Key, ["sum"] = Money(g.Sum(p => p.Amount)) })
            .ToList();
        var total = r.Payments.Sum(p => p.Amount);

        // Unverified: the protocol sends local time; Kazakhstan is UTC+5 all year since 2024.
        var local = nowUtc.AddHours(5);
        return new Dictionary<string, object?>
        {
            ["operation"] = r.Kind == FiscalReceiptKind.Sale ? OperationSell : OperationSellReturn,
            ["dateTime"] = new Dictionary<string, object?>
            {
                ["date"] = new Dictionary<string, object?> { ["year"] = local.Year, ["month"] = local.Month, ["day"] = local.Day },
                ["time"] = new Dictionary<string, object?> { ["hour"] = local.Hour, ["minute"] = local.Minute, ["second"] = local.Second },
            },
            ["domain"] = new Dictionary<string, object?> { ["type"] = "DOMAIN_SERVICES" },
            ["items"] = items,
            ["payments"] = payments,
            ["amounts"] = new Dictionary<string, object?>
            {
                ["total"] = Money(total),
                ["taken"] = Money(total),
                ["change"] = Money(0m),
            },
        };
    }

    public static string PaymentType(PaymentMethod method) => method switch
    {
        PaymentMethod.Cash => PaymentCash,
        PaymentMethod.KaspiBank => PaymentMobile,
        _ => PaymentCard, // card, Halyk, Freedom, Apple Pay, other: paid by card in the end
    };

    /// <summary>Tenge and tiyn as two whole numbers. Negative sums never reach here (a return is its own operation).</summary>
    public static Dictionary<string, object?> Money(decimal amount)
    {
        var rounded = Math.Round(Math.Max(0m, amount), 2, MidpointRounding.AwayFromZero);
        var bills = (long)Math.Truncate(rounded);
        return new Dictionary<string, object?> { ["bills"] = bills, ["coins"] = (int)((rounded - bills) * 100) };
    }

    private sealed record Answer(JsonElement Data, string? Error);

    // Logs the register in when there is no token yet, and once more if ReKassa says the token has expired.
    private async Task<Answer> CallAsync(
        FiscalCredentials creds, string serial, Func<string, string> path, object body, string? requestId, CancellationToken ct)
    {
        for (var attempt = 0; attempt < 2; attempt++)
        {
            ReKassaSession session;
            try { session = await _sessions.GetAsync(serial, attempt > 0, _options.TokenLifetimeMinutes, c => LoginAsync(creds, serial, c), ct); }
            catch (ReKassaException ex) { return new Answer(default, ex.Message); }

            var (status, json) = await SendAsync(HttpMethod.Post, path(session.CashRegisterId), body, session.Token, creds, requestId, ct);
            if (status is null) return new Answer(default, "ReKassa did not answer.");
            if (status is HttpStatusCode.Unauthorized && attempt == 0)
            {
                _logger.LogInformation("ReKassa token expired; logging in again for register {Serial}", serial);
                continue;
            }
            if ((int)status is < 200 or > 299)
                return new Answer(default, $"ReKassa answered {(int)status}" + (ErrorText(json) is { } e ? $": {e}" : "."));
            return new Answer(json, null);
        }
        return new Answer(default, "ReKassa rejected the login token twice.");
    }

    // Unverified: login by register serial number and password, with the partner key in the query; the answer holds a
    // token. The register's id for later calls is then looked up by its serial number.
    private async Task<ReKassaSession> LoginAsync(FiscalCredentials creds, string serial, CancellationToken ct)
    {
        var apiKey = string.IsNullOrWhiteSpace(creds.ApiKey) ? _options.ApiKey : creds.ApiKey;
        if (string.IsNullOrWhiteSpace(apiKey))
            throw new ReKassaException("ReKassa partner key is not set (server setting ReKassa__ApiKey or Secrets: Fiscal:ApiKey).");

        var (status, json) = await SendAsync(HttpMethod.Post, $"api/auth/login?apiKey={Uri.EscapeDataString(apiKey)}",
            new Dictionary<string, object?> { ["number"] = serial, ["password"] = creds.Password }, null, creds, null, ct);
        if (status is null) throw new ReKassaException("ReKassa did not answer the login.");
        var token = Text(json, "token") ?? Text(json, "accessToken");
        if ((int)status is < 200 or > 299 || string.IsNullOrWhiteSpace(token))
            throw new ReKassaException($"ReKassa login failed for register {serial} ({(int)status})" + (ErrorText(json) is { } e ? $": {e}" : "."));

        var (crStatus, list) = await SendAsync(HttpMethod.Get, "api/crs", null, token, creds, null, ct);
        if (crStatus is null) throw new ReKassaException("ReKassa did not answer the register lookup.");
        var registers = list.ValueKind == JsonValueKind.Array ? list.EnumerateArray().ToList()
            : list.ValueKind == JsonValueKind.Object && list.TryGetProperty("content", out var page) && page.ValueKind == JsonValueKind.Array
                ? page.EnumerateArray().ToList()
                : [];
        var match = registers.FirstOrDefault(r => string.Equals(Text(r, "serialNumber"), serial, StringComparison.OrdinalIgnoreCase));
        if (match.ValueKind == JsonValueKind.Undefined && registers.Count == 1) match = registers[0];
        var id = Text(match, "id");
        if (string.IsNullOrWhiteSpace(id)) throw new ReKassaException($"ReKassa has no register with serial number {serial} for this login.");
        return new ReKassaSession(token, id);
    }

    // Null status = network failure or timeout.
    private async Task<(HttpStatusCode? Status, JsonElement Json)> SendAsync(
        HttpMethod method, string path, object? body, string? token, FiscalCredentials creds, string? requestId, CancellationToken ct)
    {
        try
        {
            using var req = new HttpRequestMessage(method, path);
            if (body is not null) req.Content = JsonContent.Create(body, options: Json);
            if (token is not null) req.Headers.Authorization = new AuthenticationHeaderValue("Bearer", token);
            // Unverified: ReKassa returns the earlier ticket for a repeated request id, so a lost answer is no double receipt.
            if (requestId is not null) req.Headers.TryAddWithoutValidation("X-Request-ID", requestId);
            using var resp = await _http.SendAsync(req, ct);
            var text = await resp.Content.ReadAsStringAsync(ct);
            JsonElement json;
            try { json = string.IsNullOrWhiteSpace(text) ? default : JsonDocument.Parse(text).RootElement.Clone(); }
            catch (JsonException) { json = default; }
            return (resp.StatusCode, json);
        }
        catch (OperationCanceledException) when (ct.IsCancellationRequested) { throw; }
        catch (Exception ex) when (ex is HttpRequestException or TaskCanceledException)
        {
            _logger.LogWarning(ex, "ReKassa call {Path} failed", path.Split('?')[0]);
            return (null, default);
        }
    }

    private static string? ErrorText(JsonElement json)
        => Text(json, "message") ?? Text(json, "error_description") ?? Text(json, "error") ?? Text(json, "code");

    private static string? Text(JsonElement obj, string name)
    {
        if (obj.ValueKind != JsonValueKind.Object || !obj.TryGetProperty(name, out var v)) return null;
        return v.ValueKind switch
        {
            JsonValueKind.String => v.GetString(),
            JsonValueKind.Number => v.GetRawText(),
            _ => null,
        };
    }
}

public class ReKassaException(string message) : Exception(message);

public record ReKassaSession(string Token, string CashRegisterId);

/// <summary>ReKassa login tokens and register ids per register serial number, shared by the whole server (singleton).</summary>
public class ReKassaSessionCache
{
    private readonly ConcurrentDictionary<string, (ReKassaSession Session, DateTime ExpiresAt)> _sessions = new();
    private readonly SemaphoreSlim _lock = new(1, 1);

    public async Task<ReKassaSession> GetAsync(
        string serial, bool refresh, int lifetimeMinutes, Func<CancellationToken, Task<ReKassaSession>> fetch, CancellationToken ct)
    {
        if (!refresh && _sessions.TryGetValue(serial, out var cached) && cached.ExpiresAt > DateTime.UtcNow) return cached.Session;
        await _lock.WaitAsync(ct);
        try
        {
            if (!refresh && _sessions.TryGetValue(serial, out cached) && cached.ExpiresAt > DateTime.UtcNow) return cached.Session;
            var session = await fetch(ct);
            _sessions[serial] = (session, DateTime.UtcNow.AddMinutes(lifetimeMinutes));
            return session;
        }
        finally { _lock.Release(); }
    }
}
