using Microsoft.AspNetCore.Mvc;
using Asp.Versioning;
using Yurt.Application.Features.AppUpdate;
using Yurt.WebApi.Common;

namespace Yurt.WebApi.Controllers;

[ApiController]
[ApiVersion("1.0")]
[Route("api/v{version:apiVersion}/app")]
public class AppController : ApiControllerBase
{
    private readonly AppReleaseService _releases;

    public AppController(AppReleaseService releases) => _releases = releases;

    /// <summary>
    /// Minimum required app version per platform, where to send the user to update, and the newest release's notes.
    /// Public — the client must be able to check this before any login screen renders.
    /// </summary>
    [HttpGet("update-info")]
    public async Task<IActionResult> GetUpdateInfo(CancellationToken ct)
        => Ok(await _releases.GetUpdateInfoAsync(ct));
}
