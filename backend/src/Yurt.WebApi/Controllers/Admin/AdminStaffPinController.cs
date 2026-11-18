using Asp.Versioning;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Yurt.Application.Common.Models;
using Yurt.Application.Features.Auth.Services;
using Yurt.Application.Features.TimeClock;
using Yurt.Domain;
using Yurt.WebApi.Common;

namespace Yurt.WebApi.Controllers.Admin;

public record SwitchStaffDto(string? Pin, string? RefreshToken);

/// <summary>The staff PIN (own_pos): switch the till to another person and clock in or out. See docs/roles.md.</summary>
[ApiController]
[ApiVersion("1.0")]
[Route("api/v{version:apiVersion}/admin/staff-pin")]
[Authorize(Policy = "AdminOnly")]
[RequireModule(ModuleKeys.OwnPos)]
public class AdminStaffPinController(StaffPinService pins, AuthService auth) : ApiControllerBase
{
    /// <summary>Do I have a staff PIN?</summary>
    [HttpGet]
    public async Task<IActionResult> Status(CancellationToken ct) => Ok(new { iHavePin = await pins.HasPinAsync(ct) });

    /// <summary>Set or change my own staff PIN. Needs my account password.</summary>
    [HttpPut]
    public async Task<IActionResult> SetOwn([FromBody] SetStaffPinDto dto, CancellationToken ct) => NoContent(await pins.SetOwnAsync(dto, ct));

    /// <summary>The staff I may set a PIN for (Admin: workers; CEO: everybody) and whether they have one.</summary>
    [HttpGet("people")]
    [Authorize(Policy = "AdminRoleAdmin")]
    public async Task<IActionResult> People(CancellationToken ct) => Ok(await pins.ListAsync(ct));

    [HttpPut("{id:guid}")]
    [Authorize(Policy = "AdminRoleAdmin")]
    public async Task<IActionResult> SetFor(Guid id, [FromBody] SetOtherStaffPinDto dto, CancellationToken ct)
        => NoContent(await pins.SetForAsync(id, dto.Pin, ct));

    [HttpDelete("{id:guid}")]
    [Authorize(Policy = "AdminRoleAdmin")]
    public async Task<IActionResult> Clear(Guid id, CancellationToken ct) => NoContent(await pins.ClearAsync(id, ct));

    /// <summary>The till signs in as the person who owns the PIN (Workers and Admins). Returns new tokens, like a login.</summary>
    [HttpPost("switch")]
    public async Task<IActionResult> Switch([FromBody] SwitchStaffDto dto, CancellationToken ct)
    {
        var found = await pins.FindForSwitchAsync(dto.Pin, ct);
        if (!found.Succeeded) return ToResult(Result<object>.Failure(found.Error!, found.StatusCode));
        return ToResult(await auth.SwitchAdminSessionAsync(found.Data!, dto.RefreshToken, HttpContext.Connection.RemoteIpAddress?.ToString(), ct));
    }

    private IActionResult NoContent(Result result)
        => result.Succeeded ? base.NoContent() : ToResult(Result<object>.Failure(result.Error!, result.StatusCode));
}
