using Asp.Versioning;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Yurt.Application.Features.Shifts;
using Yurt.Domain;
using Yurt.WebApi.Common;

namespace Yurt.WebApi.Controllers.Admin;

/// <summary>Tips per staff member and per cashier shift (own_pos). Tips are staff money, so CEO only.</summary>
[ApiController]
[ApiVersion("1.0")]
[Route("api/v{version:apiVersion}/admin/tips")]
[Authorize(Policy = "CeoOnly")]
[RequireModule(ModuleKeys.OwnPos)]
public class AdminTipsController(TipService tips) : ApiControllerBase
{
    /// <summary>Tips between two days (<c>yyyy-MM-dd</c>, both included), for the whole cafe or one branch.</summary>
    [HttpGet]
    public async Task<IActionResult> Report([FromQuery] string? from, [FromQuery] string? to, [FromQuery] Guid? locationId, CancellationToken ct)
        => ToResult(await tips.ReportAsync(from, to, locationId, ct));
}
