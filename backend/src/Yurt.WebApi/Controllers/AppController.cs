using Microsoft.AspNetCore.Mvc;
using Asp.Versioning;
using Yurt.Application.Common.Interfaces;
using Yurt.Application.Features.AppUpdate;
using Yurt.Application.Features.Pictures;
using Yurt.Application.Features.Platform;
using Yurt.Domain;
using Yurt.WebApi.Common;

namespace Yurt.WebApi.Controllers;

[ApiController]
[ApiVersion("1.0")]
[Route("api/v{version:apiVersion}/app")]
public class AppController : ApiControllerBase
{
    private readonly AppReleaseService _releases;
    private readonly IModuleGate _modules;
    private readonly IMaintenanceGate _maintenance;
    private readonly PlatformService _platform;
    private readonly PictureService _pictures;

    public AppController(AppReleaseService releases, IModuleGate modules, IMaintenanceGate maintenance, PlatformService platform,
        PictureService pictures)
    {
        _pictures = pictures;
        _platform = platform;
        _releases = releases;
        _modules = modules;
        _maintenance = maintenance;
    }

    /// <summary>
    /// Minimum required app version per platform, where to send the user to update, and the newest release's notes.
    /// Public — the client must be able to check this before any login screen renders.
    /// </summary>
    [HttpGet("update-info")]
    public async Task<IActionResult> GetUpdateInfo(CancellationToken ct)
        => Ok(await _releases.GetUpdateInfoAsync(ct));

    /// <summary>
    /// Runtime configuration for the apps: the keys of the enabled modules and the maintenance flag. Public (needed before login).
    /// A missing or failing lookup reports everything enabled, so old and new clients keep working.
    /// </summary>
    [HttpGet("config")]
    public async Task<IActionResult> GetConfig(CancellationToken ct)
    {
        var statuses = await _modules.GetStatusesAsync(ct);
        var maintenance = await _maintenance.GetAsync(ct);
        // The saved brand applies only while the branding module is on; otherwise the Yurt default is served.
        var brand = BrandDto.Default;
        try
        {
            if (await _modules.IsEnabledAsync(ModuleKeys.Branding, ct))
                brand = await _platform.GetBrandAsync(ct);
        }
        catch (Exception) when (!ct.IsCancellationRequested)
        {
            // A broken brand lookup must never stop the apps from starting.
        }
        return Ok(new
        {
            modules = statuses.Where(s => s.IsEnabled).Select(s => s.Key).ToList(),
            maintenance = new { enabled = maintenance.Enabled, message = maintenance.Message },
            brand
        });
    }

    /// <summary>
    /// An uploaded picture (logo, menu photo, ...). Public, so the apps can show it before sign-in. The cafe is part of
    /// the link because an &lt;img&gt; request carries no cafe header; a picture of another cafe is "not found". Pictures
    /// never change (a new upload gets a new link), so browsers may keep them for a year.
    /// </summary>
    [HttpGet("pictures/{tenantId:guid}/{id:guid}")]
    public async Task<IActionResult> GetPicture(Guid tenantId, Guid id, CancellationToken ct)
    {
        var picture = await _pictures.GetAsync(tenantId, id, ct);
        if (picture is null) return NotFound();
        Response.Headers.CacheControl = "public, max-age=31536000, immutable";
        // The customer and admin apps run on other addresses than the API.
        Response.Headers["Cross-Origin-Resource-Policy"] = "cross-origin";
        return File(picture.Data, picture.ContentType);
    }
}
