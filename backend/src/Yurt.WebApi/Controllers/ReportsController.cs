using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Asp.Versioning;
using Yurt.Application.Common.Interfaces;
using Yurt.Application.Features.Reports;
using Yurt.WebApi.Common;

namespace Yurt.WebApi.Controllers;

[ApiController]
[ApiVersion("1.0")]
[Route("api/v{version:apiVersion}/reports")]
[Authorize(Policy = "CustomerOnly")]
public class ReportsController : ApiControllerBase
{
    private readonly ReportService _reports;
    private readonly ICurrentUserService _currentUser;

    public ReportsController(ReportService reports, ICurrentUserService currentUser)
    {
        _reports = reports;
        _currentUser = currentUser;
    }

    [HttpPost]
    public async Task<IActionResult> Create([FromBody] CreateReportDto dto, CancellationToken ct)
    {
        if (string.IsNullOrWhiteSpace(dto.Text))
            return BadRequest("Report text is required.");

        var result = await _reports.CreateReportAsync(_currentUser.UserId!.Value, dto.Text, ct);
        return result.Succeeded ? Ok() : BadRequest(result.Error);
    }
}
