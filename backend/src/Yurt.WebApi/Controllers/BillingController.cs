using Asp.Versioning;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Yurt.Application.Features.Billing;
using Yurt.WebApi.Common;

namespace Yurt.WebApi.Controllers;

/// <summary>Webhook of the platform owner's Stripe account (monthly cafe billing). One address for all cafes: the cafe
/// is found from the subscription, and the signature is checked with Billing:Stripe:WebhookSecret.</summary>
[ApiController]
[ApiVersion("1.0")]
[Route("api/v{version:apiVersion}/billing")]
public class BillingController(BillingService billing) : ApiControllerBase
{
    [HttpPost("stripe/webhook")]
    [AllowAnonymous]
    public async Task<IActionResult> StripeWebhook(CancellationToken ct)
    {
        using var reader = new StreamReader(Request.Body);
        var rawBody = await reader.ReadToEndAsync(ct);
        var headers = Request.Headers.ToDictionary(h => h.Key, h => h.Value.ToString(), StringComparer.OrdinalIgnoreCase);

        var result = await billing.HandleWebhookAsync(rawBody, headers, ct);
        return result.Succeeded
            ? Ok(new { received = true })
            : StatusCode(result.StatusCode, new { error = result.Error });
    }
}
