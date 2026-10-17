using Asp.Versioning;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Yurt.Application.Features.Platform;
using Yurt.WebApi.Common;

namespace Yurt.WebApi.Controllers.Admin;

/// <summary>
/// Operator tools. Every endpoint here is enforced server-side for the SuperAdmin role; the admin app only
/// hides the menu entry.
/// </summary>
[ApiController]
[ApiVersion("1.0")]
[Route("api/v{version:apiVersion}/platform")]
[Authorize(Policy = "SuperAdminOnly")]
public class PlatformController : ApiControllerBase
{
    private readonly PlatformService _platform;
    private readonly CafeProvisioningService _cafes;
    private readonly IConfiguration _config;

    public PlatformController(PlatformService platform, CafeProvisioningService cafes, IConfiguration config)
    {
        _platform = platform;
        _cafes = cafes;
        _config = config;
    }

    [HttpGet("modules")]
    public async Task<IActionResult> GetModules(CancellationToken ct)
        => Ok(await _platform.GetModulesAsync(ct));

    [HttpPut("modules/{key}")]
    public async Task<IActionResult> UpdateModule(string key, [FromBody] UpdateModuleLicenseDto dto, CancellationToken ct)
        => ToResult(await _platform.UpdateModuleAsync(key, dto, ct));

    [HttpGet("maintenance")]
    public async Task<IActionResult> GetMaintenance(CancellationToken ct)
        => Ok(await _platform.GetMaintenanceAsync(ct));

    [HttpPut("maintenance")]
    public async Task<IActionResult> SetMaintenance([FromBody] MaintenanceDto dto, CancellationToken ct)
        => ToResult(await _platform.SetMaintenanceAsync(dto, ct));

    [HttpGet("brand")]
    public async Task<IActionResult> GetBrand(CancellationToken ct)
        => Ok(await _platform.GetBrandAsync(ct));

    [HttpPut("brand")]
    public async Task<IActionResult> SetBrand([FromBody] BrandDto dto, CancellationToken ct)
        => ToResult(await _platform.SetBrandAsync(dto, ct));

    [HttpGet("cafes")]
    public async Task<IActionResult> GetCafes(CancellationToken ct)
        => Ok(await _cafes.ListAsync(ct));

    [HttpPost("cafes")]
    public async Task<IActionResult> CreateCafe([FromBody] CreateCafeDto dto, CancellationToken ct)
        => ToResult(await _cafes.CreateAsync(dto, ct));

    [HttpPut("cafes/{id:guid}/active")]
    public async Task<IActionResult> SetCafeActive(Guid id, [FromBody] SetCafeActiveDto dto, CancellationToken ct)
        => ToResult(await _cafes.SetActiveAsync(id, dto.IsActive, ct));

    /// <summary>Whether each integration has its secrets configured. Never returns the values.</summary>
    [HttpGet("integrations")]
    public IActionResult GetIntegrations()
    {
        bool Set(string key)
        {
            var v = _config[key];
            return !string.IsNullOrWhiteSpace(v) && !v.StartsWith("CHANGE_THIS", StringComparison.OrdinalIgnoreCase);
        }
        bool On(string key) => _config.GetValue<bool>(key);

        return Ok(new[]
        {
            new IntegrationStatusDto("jwt", Set("Jwt:Secret"), true),
            new IntegrationStatusDto("iiko", Set("Iiko:ApiKey") && Set("Iiko:ClientSecret"), On("Iiko:Enabled")),
            new IntegrationStatusDto("payments_sandbox", Set("Payment:WebhookSecret") && Set("Payment:SandboxSecret"), true),
            new IntegrationStatusDto("freedompay", true, On("FreedomPay:Enabled")),
            new IntegrationStatusDto("firebase_push", Set("Firebase:ServiceAccountJson"), On("Firebase:Enabled")),
            new IntegrationStatusDto("otp_whatsapp", Set("Otp:IdInstance") && Set("Otp:ApiTokenInstance"), On("Otp:Enabled")),
        });
    }
}
