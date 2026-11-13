using Asp.Versioning;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Yurt.Application.Common.Models;
using Yurt.Application.Features.TimeClock;
using Yurt.Domain;
using Yurt.WebApi.Common;

namespace Yurt.WebApi.Controllers.Admin;

/// <summary>Clock-in and clock-out with the staff PIN, and worked hours (own_pos). See docs/roles.md.</summary>
[ApiController]
[ApiVersion("1.0")]
[Route("api/v{version:apiVersion}/admin/time-clock")]
[Authorize(Policy = "AdminOnly")]
[RequireModule(ModuleKeys.OwnPos)]
public class AdminTimeClockController(TimeClockService clock) : ApiControllerBase
{
    /// <summary>Clock in, or clock out if already in. The PIN says who.</summary>
    [HttpPost("punch")]
    public async Task<IActionResult> Punch([FromBody] PunchDto dto, CancellationToken ct) => ToResult(await clock.PunchAsync(dto, ct));

    /// <summary>Who is clocked in right now.</summary>
    [HttpGet("now")]
    public async Task<IActionResult> Now(CancellationToken ct) => Ok(await clock.OnTheClockAsync(ct));

    /// <summary>Worked hours per person and every entry for a period (Admin, CEO).</summary>
    [HttpGet("hours")]
    [Authorize(Policy = "AdminRoleAdmin")]
    public async Task<IActionResult> Hours([FromQuery] string? from, [FromQuery] string? to, [FromQuery] Guid? staffId, CancellationToken ct)
        => ToResult(await clock.ReportAsync(from, to, staffId, ct));

    /// <summary>Add a forgotten entry by hand. A reason is required and goes into the audit log.</summary>
    [HttpPost("entries")]
    [Authorize(Policy = "AdminRoleAdmin")]
    public async Task<IActionResult> Add([FromBody] SaveTimeEntryDto dto, CancellationToken ct) => ToResult(await clock.SaveAsync(null, dto, ct));

    /// <summary>Fix the times of an entry (e.g. the clock-out of a forgotten one).</summary>
    [HttpPut("entries/{id:guid}")]
    [Authorize(Policy = "AdminRoleAdmin")]
    public async Task<IActionResult> Change(Guid id, [FromBody] SaveTimeEntryDto dto, CancellationToken ct) => ToResult(await clock.SaveAsync(id, dto, ct));

    [HttpDelete("entries/{id:guid}")]
    [Authorize(Policy = "AdminRoleAdmin")]
    public async Task<IActionResult> Delete(Guid id, [FromQuery] string? reason, CancellationToken ct)
    {
        var result = await clock.DeleteAsync(id, reason, ct);
        return result.Succeeded ? NoContent() : ToResult(Result<object>.Failure(result.Error!, result.StatusCode));
    }
}
