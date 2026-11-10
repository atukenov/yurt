using Asp.Versioning;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Yurt.Application.Features.StaffWatch;
using Yurt.Domain;
using Yurt.WebApi.Common;

namespace Yurt.WebApi.Controllers.Admin;

/// <summary>Staff watch (staff_watch): the camera box key, cameras and zones, shift plan, settings and the day's report.</summary>
[ApiController]
[ApiVersion("1.0")]
[Route("api/v{version:apiVersion}/admin/watch")]
[Authorize(Policy = "AdminOnly")]
[RequireModule(ModuleKeys.StaffWatch)]
public class AdminStaffWatchController(StaffWatchService watch) : ApiControllerBase
{
    [HttpGet("day")]
    public async Task<IActionResult> Day([FromQuery] Guid locationId, [FromQuery] string? day, CancellationToken ct)
        => ToResult(await watch.DayAsync(locationId, day, ct));

    [HttpGet("boxes")]
    public async Task<IActionResult> Boxes(CancellationToken ct) => Ok(await watch.BoxesAsync(ct));

    [HttpPost("boxes/{locationId:guid}")]
    [Authorize(Policy = "AdminRoleAdmin")]
    public async Task<IActionResult> NewKey(Guid locationId, CancellationToken ct) => ToResult(await watch.CreateKeyAsync(locationId, ct));

    [HttpDelete("boxes/{locationId:guid}")]
    [Authorize(Policy = "AdminRoleAdmin")]
    public async Task<IActionResult> RevokeKey(Guid locationId, CancellationToken ct) => ToResult(await watch.RevokeKeyAsync(locationId, ct));

    [HttpGet("cameras")]
    public async Task<IActionResult> Cameras([FromQuery] Guid locationId, CancellationToken ct) => Ok(await watch.CamerasAsync(locationId, ct));

    [HttpPost("cameras")]
    [Authorize(Policy = "AdminRoleAdmin")]
    public async Task<IActionResult> CreateCamera([FromBody] SaveWatchCameraDto dto, CancellationToken ct) => ToResult(await watch.CreateCameraAsync(dto, ct));

    [HttpPut("cameras/{id:guid}")]
    [Authorize(Policy = "AdminRoleAdmin")]
    public async Task<IActionResult> UpdateCamera(Guid id, [FromBody] SaveWatchCameraDto dto, CancellationToken ct)
        => ToResult(await watch.UpdateCameraAsync(id, dto, ct));

    [HttpDelete("cameras/{id:guid}")]
    [Authorize(Policy = "AdminRoleAdmin")]
    public async Task<IActionResult> DeleteCamera(Guid id, CancellationToken ct) => ToResult(await watch.DeleteCameraAsync(id, ct));

    [HttpPost("cameras/{id:guid}/snapshot-request")]
    public async Task<IActionResult> RequestSnapshot(Guid id, CancellationToken ct) => ToResult(await watch.RequestSnapshotAsync(id, ct));

    /// <summary>The camera's last still picture (heads blurred by the box).</summary>
    [HttpGet("cameras/{id:guid}/snapshot")]
    public async Task<IActionResult> Snapshot(Guid id, CancellationToken ct)
    {
        var jpeg = await watch.SnapshotAsync(id, ct);
        if (jpeg is null) return NotFound();
        Response.Headers.CacheControl = "no-store";
        return File(jpeg, "image/jpeg");
    }

    [HttpGet("settings")]
    public async Task<IActionResult> Settings([FromQuery] Guid locationId, CancellationToken ct) => Ok(await watch.SettingsAsync(locationId, ct));

    [HttpPut("settings")]
    [Authorize(Policy = "AdminRoleAdmin")]
    public async Task<IActionResult> SaveSettings([FromBody] SaveWatchSettingsDto dto, CancellationToken ct) => ToResult(await watch.SaveSettingsAsync(dto, ct));

    [HttpPut("telegram-token")]
    [Authorize(Policy = "AdminRoleAdmin")]
    public async Task<IActionResult> TelegramToken([FromBody] WatchTelegramTokenDto dto, CancellationToken ct)
        => ToResult(await watch.SetTelegramTokenAsync(dto, ct));

    [HttpPost("telegram/find-chat")]
    [Authorize(Policy = "AdminRoleAdmin")]
    public async Task<IActionResult> FindChat([FromQuery] Guid locationId, CancellationToken ct) => ToResult(await watch.FindTelegramChatAsync(locationId, ct));

    [HttpPost("telegram/test")]
    public async Task<IActionResult> TestTelegram([FromQuery] Guid locationId, CancellationToken ct) => ToResult(await watch.TestTelegramAsync(locationId, ct));

    [HttpGet("shifts")]
    public async Task<IActionResult> Shifts([FromQuery] Guid locationId, CancellationToken ct) => Ok(await watch.ShiftsAsync(locationId, ct));

    [HttpPut("shifts")]
    [Authorize(Policy = "AdminRoleAdmin")]
    public async Task<IActionResult> SaveShifts([FromBody] SaveWatchShiftsDto dto, CancellationToken ct) => ToResult(await watch.SaveShiftsAsync(dto, ct));
}

/// <summary>
/// Used by the camera box in the cafe (watch/watch.py). It signs in with its key in the <c>X-Watch-Key</c> header and
/// names its cafe with <c>X-Tenant</c> (staff_watch).
/// </summary>
[ApiController]
[ApiVersion("1.0")]
[Route("api/v{version:apiVersion}/watch-agent")]
[AllowAnonymous]
[RequireModule(ModuleKeys.StaffWatch)]
[RequestSizeLimit(4 * 1024 * 1024)]
public class WatchAgentController(StaffWatchService watch) : ApiControllerBase
{
    public const string KeyHeader = "X-Watch-Key";

    [HttpGet("config")]
    public async Task<IActionResult> Config([FromHeader(Name = KeyHeader)] string? key, [FromQuery] string? cameras, [FromQuery] string? version, CancellationToken ct)
        => ToResult(await watch.AgentConfigAsync(key, cameras, version, ct));

    [HttpPost("minutes")]
    public async Task<IActionResult> Minutes([FromHeader(Name = KeyHeader)] string? key, [FromBody] AgentMinutesDto dto, CancellationToken ct)
        => ToResult(await watch.AgentMinutesAsync(key, dto, ct));

    [HttpPost("alerts")]
    public async Task<IActionResult> Alerts([FromHeader(Name = KeyHeader)] string? key, [FromBody] AgentAlertsDto dto, CancellationToken ct)
        => ToResult(await watch.AgentAlertsAsync(key, dto, ct));

    [HttpPost("reports")]
    public async Task<IActionResult> Report([FromHeader(Name = KeyHeader)] string? key, [FromBody] AgentReportDto dto, CancellationToken ct)
        => ToResult(await watch.AgentReportAsync(key, dto, ct));

    [HttpPost("snapshots")]
    public async Task<IActionResult> Snapshot([FromHeader(Name = KeyHeader)] string? key, [FromBody] AgentSnapshotDto dto, CancellationToken ct)
        => ToResult(await watch.AgentSnapshotAsync(key, dto, ct));
}
