using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Asp.Versioning;
using Yurt.Application.Features.AppUpdate;
using Yurt.WebApi.Common;

namespace Yurt.WebApi.Controllers.Admin;

[ApiController]
[ApiVersion("1.0")]
[Route("api/v{version:apiVersion}/admin/app-releases")]
[Authorize(Policy = "AdminOnly")]
public class AdminAppReleasesController : ApiControllerBase
{
    private readonly AppReleaseService _releases;

    public AdminAppReleasesController(AppReleaseService releases) => _releases = releases;

    [HttpGet]
    public async Task<IActionResult> GetAll(CancellationToken ct)
        => Ok(await _releases.GetAllAsync(ct));

    [HttpPost]
    public async Task<IActionResult> Create([FromBody] SaveAppReleaseDto dto, CancellationToken ct)
        => ToResult(await _releases.CreateAsync(dto, ct));

    [HttpPut("{id:guid}")]
    public async Task<IActionResult> Update(Guid id, [FromBody] SaveAppReleaseDto dto, CancellationToken ct)
        => ToResult(await _releases.UpdateAsync(id, dto, ct));

    [HttpDelete("{id:guid}")]
    public async Task<IActionResult> Delete(Guid id, CancellationToken ct)
        => ToResult(await _releases.DeleteAsync(id, ct));
}
