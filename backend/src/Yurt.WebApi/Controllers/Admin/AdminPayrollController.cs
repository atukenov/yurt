using Asp.Versioning;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Yurt.Application.Features.Payroll;
using Yurt.Domain;
using Yurt.WebApi.Common;

namespace Yurt.WebApi.Controllers.Admin;

/// <summary>
/// Monthly pay (module <c>payroll</c>): fixed salaries, bonuses and deductions, and the month's pay sheet.
/// CEO only: salaries are not shown to Admins, Workers or the platform operator.
/// </summary>
[ApiController]
[ApiVersion("1.0")]
[Route("api/v{version:apiVersion}/admin/payroll")]
[Authorize(Policy = "CeoPayroll")]
[RequireModule(ModuleKeys.Payroll)]
public class AdminPayrollController(PayrollService payroll) : ApiControllerBase
{
    /// <summary>Every account of the cafe with its monthly salary.</summary>
    [HttpGet("staff")]
    public async Task<IActionResult> Staff(CancellationToken ct) => Ok(await payroll.GetStaffAsync(ct));

    [HttpPut("staff/{id:guid}/salary")]
    public async Task<IActionResult> SetSalary(Guid id, [FromBody] SetSalaryDto dto, CancellationToken ct)
        => ToResult(await payroll.SetSalaryAsync(id, dto.MonthlySalary, dto.HourlyRate, ct));

    /// <summary>The pay sheet of one month (<c>yyyy-MM</c>): live while open, as stored once closed.</summary>
    [HttpGet("months/{month}")]
    public async Task<IActionResult> Month(string month, CancellationToken ct) => ToResult(await payroll.GetMonthAsync(month, ct));

    [HttpPost("adjustments")]
    public async Task<IActionResult> AddAdjustment([FromBody] AddAdjustmentDto dto, CancellationToken ct)
        => ToResult(await payroll.AddAdjustmentAsync(dto, ct));

    [HttpDelete("adjustments/{id:guid}")]
    public async Task<IActionResult> DeleteAdjustment(Guid id, CancellationToken ct)
    {
        var result = await payroll.DeleteAdjustmentAsync(id, ct);
        return result.Succeeded ? NoContent() : StatusCode(result.StatusCode, new ProblemDetails { Title = result.Error });
    }

    [HttpPost("months/{month}/close")]
    public async Task<IActionResult> Close(string month, CancellationToken ct) => ToResult(await payroll.CloseMonthAsync(month, ct));

    [HttpPost("months/{month}/reopen")]
    public async Task<IActionResult> Reopen(string month, CancellationToken ct) => ToResult(await payroll.ReopenMonthAsync(month, ct));
}
