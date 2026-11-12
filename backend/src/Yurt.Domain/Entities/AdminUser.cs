using Yurt.Domain.Common;
using Yurt.Domain.Enums;

namespace Yurt.Domain.Entities;

public class AdminUser : BaseEntity, ITenantOwned
{
    public Guid TenantId { get; set; }

    public string Username { get; set; } = string.Empty;
    public string PasswordHash { get; set; } = string.Empty;
    public AdminRole Role { get; set; } = AdminRole.Worker;
    public bool IsActive { get; set; } = true;
    public bool MustChangePassword { get; set; }

    // Hash of the manager PIN (Admin and CEO only) that approves cancels, refunds and discounts at the till.
    // Null = no PIN set. Never the PIN itself.
    public string? ManagerPinHash { get; set; }
    public DateTime? ManagerPinSetAt { get; set; }
}
