using Yurt.Domain.Common;

namespace Yurt.Domain.Entities;

public class GroupCartMember : BaseEntity
{
    public Guid GroupCartId { get; set; }
    public Guid CustomerUserId { get; set; }
    public string DisplayName { get; set; } = string.Empty;

    public GroupCart GroupCart { get; set; } = null!;
}
