using Yurt.Domain.Common;

namespace Yurt.Domain.Entities;

public class Favorite : BaseEntity, ITenantOwned
{
    public Guid TenantId { get; set; }

    public Guid CustomerUserId { get; set; }
    public Guid MenuItemId { get; set; }

    public CustomerUser CustomerUser { get; set; } = null!;
    public MenuItem MenuItem { get; set; } = null!;
}
