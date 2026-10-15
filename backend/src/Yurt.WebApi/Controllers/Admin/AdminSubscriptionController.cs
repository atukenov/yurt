using Asp.Versioning;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Yurt.Application.Common.Interfaces;
using Yurt.WebApi.Common;

namespace Yurt.WebApi.Controllers.Admin;

/// <summary>Read-only view of which modules are on and when they expire. Editing arrives with the /platform tools (Phase 2).</summary>
[ApiController]
[ApiVersion("1.0")]
[Route("api/v{version:apiVersion}/admin/subscription")]
[Authorize(Policy = "AdminOnly")]
public class AdminSubscriptionController : ApiControllerBase
{
    private readonly IModuleGate _modules;

    public AdminSubscriptionController(IModuleGate modules) => _modules = modules;

    [HttpGet]
    public async Task<IActionResult> Get(CancellationToken ct)
    {
        var statuses = await _modules.GetStatusesAsync(ct);
        return Ok(new
        {
            modules = statuses.Select(s => new
            {
                key = s.Key,
                state = s.State.ToString().ToLowerInvariant(),
                enabled = s.IsEnabled,
                validUntil = s.ValidUntil,
                graceEndsAt = s.GraceEndsAt,
                source = s.Source,
                notes = s.Notes
            })
        });
    }
}
