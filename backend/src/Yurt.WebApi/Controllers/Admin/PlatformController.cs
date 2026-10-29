using Asp.Versioning;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Yurt.Application.Features.Billing;
using Yurt.Application.Common.Interfaces;
using Yurt.Application.Features.Fiscal;
using Yurt.Application.Features.Payments;
using Yurt.Application.Features.Payments.DTOs;
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
    private readonly CafeSecretsService _secrets;
    private readonly LoyaltyImportService _loyaltyImport;
    private readonly LoyaltyLedgerSwitchService _ledgerSwitch;
    private readonly IConfiguration _config;
    private readonly ISecretProtector _protector;

    public PlatformController(PlatformService platform, CafeProvisioningService cafes, CafeSecretsService secrets, LoyaltyImportService loyaltyImport, LoyaltyLedgerSwitchService ledgerSwitch, IConfiguration config, ISecretProtector protector)
    {
        _platform = platform;
        _cafes = cafes;
        _secrets = secrets;
        _loyaltyImport = loyaltyImport;
        _ledgerSwitch = ledgerSwitch;
        _config = config;
        _protector = protector;
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

    [HttpPost("loyalty/import")]
    public async Task<IActionResult> ImportLoyalty(CancellationToken ct)
        => ToResult(await _loyaltyImport.ImportAsync(ct));

    [HttpGet("loyalty/compare")]
    public async Task<IActionResult> CompareLoyalty(CancellationToken ct)
        => ToResult(await _loyaltyImport.CompareAsync(ct));

    [HttpGet("loyalty/ledger")]
    public async Task<IActionResult> GetLoyaltyLedger(CancellationToken ct)
        => ToResult(await _ledgerSwitch.GetAsync(ct));

    [HttpPut("loyalty/ledger")]
    public async Task<IActionResult> SetLoyaltyLedger([FromBody] SetLoyaltyLedgerDto dto, CancellationToken ct)
        => ToResult(await _ledgerSwitch.SetAsync(dto.Mode, ct));

    /// <summary>Official tax receipts of the chosen cafe: provider, skipped methods, VAT, register number per branch.</summary>
    [HttpGet("fiscal")]
    public async Task<IActionResult> GetFiscal([FromServices] FiscalSettingsService fiscal, CancellationToken ct)
        => ToResult(await fiscal.GetAsync(ct));

    [HttpPut("fiscal")]
    public async Task<IActionResult> SetFiscal([FromServices] FiscalSettingsService fiscal, [FromBody] SaveFiscalSettingsDto dto, CancellationToken ct)
        => ToResult(await fiscal.SaveAsync(dto, ct));

    [HttpGet("cafes")]
    public async Task<IActionResult> GetCafes(CancellationToken ct)
        => Ok(await _cafes.ListAsync(ct));

    [HttpPost("cafes")]
    public async Task<IActionResult> CreateCafe([FromBody] CreateCafeDto dto, CancellationToken ct)
        => ToResult(await _cafes.CreateAsync(dto, ct));

    [HttpPut("cafes/{id:guid}/active")]
    public async Task<IActionResult> SetCafeActive(Guid id, [FromBody] SetCafeActiveDto dto, CancellationToken ct)
        => ToResult(await _cafes.SetActiveAsync(id, dto.IsActive, ct));

    /// <summary>Limited mode of the chosen cafe (stopped paying): paid-until date and the hand switch.</summary>
    [HttpGet("access")]
    public async Task<IActionResult> GetAccess([FromServices] CafeAccessSettingsService access, CancellationToken ct)
        => ToResult(await access.GetAsync(ct));

    [HttpPut("access")]
    public async Task<IActionResult> SetAccess([FromServices] CafeAccessSettingsService access, [FromBody] SaveCafeAccessDto dto, CancellationToken ct)
        => ToResult(await access.SaveAsync(dto, ct));

    /// <summary>The cafe's online payment provider (Stripe or off) and the webhook address for its Stripe dashboard.</summary>
    [HttpGet("payments")]
    public async Task<IActionResult> GetPayments([FromServices] PaymentSettingsService payments, CancellationToken ct)
        => ToResult(await payments.GetAsync(ServerUrl(), ct));

    [HttpPut("payments")]
    public async Task<IActionResult> SetPayments([FromServices] PaymentSettingsService payments, [FromBody] SavePaymentSettingsDto dto, CancellationToken ct)
        => ToResult(await payments.SaveAsync(dto, ServerUrl(), ct));

    /// <summary>The cafe's monthly price on the platform owner's Stripe account, the modules it covers, and its status.</summary>
    [HttpGet("billing")]
    public async Task<IActionResult> GetBilling([FromServices] BillingService billing, CancellationToken ct)
        => Ok(await billing.GetPlatformAsync(ServerUrl(), ct));

    [HttpPut("billing")]
    public async Task<IActionResult> SetBilling([FromServices] BillingService billing, [FromBody] SaveBillingPlanDto dto, CancellationToken ct)
        => ToResult(await billing.SavePlanAsync(dto, ServerUrl(), ct));

    /// <summary>Public address of this server (Railway serves https; the forwarded headers carry it).</summary>
    private string ServerUrl() => _config["PublicApiUrl"] is { Length: > 0 } url ? url : $"{Request.Scheme}://{Request.Host}";

    [HttpGet("cafes/{id:guid}/secrets")]
    public async Task<IActionResult> GetCafeSecrets(Guid id, CancellationToken ct)
        => ToResult(await _secrets.ListAsync(id, ct));

    [HttpPut("cafes/{id:guid}/secrets/{key}")]
    public async Task<IActionResult> SetCafeSecret(Guid id, string key, [FromBody] SetCafeSecretDto dto, CancellationToken ct)
        => ToResult(await _secrets.SetAsync(id, key, dto.Value, ct));

    [HttpDelete("cafes/{id:guid}/secrets/{key}")]
    public async Task<IActionResult> ClearCafeSecret(Guid id, string key, CancellationToken ct)
        => ToResult(await _secrets.ClearAsync(id, key, ct));

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
            new IntegrationStatusDto("secrets_master_key", _protector.IsAvailable, true),
            new IntegrationStatusDto("payment_return_url", Set("Payment:CustomerAppUrl"), true),
            new IntegrationStatusDto("iiko", Set("Iiko:ApiKey") && Set("Iiko:ClientSecret"), On("Iiko:Enabled")),
            new IntegrationStatusDto("firebase_push", Set("Firebase:ServiceAccountJson"), On("Firebase:Enabled")),
            new IntegrationStatusDto("otp_whatsapp", Set("Otp:IdInstance") && Set("Otp:ApiTokenInstance"), On("Otp:Enabled")),
        });
    }
}
