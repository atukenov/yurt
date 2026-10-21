using Asp.Versioning;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Yurt.Application.Features.Stock;
using Yurt.Domain;
using Yurt.WebApi.Common;

namespace Yurt.WebApi.Controllers.Admin;

/// <summary>Per-branch stock counts and the automatic stop-list (own_pos).</summary>
[ApiController]
[ApiVersion("1.0")]
[Route("api/v{version:apiVersion}/admin/stock")]
[Authorize(Policy = "AdminOnly")]
[RequireModule(ModuleKeys.OwnPos)]
public class AdminStockController(StockService stock) : ApiControllerBase
{
    [HttpGet]
    public async Task<IActionResult> List([FromQuery] Guid locationId, CancellationToken ct)
        => ToResult(await stock.ListAsync(locationId, ct));

    /// <summary>What is running low or out, in one branch or in all of them when no branch is given.</summary>
    [HttpGet("alerts")]
    public async Task<IActionResult> Alerts([FromQuery] Guid? locationId, CancellationToken ct)
        => Ok(await stock.AlertsAsync(locationId, ct));

    [HttpPut("{menuItemId:guid}/{locationId:guid}")]
    public async Task<IActionResult> Set(Guid menuItemId, Guid locationId, [FromBody] SetStockDto dto, CancellationToken ct)
        => ToResult(await stock.SetAsync(menuItemId, locationId, dto, ct));
}
