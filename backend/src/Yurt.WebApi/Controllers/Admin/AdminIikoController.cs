using Asp.Versioning;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Yurt.Application.Common.Interfaces;
using Yurt.Application.Features.Loyalty;
using Yurt.WebApi.Common;

namespace Yurt.WebApi.Controllers.Admin;

/// <summary>
/// Read-only reference lookups against iiko, backing the admin menu-mapping and
/// location-routing pickers, plus a one-time webhook registration action. No general
/// write endpoints here — the "paid in app" payment type stays an appsettings-configured
/// value (IikoOptions.PaymentTypeId), consistent with every other global iiko setting;
/// this controller just helps the operator find the right ID to put there.
/// </summary>
[ApiController]
[ApiVersion("1.0")]
[Route("api/v{version:apiVersion}/admin/iiko")]
[Authorize(Policy = "AdminOnly")]
public class AdminIikoController : ApiControllerBase
{
    private readonly IIikoApiClient _iiko;
    private readonly IikoOptions _options;

    public AdminIikoController(IIikoApiClient iiko, IikoOptions options)
    {
        _iiko = iiko;
        _options = options;
    }

    [HttpGet("nomenclature")]
    public async Task<IActionResult> GetNomenclature(CancellationToken ct)
        => Ok(await _iiko.GetNomenclatureAsync(ct));

    [HttpGet("payment-types")]
    public async Task<IActionResult> GetPaymentTypes(CancellationToken ct)
        => Ok(await _iiko.GetPaymentTypesAsync(ct));

    [HttpGet("terminal-groups")]
    public async Task<IActionResult> GetTerminalGroups(CancellationToken ct)
        => Ok(await _iiko.GetTerminalGroupsAsync(ct));

    /// <summary>
    /// Registers this backend's public URL with iiko as the delivery-order webhook
    /// target. Run once by an operator after deploying with a configured
    /// Iiko:WebhookAuthToken — not called automatically, since it needs the instance's
    /// real public URL and shouldn't silently re-run on every boot.
    /// </summary>
    [HttpPost("register-webhook")]
    public async Task<IActionResult> RegisterWebhook(
        [FromBody] RegisterWebhookRequest dto, CancellationToken ct)
    {
        if (string.IsNullOrWhiteSpace(_options.WebhookAuthToken))
            return ValidationError("Set Iiko:WebhookAuthToken in configuration before registering the webhook.");
        if (string.IsNullOrWhiteSpace(dto.WebhookUrl))
            return ValidationError("WebhookUrl is required.");

        await _iiko.RegisterWebhookAsync(dto.WebhookUrl, _options.WebhookAuthToken, ct);
        return Ok(new { message = "Webhook registered." });
    }

    public record RegisterWebhookRequest(string WebhookUrl);
}
