using System.Globalization;
using System.Text.RegularExpressions;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;
using Yurt.Application.Common.Interfaces;
using Yurt.Application.Common.Models;
using Yurt.Application.Features.Push.Services;
using Yurt.Domain.Entities;

namespace Yurt.Application.Features.Stock;

/// <summary>
/// Phone alerts when stock runs low (Phase 6, <c>own_pos</c>). The cafe keeps a short list of phone numbers; when an
/// accepted order takes a dish or ingredient down to its warn-at number (or to zero), every cafe-app account with one
/// of those numbers gets a push on its phone. Uses the cafe's own Firebase setup, so nothing is sent while Firebase or
/// the <c>push</c> module is off.
/// </summary>
public class StockAlertPushService(
    IApplicationDbContext db, PushNotificationService push, IAuditLogService audit, ILogger<StockAlertPushService> logger)
{
    public const int MaxPhones = 5;
    private const int MaxNamesInPush = 3;
    private static readonly Regex PhoneFormat = new(@"^\+?[1-9]\d{6,14}$", RegexOptions.Compiled);

    public async Task<List<StockAlertPhoneDto>> GetPhonesAsync(CancellationToken ct = default)
    {
        var phones = await StoredPhonesAsync(ct);
        var withApp = await CustomersWithAppAsync(phones, ct);
        return phones.Select(p => new StockAlertPhoneDto(p, withApp.Any(c => Matches(c.MobileNumber, p)))).ToList();
    }

    /// <summary>Replaces the list. An empty list switches the alerts off.</summary>
    public async Task<Result<List<StockAlertPhoneDto>>> SetPhonesAsync(SaveStockAlertPhonesDto dto, CancellationToken ct = default)
    {
        var phones = (dto.Phones ?? []).Select(Normalize).Where(p => p.Length > 0).Distinct().ToList();
        if (phones.Count > MaxPhones)
            return Result<List<StockAlertPhoneDto>>.Failure($"Up to {MaxPhones} phone numbers.", 400);
        var bad = phones.FirstOrDefault(p => !PhoneFormat.IsMatch(p));
        if (bad != null) return Result<List<StockAlertPhoneDto>>.Failure($"'{bad}' is not a phone number.", 400);

        var value = string.Join(",", phones);
        var row = await db.PlatformSettings.FirstOrDefaultAsync(s => s.Key == PlatformSettingKeys.StockAlertPhones, ct);
        if (row is null)
            db.PlatformSettings.Add(new PlatformSetting { Key = PlatformSettingKeys.StockAlertPhones, Value = value });
        else
        {
            row.Value = value;
            row.UpdatedAt = DateTime.UtcNow;
        }
        await db.SaveChangesAsync(ct);
        await audit.LogAsync("StockAlertPhonesSet", "PlatformSetting", PlatformSettingKeys.StockAlertPhones,
            $"{phones.Count} phone(s)", ct);
        return Result<List<StockAlertPhoneDto>>.Success(await GetPhonesAsync(ct));
    }

    /// <summary>Sends one push about the given items (all from one branch). Never throws: it runs right after an
    /// order is accepted, and an alert problem must never fail that request.</summary>
    public async Task NotifyAsync(IReadOnlyList<StockAlertDto> items, CancellationToken ct = default)
    {
        if (items.Count == 0) return;
        try
        {
            var phones = await StoredPhonesAsync(ct);
            if (phones.Count == 0) return;
            var customers = await CustomersWithAppAsync(phones, ct);
            if (customers.Count == 0)
            {
                logger.LogInformation("Stock alert for {Count} item(s) not sent: no alert phone has the app.", items.Count);
                return;
            }

            var (title, body) = Copy(items);
            await push.SendToCustomersAsync(customers.Select(c => c.Id).ToList(), title, body,
                new Dictionary<string, string> { ["type"] = "stock-alert", ["locationId"] = items[0].LocationId.ToString() }, ct);
        }
        catch (Exception ex)
        {
            logger.LogError(ex, "Failed to send a stock alert for {Count} item(s).", items.Count);
        }
    }

    /// <summary>Short Russian copy, like the order pushes (the backend keeps no language per person).</summary>
    public static (string Title, string Body) Copy(IReadOnlyList<StockAlertDto> items)
    {
        var title = items.Any(i => i.IsOut) ? "Закончилось на складе" : "Мало на складе";
        var parts = items.OrderByDescending(i => i.IsOut).ThenBy(i => i.Name).Take(MaxNamesInPush)
            .Select(i => i.IsOut
                ? $"{i.Name}: закончилось"
                : $"{i.Name}: осталось {i.Quantity.ToString("0.##", CultureInfo.InvariantCulture)}{(string.IsNullOrEmpty(i.Unit) ? "" : " " + i.Unit)}");
        var more = items.Count > MaxNamesInPush ? $" и ещё {items.Count - MaxNamesInPush}" : "";
        return (title, $"{string.Join("; ", parts)}{more} ({items[0].LocationName})");
    }

    private async Task<List<string>> StoredPhonesAsync(CancellationToken ct)
    {
        var raw = await db.PlatformSettings.AsNoTracking()
            .Where(s => s.Key == PlatformSettingKeys.StockAlertPhones)
            .Select(s => s.Value)
            .FirstOrDefaultAsync(ct);
        return string.IsNullOrWhiteSpace(raw) ? [] : raw.Split(',', StringSplitOptions.RemoveEmptyEntries).ToList();
    }

    private async Task<List<(Guid Id, string MobileNumber)>> CustomersWithAppAsync(List<string> phones, CancellationToken ct)
    {
        if (phones.Count == 0) return [];
        var candidates = phones.SelectMany(Spellings).Distinct().ToList();
        var rows = await db.CustomerUsers
            .Where(c => c.IsActive && candidates.Contains(c.MobileNumber) && c.DeviceTokens.Any())
            .Select(c => new { c.Id, c.MobileNumber })
            .ToListAsync(ct);
        return rows.Select(r => (r.Id, r.MobileNumber)).ToList();
    }

    private static string Normalize(string? phone) => (phone ?? "").Trim().Replace(" ", "").Replace("-", "");

    private static bool Matches(string mobile, string phone) => Spellings(phone).Contains(mobile);

    /// <summary>The ways a Kazakh number may have been typed at sign-up: +7…, 7… or 8….</summary>
    private static IEnumerable<string> Spellings(string phone)
    {
        var digits = phone.TrimStart('+');
        yield return phone;
        yield return digits;
        yield return "+" + digits;
        if (digits.Length == 11 && digits[0] is '7' or '8')
        {
            var rest = digits[1..];
            yield return "+7" + rest;
            yield return "7" + rest;
            yield return "8" + rest;
        }
    }
}
