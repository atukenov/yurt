using Yurt.Domain.Common;

namespace Yurt.Domain.Entities;

public class UserReport : BaseEntity, ITenantOwned
{
    public Guid TenantId { get; set; }

    public Guid CustomerId { get; set; }
    public string Text { get; set; } = "";
    public bool IsResolved { get; set; }
    public DateTime? ResolvedAt { get; set; }

    public CustomerUser Customer { get; set; } = null!;
}
