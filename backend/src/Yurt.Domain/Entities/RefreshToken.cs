using Yurt.Domain.Common;

namespace Yurt.Domain.Entities;

public enum RefreshTokenUserType { Customer, Admin }

public class RefreshToken : BaseEntity
{
    public string Token { get; set; } = "";
    public RefreshTokenUserType UserType { get; set; }
    public Guid UserId { get; set; }
    public DateTime ExpiresAt { get; set; }
    public bool IsRevoked { get; set; }
    public string? ReplacedByToken { get; set; }
    public string? CreatedByIp { get; set; }
    public DateTime? RevokedAt { get; set; }
}
