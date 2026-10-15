using System.IdentityModel.Tokens.Jwt;
using System.Security.Claims;
using System.Security.Cryptography;
using System.Text;
using Microsoft.Extensions.Configuration;
using Microsoft.IdentityModel.Tokens;
using Yurt.Application.Common.Interfaces;

namespace Yurt.Infrastructure.Services;

public class JwtTokenService : ITokenService
{
    private readonly IConfiguration _config;

    public JwtTokenService(IConfiguration config) => _config = config;

    public string GenerateCustomerToken(Guid userId, string mobileNumber)
    {
        var claims = new List<Claim>
        {
            new(ClaimTypes.NameIdentifier, userId.ToString()),
            new(ClaimTypes.Name, mobileNumber),
            new("user_type", "customer"),
            new(JwtRegisteredClaimNames.Jti, Guid.NewGuid().ToString())
        };
        return GenerateToken(claims);
    }

    public string GenerateAdminToken(Guid adminId, string username, string role, bool mustChangePassword = false)
    {
        var claims = new List<Claim>
        {
            new(ClaimTypes.NameIdentifier, adminId.ToString()),
            new(ClaimTypes.Name, username),
            new(ClaimTypes.Role, role),
            new("user_type", "admin"),
            new(JwtRegisteredClaimNames.Jti, Guid.NewGuid().ToString())
        };
        if (mustChangePassword)
            claims.Add(new Claim("must_change_password", "true"));
        return GenerateToken(claims);
    }

    public string GenerateRefreshToken()
        => Convert.ToHexString(RandomNumberGenerator.GetBytes(64));

    public int RefreshTokenDays
        => int.Parse(_config["Jwt:RefreshTokenDays"] ?? "7");

    private string GenerateToken(List<Claim> claims)
    {
        var key = new SymmetricSecurityKey(
            Encoding.UTF8.GetBytes(_config["Jwt:Secret"]!));

        var creds = new SigningCredentials(key, SecurityAlgorithms.HmacSha256);
        var minutes = double.Parse(_config["Jwt:AccessTokenMinutes"] ?? "15");

        var token = new JwtSecurityToken(
            issuer: _config["Jwt:Issuer"],
            audience: _config["Jwt:Audience"],
            claims: claims,
            expires: DateTime.UtcNow.AddMinutes(minutes),
            signingCredentials: creds);

        return new JwtSecurityTokenHandler().WriteToken(token);
    }
}
