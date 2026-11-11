using Yurt.Domain.Common;
using Yurt.Domain.Enums;

namespace Yurt.Domain.Entities;

public class DiscountCode : BaseEntity, ITenantOwned
{
    public Guid TenantId { get; set; }

    public string Code { get; set; } = string.Empty;
    public string Title { get; set; } = string.Empty;
    public DiscountType DiscountType { get; set; }
    public decimal DiscountValue { get; set; }
    public int? MaxUses { get; set; }
    public int UsedCount { get; set; } = 0;
    public decimal? MinOrderAmount { get; set; }
    public DateTime? StartsAt { get; set; }
    public DateTime? ExpiresAt { get; set; }
    public bool IsActive { get; set; } = true;

    /// <summary>A personal code: only this guest can use it (made by an automatic message). Null = anyone.</summary>
    public Guid? CustomerUserId { get; set; }

    /// <summary>The guest message that made this personal code, for its results.</summary>
    public Guid? CampaignId { get; set; }

    public ICollection<Order> Orders { get; set; } = new List<Order>();
}
