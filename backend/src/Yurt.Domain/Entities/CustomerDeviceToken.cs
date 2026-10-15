using Yurt.Domain.Common;
using Yurt.Domain.Enums;

namespace Yurt.Domain.Entities;

public class CustomerDeviceToken : BaseEntity
{
    public Guid CustomerUserId { get; set; }
    public string Token { get; set; } = string.Empty;
    public DevicePlatform Platform { get; set; }
    public DateTime LastSeenAt { get; set; } = DateTime.UtcNow;

    public CustomerUser Customer { get; set; } = null!;
}
