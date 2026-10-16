using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Asp.Versioning;
using Yurt.Application.Common.Interfaces;
using Yurt.Application.Features.Locations.DTOs;
using Yurt.Application.Features.Locations.Services;
using Yurt.Domain;
using Yurt.WebApi.Common;

namespace Yurt.WebApi.Controllers.Admin;

[ApiController]
[ApiVersion("1.0")]
[Route("api/v{version:apiVersion}/admin/locations")]
[Authorize(Policy = "AdminOnly")]
public class AdminLocationsController : ApiControllerBase
{
    private readonly LocationService _locationService;
    private readonly IModuleGate _modules;

    public AdminLocationsController(LocationService locationService, IModuleGate modules)
    {
        _locationService = locationService;
        _modules = modules;
    }

    [HttpGet]
    public async Task<IActionResult> GetAll(CancellationToken ct)
        => Ok(await _locationService.GetAllLocationsAsync(ct));

    [HttpGet("{id:guid}")]
    public async Task<IActionResult> GetById(Guid id, CancellationToken ct)
        => ToResult(await _locationService.GetByIdAsync(id, ct));

    [HttpPost]
    public async Task<IActionResult> Create([FromBody] CreateLocationDto dto, CancellationToken ct)
    {
        // Core includes one location; more need the multi_location module.
        if (!await _modules.IsEnabledAsync(ModuleKeys.MultiLocation, ct)
            && (await _locationService.GetAllLocationsAsync(ct)).Count >= 1)
        {
            return StatusCode(StatusCodes.Status403Forbidden,
                new { code = "module_disabled", module = ModuleKeys.MultiLocation });
        }
        return ToResult(await _locationService.CreateAsync(dto, ct));
    }

    [HttpPut("{id:guid}")]
    public async Task<IActionResult> Update(
        Guid id, [FromBody] UpdateLocationDto dto, CancellationToken ct)
        => ToResult(await _locationService.UpdateAsync(id, dto, ct));

    [HttpDelete("{id:guid}")]
    public async Task<IActionResult> Delete(Guid id, CancellationToken ct)
        => ToResult(await _locationService.DeleteAsync(id, ct));
}
