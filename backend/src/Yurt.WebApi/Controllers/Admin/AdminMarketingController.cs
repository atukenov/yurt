using Asp.Versioning;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Yurt.Application.Features.Marketing;
using Yurt.Domain;
using Yurt.WebApi.Common;

namespace Yurt.WebApi.Controllers.Admin;

/// <summary>Guest groups and guest messages (automatic and one-off pushes with personal promo codes).</summary>
[ApiController]
[ApiVersion("1.0")]
[Route("api/v{version:apiVersion}/admin/marketing")]
[Authorize(Policy = "AdminRoleAdmin")]
[RequireModule(ModuleKeys.Marketing)]
public class AdminMarketingController(MarketingService service) : ApiControllerBase
{
    [HttpGet("overview")]
    public async Task<IActionResult> Overview(CancellationToken ct)
        => Ok(await service.GetOverviewAsync(DateTime.UtcNow, ct));

    [HttpGet("segments/{key}")]
    public async Task<IActionResult> SegmentGuests(string key, CancellationToken ct)
        => ToResult(await service.GetSegmentGuestsAsync(key, DateTime.UtcNow, ct));

    [HttpGet("campaigns")]
    public async Task<IActionResult> Campaigns(CancellationToken ct)
        => Ok(await service.GetCampaignsAsync(ct));

    [HttpPost("campaigns")]
    public async Task<IActionResult> Create([FromBody] SaveCampaignDto dto, CancellationToken ct)
        => ToResult(await service.CreateAsync(dto, ct));

    [HttpPut("campaigns/{id:guid}")]
    public async Task<IActionResult> Update(Guid id, [FromBody] SaveCampaignDto dto, CancellationToken ct)
        => ToResult(await service.UpdateAsync(id, dto, ct));

    [HttpDelete("campaigns/{id:guid}")]
    public async Task<IActionResult> Delete(Guid id, CancellationToken ct)
        => ToResult(await service.DeleteAsync(id, ct));

    /// <summary>Sends a one-off message to its guest group now.</summary>
    [HttpPost("campaigns/{id:guid}/send")]
    public async Task<IActionResult> Send(Guid id, CancellationToken ct)
        => ToResult(await service.SendNowAsync(id, DateTime.UtcNow, ct));
}
