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
public class AdminPrintersController(PrinterService printers) : ApiControllerBase
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
