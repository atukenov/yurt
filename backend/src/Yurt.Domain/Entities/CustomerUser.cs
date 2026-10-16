using Yurt.Domain.Common;

namespace Yurt.Domain.Entities;

public class CustomerUser : BaseEntity, ITenantOwned
{
    public Guid TenantId { get; set; }

    public string MobileNumber { get; set; } = string.Empty;
    public string PinHash { get; set; } = string.Empty;
    public string FirstName { get; set; } = string.Empty;
    public string LastName { get; set; } = string.Empty;
    public DateOnly? DateOfBirth { get; set; }
    public bool IsActive { get; set; } = true;
    public int FailedLoginAttempts { get; set; }
    public DateTime? LockoutEnd { get; set; }

    // iiko loyalty link (null until the customer is first synced to iikoCloud)
    public Guid? IikoCustomerId { get; set; }
    public Guid? IikoWalletId { get; set; }

    public ICollection<Order> Orders { get; set; } = new List<Order>();
    public ICollection<Favorite> Favorites { get; set; } = new List<Favorite>();
    public ICollection<CustomerDeviceToken> DeviceTokens { get; set; } = new List<CustomerDeviceToken>();
}
