using Asp.Versioning;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Yurt.Application.Common.Models;
using Yurt.Application.Features.Approvals;
using Yurt.Domain;
using Yurt.WebApi.Common;

namespace Yurt.WebApi.Controllers.Admin;

/// <summary>The manager PIN that approves cancels, refunds and discounts (own_pos). See docs/roles.md.</summary>
[ApiController]
[ApiVersion("1.0")]
[Route("api/v{version:apiVersion}/admin/manager-pin")]
[Authorize(Policy = "AdminOnly")]
public class AdminManagerPinController(ManagerApprovalService approvals) : ApiControllerBase
{
    /// <summary>Is a PIN asked for in this cafe, does any manager have one, and do I?</summary>
    [HttpGet]
    public async Task<IActionResult> Status(CancellationToken ct) => Ok(await approvals.GetStatusAsync(ct));

    /// <summary>Set or change my own PIN (Admin and CEO). Needs my account password.</summary>
    [HttpPut]
    [Authorize(Policy = "AdminRoleAdmin")]
    [RequireModule(ModuleKeys.OwnPos)]
    public async Task<IActionResult> Set([FromBody] SetManagerPinDto dto, CancellationToken ct)
    {
        var result = await approvals.SetPinAsync(dto, ct);
        return result.Succeeded ? NoContent() : ToResult(Result<object>.Failure(result.Error!, result.StatusCode));
    }
}
