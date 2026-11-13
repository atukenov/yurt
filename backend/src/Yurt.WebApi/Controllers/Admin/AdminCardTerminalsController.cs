using Asp.Versioning;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Yurt.Application.Features.CardTerminals;
using Yurt.Domain;
using Yurt.WebApi.Common;

namespace Yurt.WebApi.Controllers.Admin;

/// <summary>
/// Bank card terminals and the card payments sent to them (own_pos). Off by default: a branch without a terminal keeps
/// taking card payments by hand. Setting terminals up and resolving doubtful payments needs an Admin; any staff member at
/// the till can start a payment and watch it.
/// </summary>
[ApiController]
[ApiVersion("1.0")]
[Route("api/v{version:apiVersion}/admin/card-terminals")]
[Authorize(Policy = "AdminOnly")]
[RequireModule(ModuleKeys.OwnPos)]
public class AdminCardTerminalsController(CardTerminalService terminals) : ApiControllerBase
{
    [HttpGet]
    public async Task<IActionResult> List(CancellationToken ct) => Ok(await terminals.ListAsync(ct));

    [HttpPost]
    [Authorize(Policy = "AdminRoleAdmin")]
    public async Task<IActionResult> Create([FromBody] SaveCardTerminalDto dto, CancellationToken ct)
        => ToResult(await terminals.CreateAsync(dto, ct));

    [HttpPut("{id:guid}")]
    [Authorize(Policy = "AdminRoleAdmin")]
    public async Task<IActionResult> Update(Guid id, [FromBody] SaveCardTerminalDto dto, CancellationToken ct)
        => ToResult(await terminals.UpdateAsync(id, dto, ct));

    [HttpDelete("{id:guid}")]
    [Authorize(Policy = "AdminRoleAdmin")]
    public async Task<IActionResult> Delete(Guid id, CancellationToken ct) => ToResult(await terminals.DeleteAsync(id, ct));

    [HttpGet("payments")]
    public async Task<IActionResult> Recent([FromQuery] int limit = 30, CancellationToken ct = default)
        => Ok(await terminals.RecentAsync(limit, ct));

    /// <summary>Sends an amount to the terminal. The till then polls <c>payments/{id}</c> until the status settles.</summary>
    [HttpPost("payments")]
    public async Task<IActionResult> Start([FromBody] StartCardPaymentDto dto, CancellationToken ct)
        => ToResult(await terminals.StartAsync(dto, ct));

    [HttpGet("payments/{id:guid}")]
    public async Task<IActionResult> Get(Guid id, CancellationToken ct) => ToResult(await terminals.GetAsync(id, ct));

    [HttpPost("payments/{id:guid}/cancel")]
    public async Task<IActionResult> Cancel(Guid id, CancellationToken ct) => ToResult(await terminals.CancelAsync(id, ct));

    [HttpPost("payments/{id:guid}/refund")]
    [Authorize(Policy = "AdminRoleAdmin")]
    public async Task<IActionResult> Refund(Guid id, CancellationToken ct) => ToResult(await terminals.RefundAsync(id, ct));

    [HttpPost("payments/{id:guid}/resolve")]
    [Authorize(Policy = "AdminRoleAdmin")]
    public async Task<IActionResult> Resolve(Guid id, [FromBody] ResolveCardPaymentDto dto, CancellationToken ct)
        => ToResult(await terminals.ResolveAsync(id, dto, ct));
}

/// <summary>
/// Used by the branch agent in the cafe (the same agent and key as printing). It signs in with <c>X-Print-Agent-Key</c> and
/// names its cafe with <c>X-Tenant</c> (own_pos).
/// </summary>
[ApiController]
[ApiVersion("1.0")]
[Route("api/v{version:apiVersion}/print-agent/terminal")]
[AllowAnonymous]
[RequireModule(ModuleKeys.OwnPos)]
public class CardTerminalAgentController(CardTerminalService terminals) : ApiControllerBase
{
    [HttpPost("claim")]
    public async Task<IActionResult> Claim([FromHeader(Name = PrintAgentController.KeyHeader)] string? key, CancellationToken ct)
        => ToResult(await terminals.ClaimAsync(key, ct));

    [HttpPost("{id:guid}/result")]
    public async Task<IActionResult> Result(Guid id, [FromHeader(Name = PrintAgentController.KeyHeader)] string? key,
        [FromBody] AgentTerminalResultDto dto, CancellationToken ct)
        => ToResult(await terminals.ReportAsync(key, id, dto, ct));
}
