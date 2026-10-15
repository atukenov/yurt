namespace Yurt.Application.Common.Interfaces;

public interface ITokenService
{
    string GenerateCustomerToken(Guid userId, string mobileNumber);
    string GenerateAdminToken(Guid adminId, string username, string role, bool mustChangePassword = false);
    string GenerateRefreshToken();
    int RefreshTokenDays { get; }
}
