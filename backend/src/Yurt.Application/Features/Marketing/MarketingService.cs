using System.Globalization;
using System.Security.Cryptography;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;
using Yurt.Application.Common.Interfaces;
using Yurt.Application.Common.Models;
using Yurt.Application.Features.Push.Services;
using Yurt.Domain;
using Yurt.Domain.Entities;
using Yurt.Domain.Enums;

namespace Yurt.Application.Features.Marketing;

/// <summary>
/// Guest groups and guest messages (marketing module). Automatic messages (come back, birthday, thanks after the first
/// order) are sent by a background job; one-off messages are sent once by an admin. Each guest can get a personal
/// one-time promo code with the message. Rules that keep it polite: no messages at night, at most one message per
/// guest every few days (a birthday message is the exception), never to a guest who switched offers off.
/// </summary>
public class MarketingService(
    IApplicationDbContext db,
    PushNotificationService push,
    IModuleGate modules,
    IAuditLogService audit,
    ILogger<MarketingService> logger)
{
    public static readonly TimeZoneInfo CafeTime = TimeZoneInfo.FindSystemTimeZoneById("Asia/Almaty");

    /// <summary>A guest gets at most one message in this many days (birthday messages excepted).</summary>
    public const int FrequencyCapDays = 3;

    /// <summary>Automatic messages go out only from this local hour up to (not including) the end hour.</summary>
    public const int AutoStartHour = 10, AutoEndHour = 21;

    /// <summary>A one-off message can be sent by hand only from this local hour up to the end hour.</summary>
    public const int ManualStartHour = 9, ManualEndHour = 22;

    /// <summary>Most guests one automatic message sends to in one run (the job runs often, the rest follow).</summary>
    public const int MaxPerRun = 300;

    private const string CodeAlphabet = "ABCDEFGHJKMNPQRSTUVWXYZ23456789";

    // ── Guest groups ─────────────────────────────────────────────────────────

    public async Task<MarketingOverviewDto> GetOverviewAsync(DateTime nowUtc, CancellationToken ct = default)
    {
        var guests = await LoadGuestsAsync(nowUtc, ct);
        var today = LocalDate(nowUtc);
        var segments = GuestSegments.Keys.Select(key =>
        {
            var members = GuestSegments.Filter(key, guests, nowUtc, today).ToList();
            return new GuestSegmentDto(key, members.Count, members.Count(Reachable));
        }).ToList();
        return new MarketingOverviewDto(await push.IsAvailableAsync(ct), segments);
    }

    /// <summary>The first guests of a group (most money spent first), for a quick look.</summary>
    public async Task<Result<List<SegmentGuestDto>>> GetSegmentGuestsAsync(string key, DateTime nowUtc, CancellationToken ct = default)
    {
        if (!GuestSegments.IsKnown(key)) return Result<List<SegmentGuestDto>>.NotFound("Unknown guest group.");
        var guests = await LoadGuestsAsync(nowUtc, ct);
        var members = GuestSegments.Filter(key, guests, nowUtc, LocalDate(nowUtc))
            .OrderByDescending(g => g.TotalSpent).Take(100).ToList();
        var ids = members.Select(m => m.Id).ToList();
        var phones = await db.CustomerUsers.Where(c => ids.Contains(c.Id))
            .Select(c => new { c.Id, c.MobileNumber, c.LastName }).ToDictionaryAsync(c => c.Id, ct);
        return Result<List<SegmentGuestDto>>.Success(members.Select(m => new SegmentGuestDto(
            m.Id, $"{m.FirstName} {phones[m.Id].LastName}".Trim(), phones[m.Id].MobileNumber,
            m.CompletedOrders, m.LastOrderAt, m.TotalSpent, Reachable(m))).ToList());
    }

    private static bool Reachable(GuestStats g) => g.HasPhone && !g.OptedOut;

    /// <summary>Every active guest of the cafe with numbers from their completed orders.</summary>
    public async Task<List<GuestStats>> LoadGuestsAsync(DateTime nowUtc, CancellationToken ct = default)
    {
        var since30 = nowUtc.AddDays(-30);
        return await db.CustomerUsers
            .Where(c => c.IsActive)
            .Select(c => new GuestStats(
                c.Id,
                c.FirstName,
                c.CreatedAt,
                c.DateOfBirth,
                c.MarketingOptOut,
                c.DeviceTokens.Any(),
                c.Orders.Count(o => o.Status == OrderStatus.Completed),
                c.Orders.Count(o => o.Status == OrderStatus.Completed && o.CreatedAt >= since30),
                c.Orders.Where(o => o.Status == OrderStatus.Completed).Min(o => (DateTime?)o.CreatedAt),
                c.Orders.Where(o => o.Status == OrderStatus.Completed).Max(o => (DateTime?)o.CreatedAt),
                c.Orders.Where(o => o.Status == OrderStatus.Completed).Sum(o => (decimal?)o.Total) ?? 0))
            .ToListAsync(ct);
    }

    // ── Messages: list and edit ──────────────────────────────────────────────

    public async Task<List<CampaignDto>> GetCampaignsAsync(CancellationToken ct = default)
    {
        var campaigns = await db.MarketingCampaigns.OrderBy(c => c.Kind).ThenByDescending(c => c.CreatedAt).ToListAsync(ct);
        var results = await GetResultsAsync(ct);
        return campaigns.Select(c => ToDto(c, results.GetValueOrDefault(c.Id, new CampaignResultsDto(0, 0, 0)))).ToList();
    }

    private async Task<Dictionary<Guid, CampaignResultsDto>> GetResultsAsync(CancellationToken ct)
    {
        var sent = await db.MarketingMessages.GroupBy(m => m.CampaignId)
            .Select(g => new { g.Key, Count = g.Count() }).ToDictionaryAsync(x => x.Key, x => x.Count, ct);
        var used = await db.Orders
            .Where(o => o.DiscountCodeId != null && o.Status != OrderStatus.Declined
                && o.DiscountCode!.CampaignId != null)
            .GroupBy(o => o.DiscountCode!.CampaignId!.Value)
            .Select(g => new { g.Key, Count = g.Count(), Revenue = g.Sum(o => o.Total) })
            .ToDictionaryAsync(x => x.Key, ct);
        return sent.Keys.Union(used.Keys).ToDictionary(id => id, id => new CampaignResultsDto(
            sent.GetValueOrDefault(id),
            used.TryGetValue(id, out var u) ? u.Count : 0,
            used.TryGetValue(id, out var r) ? r.Revenue : 0));
    }

    public async Task<Result<CampaignDto>> CreateAsync(SaveCampaignDto dto, CancellationToken ct = default)
    {
        var campaign = new MarketingCampaign();
        var error = Apply(campaign, dto, isNew: true);
        if (error is not null) return Result<CampaignDto>.Failure(error);
        if (campaign.Kind != CampaignKind.OneOff
            && await db.MarketingCampaigns.AnyAsync(c => c.Kind == campaign.Kind, ct))
            return Result<CampaignDto>.Failure("This automatic message already exists. Edit it instead.", 409);

        db.MarketingCampaigns.Add(campaign);
        await db.SaveChangesAsync(ct);
        await audit.LogAsync("CampaignCreated", "MarketingCampaign", campaign.Id.ToString(), campaign.Name, ct);
        return Result<CampaignDto>.Success(ToDto(campaign, new CampaignResultsDto(0, 0, 0)), 201);
    }

    public async Task<Result<CampaignDto>> UpdateAsync(Guid id, SaveCampaignDto dto, CancellationToken ct = default)
    {
        var campaign = await db.MarketingCampaigns.FirstOrDefaultAsync(c => c.Id == id, ct);
        if (campaign is null) return Result<CampaignDto>.NotFound();
        if (campaign.SentAt is not null) return Result<CampaignDto>.Failure("This message was already sent and can't be changed.", 409);
        var error = Apply(campaign, dto, isNew: false);
        if (error is not null) return Result<CampaignDto>.Failure(error);
        await db.SaveChangesAsync(ct);
        await audit.LogAsync("CampaignUpdated", "MarketingCampaign", id.ToString(), campaign.Name, ct);
        var results = await GetResultsAsync(ct);
        return Result<CampaignDto>.Success(ToDto(campaign, results.GetValueOrDefault(id, new CampaignResultsDto(0, 0, 0))));
    }

    public async Task<Result<bool>> DeleteAsync(Guid id, CancellationToken ct = default)
    {
        var campaign = await db.MarketingCampaigns.FirstOrDefaultAsync(c => c.Id == id, ct);
        if (campaign is null) return Result<bool>.NotFound();
        db.MarketingCampaigns.Remove(campaign); // personal codes already given keep working until they expire
        await db.SaveChangesAsync(ct);
        await audit.LogAsync("CampaignDeleted", "MarketingCampaign", id.ToString(), campaign.Name, ct);
        return Result<bool>.Success(true);
    }

    /// <summary>Copies the form into the message; returns an error text when something is wrong.</summary>
    public static string? Apply(MarketingCampaign c, SaveCampaignDto dto, bool isNew)
    {
        if (!Enum.TryParse<CampaignKind>(dto.Kind, true, out var kind) || !Enum.IsDefined(kind))
            return "Unknown message kind.";
        if (!isNew && kind != c.Kind) return "The kind of a message can't be changed.";
        if (string.IsNullOrWhiteSpace(dto.Name)) return "Give the message a name.";

        string? Clean(string? s, int max) => string.IsNullOrWhiteSpace(s) ? null : s.Trim()[..Math.Min(s.Trim().Length, max)];
        var titles = new[] { Clean(dto.Title, 120), Clean(dto.TitleRu, 120), Clean(dto.TitleKk, 120) };
        var bodies = new[] { Clean(dto.Body, 500), Clean(dto.BodyRu, 500), Clean(dto.BodyKk, 500) };
        if (titles.All(t => t is null) || bodies.All(b => b is null))
            return "Write the message title and text in at least one language.";

        var (minDays, maxDays) = kind switch
        {
            CampaignKind.ComeBack => (7, 365),
            CampaignKind.Birthday => (0, 14),
            CampaignKind.FirstOrderThanks => (0, 30),
            _ => (0, 0),
        };
        if (kind != CampaignKind.OneOff && (dto.Days < minDays || dto.Days > maxDays))
            return $"Days must be from {minDays} to {maxDays}.";
        if (kind == CampaignKind.OneOff && !GuestSegments.IsKnown(dto.Segment))
            return "Choose a guest group.";

        DiscountType? type = null;
        if (!string.IsNullOrWhiteSpace(dto.DiscountType))
        {
            if (!Enum.TryParse<DiscountType>(dto.DiscountType, true, out var t) || !Enum.IsDefined(t))
                return "Invalid discount type. Use 'Percentage' or 'FixedAmount'.";
            if (dto.DiscountValue is not > 0) return "Discount value must be greater than zero.";
            if (t == DiscountType.Percentage && dto.DiscountValue > 100) return "Percentage discount cannot exceed 100.";
            type = t;
        }
        var validDays = dto.CodeValidDays ?? 7;
        if (type is not null && (validDays < 1 || validDays > 90)) return "A personal code must be valid for 1 to 90 days.";
        if (dto.MinOrderAmount is < 0) return "Minimum order can't be negative.";

        c.Kind = kind;
        c.Name = dto.Name.Trim()[..Math.Min(dto.Name.Trim().Length, 200)];
        c.IsActive = dto.IsActive;
        c.Segment = kind == CampaignKind.OneOff ? dto.Segment : null;
        c.Days = kind == CampaignKind.OneOff ? 0 : dto.Days;
        c.Title = titles[0] ?? "";
        c.TitleRu = titles[1];
        c.TitleKk = titles[2];
        c.Body = bodies[0] ?? "";
        c.BodyRu = bodies[1];
        c.BodyKk = bodies[2];
        c.DiscountType = type;
        c.DiscountValue = type is null ? null : dto.DiscountValue;
        c.MinOrderAmount = type is null ? null : dto.MinOrderAmount;
        c.CodeValidDays = validDays;
        return null;
    }

    private static CampaignDto ToDto(MarketingCampaign c, CampaignResultsDto results) => new(
        c.Id, c.Kind.ToString(), c.Name, c.IsActive, c.Segment, c.Days,
        c.Title, c.TitleRu, c.TitleKk, c.Body, c.BodyRu, c.BodyKk,
        c.DiscountType?.ToString(), c.DiscountValue, c.MinOrderAmount, c.CodeValidDays,
        c.SentAt, c.CreatedAt, results);

    // ── Sending ──────────────────────────────────────────────────────────────

    /// <summary>Sends a one-off message to its guest group now (once).</summary>
    public async Task<Result<SendCampaignResultDto>> SendNowAsync(Guid id, DateTime nowUtc, CancellationToken ct = default)
    {
        var campaign = await db.MarketingCampaigns.FirstOrDefaultAsync(c => c.Id == id, ct);
        if (campaign is null) return Result<SendCampaignResultDto>.NotFound();
        if (campaign.Kind != CampaignKind.OneOff)
            return Result<SendCampaignResultDto>.Failure("Automatic messages are sent by themselves.", 400);
        if (campaign.SentAt is not null) return Result<SendCampaignResultDto>.Failure("This message was already sent.", 409);
        var hour = LocalTime(nowUtc).Hour;
        if (hour < ManualStartHour || hour >= ManualEndHour)
            return Result<SendCampaignResultDto>.Failure(
                $"Messages are not sent at night ({ManualEndHour}:00–{ManualStartHour:00}:00). Try again in the morning.", 422);
        if (!await push.IsAvailableAsync(ct))
            return Result<SendCampaignResultDto>.Failure("Push messages are off for this cafe, so the message can't be sent.", 422);

        var guests = await LoadGuestsAsync(nowUtc, ct);
        var members = GuestSegments.Filter(campaign.Segment!, guests, nowUtc, LocalDate(nowUtc)).ToList();
        var recent = await RecentlyMessagedAsync(nowUtc, ct);

        // Mark it sent first, so a double click can't send it twice.
        campaign.SentAt = nowUtc;
        campaign.IsActive = false;
        await db.SaveChangesAsync(ct);

        int sent = 0, skipped = 0, noApp = 0, optedOut = 0;
        var codes = new HashSet<string>();
        foreach (var guest in members)
        {
            if (guest.OptedOut) { optedOut++; continue; }
            if (!guest.HasPhone) { noApp++; continue; }
            if (recent.Contains(guest.Id)) { skipped++; continue; }
            if (await SendToGuestAsync(campaign, guest, nowUtc, codes, ct)) sent++;
            else noApp++;
        }
        await audit.LogAsync("CampaignSent", "MarketingCampaign", id.ToString(), $"{campaign.Name}: {sent} guests", ct);
        return Result<SendCampaignResultDto>.Success(new SendCampaignResultDto(sent, skipped, noApp, optedOut));
    }

    /// <summary>
    /// One run of the automatic messages for the current cafe (called by the background job). Returns how many guests
    /// got a message. Does nothing while the marketing module or pushes are off, or outside the daytime hours.
    /// </summary>
    public async Task<int> RunAutomaticAsync(DateTime nowUtc, CancellationToken ct = default)
    {
        var hour = LocalTime(nowUtc).Hour;
        if (hour < AutoStartHour || hour >= AutoEndHour) return 0;

        var campaigns = await db.MarketingCampaigns
            .Where(c => c.IsActive && c.Kind != CampaignKind.OneOff)
            .ToListAsync(ct);
        if (campaigns.Count == 0) return 0;
        if (!await modules.IsEnabledAsync(ModuleKeys.Marketing, ct) || !await push.IsAvailableAsync(ct)) return 0;

        var guests = (await LoadGuestsAsync(nowUtc, ct)).Where(Reachable).ToList();
        var recent = await RecentlyMessagedAsync(nowUtc, ct);
        var today = LocalDate(nowUtc);
        var codes = new HashSet<string>();
        var total = 0;

        foreach (var campaign in campaigns)
        {
            // When each guest last got this message.
            var lastSent = await db.MarketingMessages
                .Where(m => m.CampaignId == campaign.Id)
                .GroupBy(m => m.CustomerUserId)
                .Select(g => new { g.Key, Last = g.Max(m => m.CreatedAt) })
                .ToDictionaryAsync(x => x.Key, x => x.Last, ct);

            IEnumerable<GuestStats> due = campaign.Kind switch
            {
                // Has not ordered for N days, and has not had this message since their last order.
                CampaignKind.ComeBack => guests.Where(g =>
                    g.LastOrderAt is { } last && last <= nowUtc.AddDays(-campaign.Days)
                    && !(lastSent.TryGetValue(g.Id, out var s) && s >= last)),
                // Birthday within N days, and not greeted in the last ~10 months.
                CampaignKind.Birthday => guests.Where(g =>
                    GuestSegments.DaysToBirthday(g.DateOfBirth, today) is { } d && d <= campaign.Days
                    && !(lastSent.TryGetValue(g.Id, out var s) && s >= nowUtc.AddDays(-300))),
                // Exactly one order, made N days ago (a week's window, so switching it on doesn't message old guests).
                CampaignKind.FirstOrderThanks => guests.Where(g =>
                    g.CompletedOrders == 1 && g.FirstOrderAt is { } first
                    && first <= nowUtc.AddDays(-campaign.Days) && first > nowUtc.AddDays(-campaign.Days - 7)
                    && !lastSent.ContainsKey(g.Id)),
                _ => [],
            };

            var sentHere = 0;
            foreach (var guest in due)
            {
                if (sentHere >= MaxPerRun) break;
                if (campaign.Kind != CampaignKind.Birthday && recent.Contains(guest.Id)) continue;
                if (!await SendToGuestAsync(campaign, guest, nowUtc, codes, ct)) continue;
                recent.Add(guest.Id);
                sentHere++;
            }
            if (sentHere > 0)
                logger.LogInformation("Guest message {Campaign} sent to {Count} guest(s).", campaign.Name, sentHere);
            total += sentHere;
        }
        return total;
    }

    private async Task<HashSet<Guid>> RecentlyMessagedAsync(DateTime nowUtc, CancellationToken ct)
    {
        var since = nowUtc.AddDays(-FrequencyCapDays);
        return (await db.MarketingMessages.Where(m => m.CreatedAt >= since)
            .Select(m => m.CustomerUserId).Distinct().ToListAsync(ct)).ToHashSet();
    }

    /// <summary>Makes the guest's personal code (if the message gives one), logs the message and pushes it.
    /// Returns false (and keeps nothing) when the guest has no phone to send to.</summary>
    private async Task<bool> SendToGuestAsync(
        MarketingCampaign campaign, GuestStats guest, DateTime nowUtc, HashSet<string> takenCodes, CancellationToken ct)
    {
        DiscountCode? code = null;
        if (campaign.DiscountType is { } type && campaign.DiscountValue is { } value)
        {
            code = new DiscountCode
            {
                Code = await NewCodeAsync(campaign.Kind, takenCodes, ct),
                Title = campaign.Name.Length > 200 ? campaign.Name[..200] : campaign.Name,
                DiscountType = type,
                DiscountValue = value,
                MinOrderAmount = campaign.MinOrderAmount,
                MaxUses = 1,
                ExpiresAt = nowUtc.AddDays(campaign.CodeValidDays),
                IsActive = true,
                CustomerUserId = guest.Id,
                CampaignId = campaign.Id,
            };
            db.DiscountCodes.Add(code);
        }
        var message = new MarketingMessage
        {
            CampaignId = campaign.Id, CustomerUserId = guest.Id, DiscountCodeId = code?.Id, CreatedAt = nowUtc,
        };
        db.MarketingMessages.Add(message);
        await db.SaveChangesAsync(ct); // the code works before the push arrives

        var data = new Dictionary<string, string> { ["type"] = "promo" };
        if (code is not null) data["code"] = code.Code;
        var delivered = await push.SendInLanguageAsync(
            guest.Id, lang => Render(campaign, lang, guest.FirstName, code?.Code), data, ct);
        if (delivered) return true;

        db.MarketingMessages.Remove(message);
        if (code is not null) db.DiscountCodes.Remove(code);
        await db.SaveChangesAsync(ct);
        return false;
    }

    private async Task<string> NewCodeAsync(CampaignKind kind, HashSet<string> taken, CancellationToken ct)
    {
        var prefix = kind switch
        {
            CampaignKind.ComeBack => "BACK",
            CampaignKind.Birthday => "BDAY",
            CampaignKind.FirstOrderThanks => "THANKS",
            _ => "GIFT",
        };
        for (var attempt = 0; ; attempt++)
        {
            var chars = new char[attempt < 5 ? 5 : 8];
            for (var i = 0; i < chars.Length; i++) chars[i] = CodeAlphabet[RandomNumberGenerator.GetInt32(CodeAlphabet.Length)];
            var code = $"{prefix}-{new string(chars)}";
            if (!taken.Add(code)) continue;
            if (!await db.DiscountCodes.AnyAsync(d => d.Code == code, ct)) return code;
        }
    }

    // ── Text ─────────────────────────────────────────────────────────────────

    /// <summary>
    /// The title and text for one phone: in the phone's language when the message has it (else Russian, then English,
    /// then Kazakh), with {name}, {code}, {discount} and {days} filled in. When the message gives a code but the text
    /// has no {code}, the code is added on a new line.
    /// </summary>
    public static (string Title, string Body) Render(MarketingCampaign c, string? lang, string? firstName, string? code)
    {
        static string? First(params string?[] options) => options.FirstOrDefault(o => !string.IsNullOrWhiteSpace(o));
        string? Ne(string s) => string.IsNullOrWhiteSpace(s) ? null : s;
        var (title, body) = lang switch
        {
            "kk" => (First(c.TitleKk, c.TitleRu, Ne(c.Title)), First(c.BodyKk, c.BodyRu, Ne(c.Body))),
            "en" => (First(Ne(c.Title), c.TitleRu, c.TitleKk), First(Ne(c.Body), c.BodyRu, c.BodyKk)),
            _ => (First(c.TitleRu, Ne(c.Title), c.TitleKk), First(c.BodyRu, Ne(c.Body), c.BodyKk)),
        };
        var discount = c.DiscountType switch
        {
            DiscountType.Percentage => $"{c.DiscountValue?.ToString("0.##", CultureInfo.InvariantCulture)}%",
            DiscountType.FixedAmount => $"{c.DiscountValue?.ToString("0.##", CultureInfo.InvariantCulture)} ₸",
            _ => "",
        };

        string Fill(string text)
        {
            var name = firstName?.Trim() ?? "";
            if (name.Length == 0)
                text = text.Replace(", {name}", "").Replace(" {name}", "").Replace("{name}", "");
            return text.Replace("{name}", name).Replace("{code}", code ?? "")
                .Replace("{discount}", discount).Replace("{days}", c.CodeValidDays.ToString(CultureInfo.InvariantCulture))
                .Trim();
        }

        var filledBody = Fill(body ?? "");
        if (code is not null && !(body ?? "").Contains("{code}"))
        {
            var label = lang switch { "kk" => "Сіздің промокодыңыз", "en" => "Your promo code", _ => "Ваш промокод" };
            filledBody = $"{filledBody}\n{label}: {code}".Trim();
        }
        return (Fill(title ?? ""), filledBody);
    }

    private static DateTime LocalTime(DateTime nowUtc)
        => TimeZoneInfo.ConvertTimeFromUtc(DateTime.SpecifyKind(nowUtc, DateTimeKind.Utc), CafeTime);

    private static DateOnly LocalDate(DateTime nowUtc) => DateOnly.FromDateTime(LocalTime(nowUtc));
}
