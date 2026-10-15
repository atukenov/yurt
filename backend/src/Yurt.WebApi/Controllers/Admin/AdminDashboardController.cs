using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Asp.Versioning;
using Yurt.Application.Features.Analytics.Services;
using Yurt.WebApi.Common;

namespace Yurt.WebApi.Controllers.Admin;

[ApiController]
[ApiVersion("1.0")]
[Route("api/v{version:apiVersion}/admin/dashboard")]
[Authorize(Policy = "AdminOnly")]
public class AdminDashboardController : ApiControllerBase
{
    private readonly AnalyticsService _analytics;

    public AdminDashboardController(AnalyticsService analytics) => _analytics = analytics;

    [HttpGet]
    public async Task<IActionResult> GetDashboard(CancellationToken ct)
        => Ok(await _analytics.GetDashboardAsync(ct));
}
