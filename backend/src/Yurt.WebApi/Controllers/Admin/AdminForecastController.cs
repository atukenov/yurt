using Asp.Versioning;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Yurt.Application.Features.Forecast;
using Yurt.Domain;
using Yurt.WebApi.Common;

namespace Yurt.WebApi.Controllers.Admin;

/// <summary>
/// Sales forecast and daily prep plan of a branch (own_pos). It shows item counts and ingredient amounts, never money, so
/// every staff role can open it.
/// </summary>
[ApiController]
[ApiVersion("1.0")]
[Route("api/v{version:apiVersion}/admin/forecast")]
[Authorize(Policy = "AdminOnly")]
[RequireModule(ModuleKeys.OwnPos)]
public class AdminForecastController(ForecastService forecast) : ApiControllerBase
{
    /// <summary>
    /// What to expect on <paramref name="date"/> (<c>yyyy-MM-dd</c>, default today) at the branch. <paramref name="adjust"/>
    /// makes the day busier or quieter in percent (a holiday, an event); <paramref name="buffer"/> is the safety margin.
    /// </summary>
    [HttpGet]
    public async Task<IActionResult> Get(
        [FromQuery] Guid locationId, [FromQuery] string? date, [FromQuery] int adjust = 0,
        [FromQuery] int buffer = ForecastMath.DefaultBufferPercent, CancellationToken ct = default)
    {
        if (locationId == Guid.Empty) return ToResult(Yurt.Application.Common.Models.Result<ForecastDto>.Failure("Pick a branch.", 422));
        var day = ForecastService.LocalNow(DateTime.UtcNow).Date;
        if (date is not null && !ForecastService.TryParseDate(date, out day))
            return ToResult(Yurt.Application.Common.Models.Result<ForecastDto>.Failure("Date must look like 2026-11-14.", 422));
        return ToResult(await forecast.GetAsync(locationId, day, adjust, buffer, ct));
    }
}
