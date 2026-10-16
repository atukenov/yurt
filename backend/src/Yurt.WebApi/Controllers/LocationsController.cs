using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Asp.Versioning;
using Yurt.Application.Common.Interfaces;
using Yurt.Application.Features.Locations.Services;
using Yurt.Domain;
using Yurt.WebApi.Common;

namespace Yurt.WebApi.Controllers;

[ApiController]
[ApiVersion("1.0")]
[Route("api/v{version:apiVersion}/locations")]
public class LocationsController : ApiControllerBase
{
    private readonly LocationService _locationService;
    private readonly IModuleGate _modules;

    public LocationsController(LocationService locationService, IModuleGate modules)
    {
        _locationService = locationService;
        _modules = modules;
    }

    /// <summary>Get all active locations.</summary>
    [HttpGet]
    public async Task<IActionResult> GetActive(CancellationToken ct = default)
    {
        var locations = await _locationService.GetActiveLocationsAsync(ct);
        // Without multi_location only the first branch is offered; the others are kept, not deleted.
        if (!await _modules.IsEnabledAsync(ModuleKeys.MultiLocation, ct))
            locations = locations.Take(1).ToList();
        return Ok(locations);
    }

    /// <summary>Get a specific location by ID.</summary>
    [HttpGet("{id:guid}")]
    public async Task<IActionResult> GetById(Guid id, CancellationToken ct)
        => ToResult(await _locationService.GetByIdAsync(id, ct));
}
