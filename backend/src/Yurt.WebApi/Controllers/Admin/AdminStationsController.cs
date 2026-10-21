using Asp.Versioning;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Yurt.Application.Features.Stations;
using Yurt.Domain;
using Yurt.WebApi.Common;

namespace Yurt.WebApi.Controllers.Admin;

/// <summary>Stations (Kitchen, Bar, ...) and routing of menu categories to them (own_pos).</summary>
[ApiController]
[ApiVersion("1.0")]
[Route("api/v{version:apiVersion}/admin/stations")]
[Authorize(Policy = "AdminOnly")]
[RequireModule(ModuleKeys.OwnPos)]
public class AdminStationsController(StationService stations) : ApiControllerBase
{
    [HttpGet("routing")]
    public async Task<IActionResult> Routing(CancellationToken ct) => ToResult(await stations.RoutingAsync(ct));

    [HttpPost]
    public async Task<IActionResult> Create([FromBody] SaveStationDto dto, CancellationToken ct)
        => ToResult(await stations.CreateAsync(dto, ct));

    [HttpDelete("{id:guid}")]
    public async Task<IActionResult> Delete(Guid id, CancellationToken ct) => ToResult(await stations.DeleteAsync(id, ct));

    [HttpPut("categories/{categoryId:guid}")]
    public async Task<IActionResult> SetCategory(Guid categoryId, [FromBody] SetCategoryStationDto dto, CancellationToken ct)
        => ToResult(await stations.SetCategoryStationAsync(categoryId, dto, ct));
}
