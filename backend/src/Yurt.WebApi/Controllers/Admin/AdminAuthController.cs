using System.Security.Claims;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Asp.Versioning;
using Yurt.Application.Features.Auth.DTOs;
using Yurt.Application.Features.Auth.Services;
using Yurt.Application.Features.Auth.Validators;
using Yurt.WebApi.Common;

namespace Yurt.WebApi.Controllers.Admin;

[ApiController]
[ApiVersion("1.0")]
[Route("api/v{version:apiVersion}/admin/auth")]
public class AdminAuthController : ApiControllerBase
{
    private readonly AuthService _authService;

    public AdminAuthController(AuthService authService) => _authService = authService;

    /// <summary>Admin login with username and password.</summary>
    [HttpPost("login")]
    public async Task<IActionResult> Login([FromBody] AdminLoginDto dto, CancellationToken ct)
    {
        var validator = new AdminLoginValidator();
        var validation = await validator.ValidateAsync(dto, ct);
        if (!validation.IsValid)
            return ValidationError(string.Join("; ", validation.Errors.Select(e => e.ErrorMessage)));

        var ip = HttpContext.Connection.RemoteIpAddress?.ToString();
        var result = await _authService.LoginAdminAsync(dto, ip, ct);
        return ToResult(result);
    }

    /// <summary>Rotate an admin refresh token.</summary>
    [HttpPost("refresh")]
    public async Task<IActionResult> Refresh([FromBody] RefreshRequestDto dto, CancellationToken ct)
    {
        var ip = HttpContext.Connection.RemoteIpAddress?.ToString();
        var result = await _authService.RefreshAsync(dto.RefreshToken, ip, ct);
        return ToResult(result);
    }

    /// <summary>Revoke an admin refresh token (logout).</summary>
    [HttpPost("logout")]
    public async Task<IActionResult> Logout([FromBody] RefreshRequestDto dto, CancellationToken ct)
    {
        var ip = HttpContext.Connection.RemoteIpAddress?.ToString();
        await _authService.RevokeAsync(dto.RefreshToken, ip, ct);
        return NoContent();
    }

    /// <summary>Change the authenticated admin's password and clear MustChangePassword flag.</summary>
    [HttpPost("change-password")]
    [Authorize(Policy = "AdminOnly")]
    public async Task<IActionResult> ChangePassword([FromBody] ChangeAdminPasswordDto dto, CancellationToken ct)
    {
        var adminIdClaim = User.FindFirstValue(ClaimTypes.NameIdentifier);
        if (!Guid.TryParse(adminIdClaim, out var adminId))
            return Unauthorized();

        var result = await _authService.ChangeAdminPasswordAsync(adminId, dto, ct);
        return ToResult(result);
    }
}
