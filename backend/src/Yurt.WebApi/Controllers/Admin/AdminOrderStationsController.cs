using Asp.Versioning;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Yurt.Application.Features.Stations;
using Yurt.Domain;
using Yurt.WebApi.Common;

namespace Yurt.WebApi.Controllers.Admin;

/// <summary>A station (Kitchen, Bar, ...) marks its own part of an order ready, or takes that back (own_pos).</summary>
[ApiController]
[ApiVersion("1.0")]
[Route("api/v{version:apiVersion}/admin/orders/{id:guid}/stations/{stationId:guid}/ready")]
[Authorize(Policy = "AdminOnly")]
[RequireModule(ModuleKeys.OwnPos)]
public class AdminOrderStationsController(StationReadyService ready) : ApiControllerBase
{
    [HttpPost]
    public async Task<IActionResult> MarkReady(Guid id, Guid stationId, CancellationToken ct)
        => ToResult(await ready.SetReadyAsync(id, stationId, true, ct));

    [HttpDelete]
    public async Task<IActionResult> Undo(Guid id, Guid stationId, CancellationToken ct)
        => ToResult(await ready.SetReadyAsync(id, stationId, false, ct));
}
