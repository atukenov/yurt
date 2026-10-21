using Asp.Versioning;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Yurt.Application.Features.Printing;
using Yurt.Domain;
using Yurt.WebApi.Common;

namespace Yurt.WebApi.Controllers.Admin;

/// <summary>Network printers per branch, test print and the print job list (own_pos). Setting printers up needs an Admin.</summary>
[ApiController]
[ApiVersion("1.0")]
[Route("api/v{version:apiVersion}/admin/printers")]
[Authorize(Policy = "AdminOnly")]
[RequireModule(ModuleKeys.OwnPos)]
public class AdminPrintersController(PrinterService printers, PrintAgentService agents) : ApiControllerBase
{
    [HttpGet]
    public async Task<IActionResult> List(CancellationToken ct) => Ok(await printers.ListAsync(ct));

    [HttpGet("jobs")]
    public async Task<IActionResult> Jobs([FromQuery] int limit = 30, CancellationToken ct = default)
        => Ok(await printers.RecentJobsAsync(limit, ct));

    [HttpPost]
    [Authorize(Policy = "AdminRoleAdmin")]
    public async Task<IActionResult> Create([FromBody] SavePrinterDto dto, CancellationToken ct)
        => ToResult(await printers.CreateAsync(dto, ct));

    [HttpPut("{id:guid}")]
    [Authorize(Policy = "AdminRoleAdmin")]
    public async Task<IActionResult> Update(Guid id, [FromBody] SavePrinterDto dto, CancellationToken ct)
        => ToResult(await printers.UpdateAsync(id, dto, ct));

    [HttpDelete("{id:guid}")]
    [Authorize(Policy = "AdminRoleAdmin")]
    public async Task<IActionResult> Delete(Guid id, CancellationToken ct) => ToResult(await printers.DeleteAsync(id, ct));

    [HttpPost("{id:guid}/test")]
    public async Task<IActionResult> Test(Guid id, CancellationToken ct) => ToResult(await printers.TestAsync(id, ct));

    /// <summary>The print agent of each branch that has one: when it last asked for jobs.</summary>
    [HttpGet("agents")]
    public async Task<IActionResult> Agents(CancellationToken ct) => Ok(await agents.ListAsync(ct));

    /// <summary>Makes a new key for a branch's print agent (shown once); the old key stops working.</summary>
    [HttpPost("agents/{locationId:guid}")]
    [Authorize(Policy = "AdminRoleAdmin")]
    public async Task<IActionResult> CreateAgentKey(Guid locationId, CancellationToken ct)
        => ToResult(await agents.CreateKeyAsync(locationId, ct));

    [HttpDelete("agents/{locationId:guid}")]
    [Authorize(Policy = "AdminRoleAdmin")]
    public async Task<IActionResult> RevokeAgentKey(Guid locationId, CancellationToken ct)
        => ToResult(await agents.RevokeAsync(locationId, ct));
}

/// <summary>Print buttons on the kitchen screen and the orders page (own_pos).</summary>
[ApiController]
[ApiVersion("1.0")]
[Route("api/v{version:apiVersion}/admin/orders/{orderId:guid}/print")]
[Authorize(Policy = "AdminOnly")]
[RequireModule(ModuleKeys.OwnPos)]
public class AdminOrderPrintController(PrintService print) : ApiControllerBase
{
    [HttpPost("ticket")]
    public async Task<IActionResult> Ticket(Guid orderId, [FromQuery] Guid? stationId, CancellationToken ct)
        => ToResult(await print.PrintTicketAsync(orderId, stationId, ct));

    [HttpPost("receipt")]
    public async Task<IActionResult> Receipt(Guid orderId, CancellationToken ct) => ToResult(await print.PrintReceiptAsync(orderId, ct));
}

/// <summary>
/// The Open drawer button on the Cashier page: opens the cash drawer plugged into the branch's receipt printer (own_pos).
/// Any staff member may press it; each press is in the audit log.
/// </summary>
[ApiController]
[ApiVersion("1.0")]
[Route("api/v{version:apiVersion}/admin/cash-drawer")]
[Authorize(Policy = "AdminOnly")]
[RequireModule(ModuleKeys.OwnPos)]
public class AdminCashDrawerController(PrintService print) : ApiControllerBase
{
    [HttpPost("{locationId:guid}/open")]
    public async Task<IActionResult> Open(Guid locationId, CancellationToken ct) => ToResult(await print.OpenDrawerAsync(locationId, ct));
}

/// <summary>
/// Used by the print agent that runs in the cafe (scripts/print-agent.mjs). It signs in with its key in the
/// <c>X-Print-Agent-Key</c> header and names its cafe with <c>X-Tenant</c> (own_pos).
/// </summary>
[ApiController]
[ApiVersion("1.0")]
[Route("api/v{version:apiVersion}/print-agent")]
[AllowAnonymous]
[RequireModule(ModuleKeys.OwnPos)]
public class PrintAgentController(PrintAgentService agents) : ApiControllerBase
{
    public const string KeyHeader = "X-Print-Agent-Key";

    [HttpPost("jobs/claim")]
    public async Task<IActionResult> Claim([FromHeader(Name = KeyHeader)] string? key, CancellationToken ct)
        => ToResult(await agents.ClaimAsync(key, ct));

    [HttpPost("jobs/{id:guid}/result")]
    public async Task<IActionResult> Result(Guid id, [FromHeader(Name = KeyHeader)] string? key, [FromBody] AgentJobResultDto dto, CancellationToken ct)
        => ToResult(await agents.ReportAsync(key, id, dto, ct));
}
