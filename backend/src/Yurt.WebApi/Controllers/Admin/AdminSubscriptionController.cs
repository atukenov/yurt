using Asp.Versioning;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Yurt.Application.Common.Interfaces;
using Yurt.Application.Features.Billing;
using Yurt.Application.Features.Tenants;
using Yurt.WebApi.Common;

namespace Yurt.WebApi.Controllers.Admin;

/// <summary>Which modules are on and when they expire, and the cafe's monthly card payment to the platform owner
/// (Stripe subscription). Modules themselves are edited on the /platform screen.</summary>
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

    /// <summary>The cafe's monthly plan, its Stripe subscription status and the date it is paid until.</summary>
    [HttpGet("billing")]
    public async Task<IActionResult> GetBilling([FromServices] BillingService billing, CancellationToken ct)
        => Ok(await billing.GetForCafeAsync(ct));

    /// <summary>Opens Stripe's page to start paying monthly by card. Owners only, not staff.</summary>
    [HttpPost("billing/checkout")]
    [Authorize(Policy = "AdminRoleAdmin")]
    public async Task<IActionResult> StartCheckout([FromServices] BillingService billing, [FromBody] BillingRedirectRequest dto, CancellationToken ct)
        => ToResult(await billing.StartCheckoutAsync(dto.ReturnUrl, ct));

    /// <summary>Opens Stripe's billing page: change the card, see invoices, cancel. Owners only, not staff.</summary>
    [HttpPost("billing/portal")]
    [Authorize(Policy = "AdminRoleAdmin")]
    public async Task<IActionResult> OpenPortal([FromServices] BillingService billing, [FromBody] BillingRedirectRequest dto, CancellationToken ct)
        => ToResult(await billing.OpenPortalAsync(dto.ReturnUrl, ct));
}
