namespace Yurt.Application.Common.Interfaces;

public interface ICurrentUserService
{
    Guid? UserId { get; }
    string? Username { get; }
    string? UserType { get; } // "customer" | "admin"
    string? Role { get; }
    bool IsAuthenticated { get; }
}
