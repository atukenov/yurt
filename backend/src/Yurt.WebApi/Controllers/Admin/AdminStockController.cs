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
public class AdminStockController(StockService stock, StockMoveService moves) : ApiControllerBase
{
    [HttpGet]
    public async Task<IActionResult> List([FromQuery] Guid locationId, CancellationToken ct)
        => ToResult(await stock.ListAsync(locationId, ct));

    /// <summary>What is running low or out, in one branch or in all of them when no branch is given.</summary>
    [HttpGet("alerts")]
    public async Task<IActionResult> Alerts([FromQuery] Guid? locationId, CancellationToken ct)
        => Ok(await stock.AlertsAsync(locationId, ct));

    /// <summary>Hand changes to dish and ingredient counts in a branch, newest first: who changed what and when.</summary>
    [HttpGet("history")]
    public async Task<IActionResult> History(
        [FromQuery] Guid locationId, [FromQuery] int take = 100, [FromQuery] string? types = null, CancellationToken ct = default)
        => ToResult(await stock.HistoryAsync(locationId, take, types, ct));

    /// <summary>What a waiting order needs more of than its branch has, so staff are warned before accepting it.</summary>
    [HttpGet("order-check/{orderId:guid}")]
    public async Task<IActionResult> OrderCheck(Guid orderId, CancellationToken ct)
        => ToResult(await stock.OrderShortagesAsync(orderId, ct));

    /// <summary>A delivery arrived: add it to the shelf, optionally with what it cost.</summary>
    [HttpPost("restock")]
    public async Task<IActionResult> Restock([FromBody] RestockDto dto, CancellationToken ct)
        => ToResult(await moves.RestockAsync(dto, ct));

    /// <summary>Spilled, expired, a staff drink... Staff can do this, not only admins.</summary>
    [HttpPost("write-off")]
    public async Task<IActionResult> WriteOff([FromBody] WriteOffDto dto, CancellationToken ct)
        => ToResult(await moves.WriteOffAsync(dto, ct));

    /// <summary>Move stock from one branch to another. Staff can do this, not only admins.</summary>
    [HttpPost("transfer")]
    public async Task<IActionResult> Transfer([FromBody] TransferDto dto, CancellationToken ct)
        => ToResult(await moves.TransferAsync(dto, ct));

    [HttpPost("stocktake")]
    public async Task<IActionResult> Stocktake([FromBody] StocktakeDto dto, CancellationToken ct)
        => ToResult(await moves.StocktakeAsync(dto, ct));

    /// <summary>Write-offs totalled by reason (default: the last 30 days). Money figures, so cafe admins only.</summary>
    [HttpGet("write-offs")]
    [Authorize(Policy = "AdminRoleAdmin")]
    public async Task<IActionResult> WriteOffs(
        [FromQuery] Guid? locationId, [FromQuery] DateTime? from, [FromQuery] DateTime? to, CancellationToken ct)
        => Ok(await moves.WriteOffSummaryAsync(locationId, from, to, ct));

    /// <summary>Cost of one portion of each dish against its price. Cafe admins only.</summary>
    [HttpGet("food-cost")]
    [Authorize(Policy = "AdminRoleAdmin")]
    public async Task<IActionResult> FoodCost(CancellationToken ct)
        => Ok(await moves.FoodCostAsync(ct));

    [HttpPut("{menuItemId:guid}/{locationId:guid}")]
    public async Task<IActionResult> Set(Guid menuItemId, Guid locationId, [FromBody] SetStockDto dto, CancellationToken ct)
        => ToResult(await stock.SetAsync(menuItemId, locationId, dto, ct));
}
