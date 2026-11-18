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

    // Hash of the staff PIN (any role): switches the till to this person and clocks them in/out. Never the PIN itself.
    // Different from every other staff PIN and from every manager PIN of the cafe.
    public string? StaffPinHash { get; set; }
    public DateTime? StaffPinSetAt { get; set; }
}
