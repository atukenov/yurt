using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Asp.Versioning;
using Yurt.Application.Features.Reports;
using Yurt.WebApi.Common;

namespace Yurt.WebApi.Controllers.Admin;

[ApiController]
[ApiVersion("1.0")]
[Route("api/v{version:apiVersion}/admin/reports")]
[Authorize(Policy = "AdminOnly")]
public class AdminReportsController : ApiControllerBase
{
    private readonly ReportService _reports;

    public AdminReportsController(ReportService reports) => _reports = reports;

    [HttpGet]
    public async Task<IActionResult> GetReports([FromQuery] bool resolved = false, CancellationToken ct = default)
        => Ok(await _reports.GetReportsAsync(resolved, ct));

    [HttpPost("{id:guid}/resolve")]
    public async Task<IActionResult> Resolve(Guid id, CancellationToken ct)
    {
        var result = await _reports.MarkResolvedAsync(id, ct);
        return result.Succeeded ? Ok() : NotFound(result.Error);
    }
}
