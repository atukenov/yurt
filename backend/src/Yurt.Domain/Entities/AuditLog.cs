using Yurt.Domain.Common;

namespace Yurt.Domain.Entities;

public class AuditLog : BaseEntity, ITenantOwned
{
    public Guid TenantId { get; set; }

    public string Action { get; set; } = "";
    public string EntityType { get; set; } = "";
    public string? EntityId { get; set; }
    public Guid? PerformedByAdminId { get; set; }
    public string PerformedByUsername { get; set; } = "";
    public string? Details { get; set; }
    public string? IpAddress { get; set; }
}
