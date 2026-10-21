using System.Collections.Concurrent;
using System.Net;
using System.Net.Http.Json;
using System.Text.Json;
using System.Text.Json.Serialization;
using Microsoft.Extensions.Logging;
using Yurt.Application.Common.Interfaces;
using Yurt.Application.Features.Fiscal;
using Yurt.Domain.Entities;
using Yurt.Domain.Enums;

namespace Yurt.Infrastructure.Fiscal;

/// <summary>Server-wide Webkassa settings (section "Webkassa"). Each cafe's login comes from its own secrets.</summary>
public class WebkassaOptions
{
    /// <summary>Live server. Use https://devkkm.webkassa.kz with a Webkassa test account.</summary>
    public string BaseUrl { get; set; } = "https://kkm.webkassa.kz";

    /// <summary>How long a login token is reused before logging in again.</summary>
    public int TokenLifetimeMinutes { get; set; } = 12 * 60;
}

/// <summary>
/// Webkassa cloud cash register adapter: log in for a token, send a check (sale or return), close the shift (Z report).
/// Webkassa opens the fiscal shift by itself on the first check.
///
/// NOT VERIFIED against Webkassa yet: the paths, field names and codes below follow Webkassa's public partner flow
/// as known from other POS systems. The full API documentation and a test account are given to partners on request;
/// check every item marked "unverified" against them before a cafe goes live.
/// </summary>
public class WebkassaFiscalRegistrar : IFiscalRegistrar
{
    // Unverified: Webkassa operation types (2 = sale, 3 = return of a sale).
    public const int OperationSale = 2;
    public const int OperationSaleReturn = 3;
    // Unverified: payment types (0 = cash, 1 = card, 4 = mobile payment such as Kaspi QR).
    public const int PaymentCash = 0;
    public const int PaymentCard = 1;
    public const int PaymentMobile = 4;
    // Unverified: tax types (0 = no VAT, 100 = VAT).
    public const int TaxNone = 0;
    public const int TaxVat = 100;
    // Unverified: unit code 796 = "piece" in the Kazakh unit list.
    public const int UnitPiece = 796;
    // Unverified: error code Webkassa answers when the token has expired.
    public const int ErrorSessionExpired = 2;

    private static readonly JsonSerializerOptions Json = new()
    {
        PropertyNamingPolicy = null, // Webkassa uses PascalCase names
        DefaultIgnoreCondition = JsonIgnoreCondition.WhenWritingNull,
    };

    private readonly HttpClient _http;
    private readonly WebkassaOptions _options;
    private readonly WebkassaTokenCache _tokens;
    private readonly ILogger<WebkassaFiscalRegistrar> _logger;

    public WebkassaFiscalRegistrar(HttpClient http, WebkassaOptions options, WebkassaTokenCache tokens, ILogger<WebkassaFiscalRegistrar> logger)
    {
        _http = http;
        _options = options;
        _tokens = tokens;
        _logger = logger;
        _http.BaseAddress = new Uri(_options.BaseUrl.TrimEnd('/') + "/");
        _http.Timeout = TimeSpan.FromSeconds(30);
    }

    public string Provider => FiscalProviders.Webkassa;

    public async Task<FiscalResult> SendReceiptAsync(FiscalCredentials credentials, FiscalReceiptRequest receipt, CancellationToken ct = default)
    {
        var answer = await CallAsync(credentials, "api/Check", token => BuildCheck(token, receipt), ct);
        if (answer.Error is not null) return FiscalResult.Fail(answer.Error);
        var data = answer.Data;
        return FiscalResult.Ok(
            Text(data, "CheckNumber"),
            Text(data, "TicketUrl") ?? Text(data, "TicketPrintUrl"));
    }

    public async Task<FiscalResult> CloseShiftAsync(FiscalCredentials credentials, string registerNumber, CancellationToken ct = default)
    {
        var answer = await CallAsync(credentials, "api/ZReport",
            token => new Dictionary<string, object?> { ["Token"] = token, ["CashboxUniqueNumber"] = registerNumber }, ct);
        return answer.Error is null ? FiscalResult.Ok(Text(answer.Data, "ShiftNumber"), null) : FiscalResult.Fail(answer.Error);
    }

    /// <summary>The check body Webkassa expects.</summary>
    public static Dictionary<string, object?> BuildCheck(string token, FiscalReceiptRequest r)
    {
        var positions = r.Lines.Select(l =>
        {
            var lineSum = l.UnitPrice * l.Quantity - l.Discount;
            var tax = r.VatPercent is > 0 ? Math.Round(lineSum * r.VatPercent.Value / (100m + r.VatPercent.Value), 2, MidpointRounding.AwayFromZero) : 0m;
            return new Dictionary<string, object?>
            {
                ["Count"] = l.Quantity,
                ["Price"] = l.UnitPrice,
                ["Discount"] = l.Discount,
                ["Markup"] = 0m,
                ["TaxType"] = r.VatPercent is > 0 ? TaxVat : TaxNone,
                ["TaxPercent"] = r.VatPercent ?? 0,
                ["Tax"] = tax,
                ["PositionName"] = l.Name,
                ["UnitCode"] = UnitPiece,
                ["IsStorno"] = false,
            };
        }).ToList();

        // Two parts paid with methods of the same Webkassa type are one payment line.
        var payments = r.Payments.GroupBy(p => PaymentType(p.Method))
            .Select(g => new Dictionary<string, object?> { ["Sum"] = g.Sum(p => p.Amount), ["PaymentType"] = g.Key })
            .ToList();

        return new Dictionary<string, object?>
        {
            ["Token"] = token,
            ["CashboxUniqueNumber"] = r.RegisterNumber,
            ["OperationType"] = r.Kind == FiscalReceiptKind.Sale ? OperationSale : OperationSaleReturn,
            ["Positions"] = positions,
            ["Payments"] = payments,
            ["Change"] = 0m,
            ["RoundType"] = 2, // unverified: no rounding of the total
            ["ExternalCheckNumber"] = r.ExternalId, // unverified: Webkassa returns the earlier check for a repeated number
        };
    }

