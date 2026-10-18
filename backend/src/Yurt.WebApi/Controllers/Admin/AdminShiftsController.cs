using Asp.Versioning;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Yurt.Application.Features.Shifts;
using Yurt.Domain;
using Yurt.WebApi.Common;

namespace Yurt.WebApi.Controllers.Admin;

/// <summary>Cashier shifts, cash in/out and the X/Z report (own_pos).</summary>
[ApiController]
[ApiVersion("1.0")]
[Route("api/v{version:apiVersion}/admin/shifts")]
[Authorize(Policy = "AdminOnly")]
[RequireModule(ModuleKeys.OwnPos)]
public class AdminShiftsController(ShiftService shifts) : ApiControllerBase
{
    [HttpGet("current")]
    public async Task<IActionResult> Current([FromQuery] Guid locationId, CancellationToken ct)
        => ToResult(await shifts.CurrentAsync(locationId, ct));

    [HttpGet]
    public async Task<IActionResult> List([FromQuery] Guid locationId, CancellationToken ct)
        => ToResult(await shifts.ListAsync(locationId, ct));

    [HttpPost("open")]
    public async Task<IActionResult> Open([FromBody] OpenShiftDto dto, CancellationToken ct)
        => ToResult(await shifts.OpenAsync(dto, ct));

    [HttpPost("{id:guid}/cash")]
    public async Task<IActionResult> Cash(Guid id, [FromBody] CashMovementInputDto dto, CancellationToken ct)
        => ToResult(await shifts.AddMovementAsync(id, dto, ct));

    [HttpPost("{id:guid}/close")]
    public async Task<IActionResult> Close(Guid id, [FromBody] CloseShiftDto dto, CancellationToken ct)
        => ToResult(await shifts.CloseAsync(id, dto, ct));

    [HttpGet("{id:guid}/report")]
    public async Task<IActionResult> Report(Guid id, CancellationToken ct)
        => ToResult(await shifts.ReportAsync(id, ct));
}
