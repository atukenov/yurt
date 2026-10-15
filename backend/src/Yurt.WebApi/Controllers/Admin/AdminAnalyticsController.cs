using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Asp.Versioning;
using Yurt.Application.Features.Analytics.Services;

namespace Yurt.WebApi.Controllers.Admin;

[ApiController]
[ApiVersion("1.0")]
[Route("api/v{version:apiVersion}/admin/analytics")]
[Authorize(Policy = "AdminOnly")]
public class AdminAnalyticsController : ControllerBase
{
    private readonly AnalyticsService _analyticsService;

    public AdminAnalyticsController(AnalyticsService analyticsService)
        => _analyticsService = analyticsService;

    /// <summary>Get analytics data for the specified period and optional location.</summary>
    [HttpGet]
    public async Task<IActionResult> GetAnalytics(
        [FromQuery] string period = "month",
        [FromQuery] Guid? locationId = null,
        CancellationToken ct = default)
        => Ok(await _analyticsService.GetAnalyticsAsync(period, locationId, ct));
}