    public static int PaymentType(PaymentMethod method) => method switch
    {
        PaymentMethod.Cash => PaymentCash,
        PaymentMethod.KaspiBank => PaymentMobile,
        _ => PaymentCard, // card, Halyk, Freedom, Apple Pay, other: paid by card in the end
    };

    private sealed record Answer(JsonElement Data, string? Error);

    // Logs in when there is no token yet, and once more if Webkassa says the token has expired.
    private async Task<Answer> CallAsync(FiscalCredentials creds, string path, Func<string, object> body, CancellationToken ct)
    {
        for (var attempt = 0; attempt < 2; attempt++)
        {
            string token;
            try { token = await _tokens.GetAsync(creds.Login, attempt > 0, _options.TokenLifetimeMinutes, c => LoginAsync(creds, c), ct); }
            catch (WebkassaException ex) { return new Answer(default, ex.Message); }

            var (status, json) = await PostAsync(creds, path, body(token), ct);
            if (status is null) return new Answer(default, json.ValueKind == JsonValueKind.String ? json.GetString() : "Webkassa did not answer.");
            var errors = Errors(json);
            var expired = status == HttpStatusCode.Unauthorized || errors.Any(e => e.Code == ErrorSessionExpired);
            if (expired && attempt == 0)
            {
                _logger.LogInformation("Webkassa token expired; logging in again for {Path}", path);
                continue;
            }
            if (errors.Count > 0) return new Answer(default, "Webkassa: " + string.Join("; ", errors.Select(e => $"{e.Text} (code {e.Code})")));
            if (status is not HttpStatusCode.OK) return new Answer(default, $"Webkassa answered {(int)status}.");
            return new Answer(json.TryGetProperty("Data", out var data) ? data : default, null);
        }
        return new Answer(default, "Webkassa rejected the login token twice.");
    }

    private async Task<string> LoginAsync(FiscalCredentials creds, CancellationToken ct)
    {
        var (status, json) = await PostAsync(creds, "api/Authorize",
            new Dictionary<string, object?> { ["Login"] = creds.Login, ["Password"] = creds.Password }, ct);
        if (status is null) throw new WebkassaException("Webkassa did not answer the login.");
        var errors = Errors(json);
        if (errors.Count > 0) throw new WebkassaException("Webkassa login failed: " + string.Join("; ", errors.Select(e => e.Text)));
        var token = json.TryGetProperty("Data", out var data) ? Text(data, "Token") : null;
        if (string.IsNullOrWhiteSpace(token)) throw new WebkassaException($"Webkassa login failed ({(int)status}).");
        return token;
    }

    // Null status = network failure or timeout; the JSON then holds the error text.
    private async Task<(HttpStatusCode? Status, JsonElement Json)> PostAsync(FiscalCredentials creds, string path, object body, CancellationToken ct)
    {
        try
        {
            using var req = new HttpRequestMessage(HttpMethod.Post, path) { Content = JsonContent.Create(body, options: Json) };
            // Unverified: partner key header name.
            if (!string.IsNullOrWhiteSpace(creds.ApiKey)) req.Headers.TryAddWithoutValidation("X-API-KEY", creds.ApiKey);
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
            _logger.LogWarning(ex, "Webkassa call {Path} failed", path);
            return (null, JsonSerializer.SerializeToElement("Webkassa did not answer."));
        }
    }

    private static List<(int Code, string Text)> Errors(JsonElement json)
    {
        if (json.ValueKind != JsonValueKind.Object || !json.TryGetProperty("Errors", out var errors) || errors.ValueKind != JsonValueKind.Array)
            return [];
        return errors.EnumerateArray()
            .Select(e => (
                Code: e.TryGetProperty("Code", out var c) && c.TryGetInt32(out var n) ? n : 0,
                Text: Text(e, "Text") ?? "unknown error"))
            .ToList();
    }

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

public class WebkassaException(string message) : Exception(message);

/// <summary>Webkassa login tokens per cafe login, shared by the whole server (singleton).</summary>
public class WebkassaTokenCache
{
    private readonly ConcurrentDictionary<string, (string Token, DateTime ExpiresAt)> _tokens = new();
    private readonly SemaphoreSlim _lock = new(1, 1);

    public async Task<string> GetAsync(string login, bool refresh, int lifetimeMinutes, Func<CancellationToken, Task<string>> fetch, CancellationToken ct)
    {
        if (!refresh && _tokens.TryGetValue(login, out var cached) && cached.ExpiresAt > DateTime.UtcNow) return cached.Token;
        await _lock.WaitAsync(ct);
        try
        {
            if (!refresh && _tokens.TryGetValue(login, out cached) && cached.ExpiresAt > DateTime.UtcNow) return cached.Token;
            var token = await fetch(ct);
            _tokens[login] = (token, DateTime.UtcNow.AddMinutes(lifetimeMinutes));
            return token;
        }
        finally { _lock.Release(); }
    }
}
