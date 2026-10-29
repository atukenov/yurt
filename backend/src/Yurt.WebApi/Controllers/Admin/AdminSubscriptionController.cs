using Asp.Versioning;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Yurt.Application.Common.Interfaces;
using Yurt.Application.Features.Tenants;
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

    private readonly CafeAccessService _access;
    private readonly BillingOptions _billing;

    public AdminSubscriptionController(IModuleGate modules, CafeAccessService access, BillingOptions billing)
    {
        _modules = modules;
        _access = access;
        _billing = billing;
    }

    [HttpGet]
    public async Task<IActionResult> Get(CancellationToken ct)
    {
        var statuses = await _modules.GetStatusesAsync(ct);
        var access = await _access.GetAsync(ct);
        return Ok(new
        {
            // Limited mode (cafe stopped paying Yurt): staff see a banner; new customer orders are refused.
            access = new
            {
                state = access.State.ToString().ToLowerInvariant(),
                paidUntil = access.PaidUntil,
                limitedFrom = access.LimitedFrom,
                limitedByOperator = access.ByOperator,
                graceDays = _billing.GraceDays
            },
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
