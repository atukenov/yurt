namespace Yurt.Application.Features.Marketing;

/// <summary>One guest group: everybody in it, and how many of them a push can reach (app on the phone, offers on).</summary>
public record GuestSegmentDto(string Key, int Guests, int Reachable);

/// <summary>The Guest messages page header: can pushes go out at all, and the guest groups.</summary>
public record MarketingOverviewDto(bool PushReady, List<GuestSegmentDto> Segments);

/// <summary>A guest in a group, for the short list under a group.</summary>
public record SegmentGuestDto(
    Guid Id, string Name, string MobileNumber, int Orders, DateTime? LastOrderAt, decimal TotalSpent, bool Reachable);

public record CampaignDto(
    Guid Id,
    string Kind,
    string Name,
    bool IsActive,
    string? Segment,
    int Days,
    string Title,
    string? TitleRu,
    string? TitleKk,
    string Body,
    string? BodyRu,
    string? BodyKk,
    string? DiscountType,
    decimal? DiscountValue,
    decimal? MinOrderAmount,
    int CodeValidDays,
    DateTime? SentAt,
    DateTime CreatedAt,
    CampaignResultsDto Results);

/// <summary>What a message brought: guests it reached, personal codes used in orders, and the money of those orders.</summary>
public record CampaignResultsDto(int Sent, int CodesUsed, decimal Revenue);

public record SaveCampaignDto(
    string Kind,
    string Name,
    bool IsActive,
    string? Segment,
    int Days,
    string? Title,
    string? TitleRu,
    string? TitleKk,
    string? Body,
    string? BodyRu,
    string? BodyKk,
    string? DiscountType,
    decimal? DiscountValue,
    decimal? MinOrderAmount,
    int? CodeValidDays);

/// <summary>Result of sending a one-off message now.</summary>
public record SendCampaignResultDto(int Sent, int SkippedRecent, int NoApp, int OptedOut);
