using Yurt.Domain.Common;
using Yurt.Domain.Enums;

namespace Yurt.Domain.Entities;

/// <summary>
/// A push message to guests (marketing module): an automatic one (come back, birthday, thanks after the first order)
/// that the background job sends by itself, or a one-off message to a guest group that an admin sends once.
/// It can give each guest a personal one-time promo code.
/// </summary>
public class MarketingCampaign : BaseEntity, ITenantOwned
{
    public Guid TenantId { get; set; }

    public CampaignKind Kind { get; set; }
    public string Name { get; set; } = string.Empty;
    public bool IsActive { get; set; } = true;

    /// <summary>One-off messages: the guest group (see GuestSegments). Ignored by automatic ones.</summary>
    public string? Segment { get; set; }

    /// <summary>Come back: days without an order. Birthday: days before the birthday. Thanks: days after the first order.</summary>
    public int Days { get; set; }

    // Message text; {name}, {code}, {discount} and {days} are filled in per guest. English is the fallback column.
    public string Title { get; set; } = string.Empty;
    public string? TitleRu { get; set; }
    public string? TitleKk { get; set; }
    public string Body { get; set; } = string.Empty;
    public string? BodyRu { get; set; }
    public string? BodyKk { get; set; }

    /// <summary>Personal code given with the message; null = message only.</summary>
    public DiscountType? DiscountType { get; set; }
    public decimal? DiscountValue { get; set; }
    public decimal? MinOrderAmount { get; set; }
    public int CodeValidDays { get; set; } = 7;

    /// <summary>One-off messages: when it was sent (each is sent once).</summary>
    public DateTime? SentAt { get; set; }

    public ICollection<MarketingMessage> Messages { get; set; } = new List<MarketingMessage>();
}

/// <summary>One message sent to one guest; used for "not too often" rules, "send once" rules and results.</summary>
public class MarketingMessage : BaseEntity, ITenantOwned
{
    public Guid TenantId { get; set; }

    public Guid CampaignId { get; set; }
    public MarketingCampaign Campaign { get; set; } = null!;
    public Guid CustomerUserId { get; set; }
    public Guid? DiscountCodeId { get; set; }
}
