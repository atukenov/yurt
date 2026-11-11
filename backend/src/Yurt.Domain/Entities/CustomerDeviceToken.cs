using Yurt.Domain.Common;
using Yurt.Domain.Enums;

namespace Yurt.Domain.Entities;

public class CustomerDeviceToken : BaseEntity, ITenantOwned
{
    public Guid TenantId { get; set; }

    public Guid CustomerUserId { get; set; }
    public string Token { get; set; } = string.Empty;
    public DevicePlatform Platform { get; set; }
    public DateTime LastSeenAt { get; set; } = DateTime.UtcNow;

    /// <summary>App language on this phone (ru, kk or en) when it registered, so marketing messages come in it.</summary>
    public string? Language { get; set; }

    public CustomerUser Customer { get; set; } = null!;
}
