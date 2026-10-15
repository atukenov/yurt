using Yurt.Domain.Common;
using Yurt.Domain.Enums;

namespace Yurt.Domain.Entities;

public class GroupCart : BaseEntity
{
    public string Code { get; set; } = string.Empty;
    public Guid LocationId { get; set; }
    public Guid CreatedByUserId { get; set; }
    public GroupCartStatus Status { get; set; } = GroupCartStatus.Open;
    public DateTime ExpiresAt { get; set; }

    public Location Location { get; set; } = null!;
    public ICollection<GroupCartItem> Items { get; set; } = [];
    public ICollection<GroupCartMember> Members { get; set; } = [];
}
