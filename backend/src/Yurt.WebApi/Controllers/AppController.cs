using Microsoft.AspNetCore.Mvc;
using Asp.Versioning;
using Yurt.Application.Common.Interfaces;
using Yurt.Application.Features.AppUpdate;
using Yurt.WebApi.Common;

namespace Yurt.WebApi.Controllers;

[ApiController]
[ApiVersion("1.0")]
[Route("api/v{version:apiVersion}/app")]
public class AppController : ApiControllerBase
{
    private readonly AppReleaseService _releases;
    private readonly IModuleGate _modules;

    public AppController(AppReleaseService releases, IModuleGate modules)
    {
        _releases = releases;
        _modules = modules;
    }

    /// <summary>
    /// Minimum required app version per platform, where to send the user to update, and the newest release's notes.
    /// Public — the client must be able to check this before any login screen renders.
    /// </summary>
    [HttpGet("update-info")]
    public async Task<IActionResult> GetUpdateInfo(CancellationToken ct)
        => Ok(await _releases.GetUpdateInfoAsync(ct));

    /// <summary>
    /// Runtime configuration for the apps: the keys of the enabled modules. Public (needed before login).
    /// A missing or failing lookup reports everything enabled, so old and new clients keep working.
    /// </summary>
    [HttpGet("config")]
    public async Task<IActionResult> GetConfig(CancellationToken ct)
    {
        var statuses = await _modules.GetStatusesAsync(ct);
        return Ok(new { modules = statuses.Where(s => s.IsEnabled).Select(s => s.Key).ToList() });
    }
}
