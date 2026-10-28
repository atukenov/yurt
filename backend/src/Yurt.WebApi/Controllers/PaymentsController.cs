using System.Linq;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Asp.Versioning;
using Yurt.Application.Common.Interfaces;
using Yurt.Application.Features.Payments.DTOs;
using Yurt.Application.Features.Payments.Services;
using Yurt.Domain;
using Yurt.Domain.Enums;
using Yurt.WebApi.Common;

namespace Yurt.WebApi.Controllers;

[ApiController]
[ApiVersion("1.0")]
[Route("api/v{version:apiVersion}/payments")]
public class PaymentsController : ApiControllerBase
{
  private readonly PaymentService _paymentService;
  private readonly ICurrentUserService _currentUser;

  public PaymentsController(PaymentService paymentService, ICurrentUserService currentUser)
  {
    _paymentService = paymentService;
    _currentUser = currentUser;
  }

  /// <summary>Whether this cafe takes online payments ({ provider: "stripe" } or { provider: null }), so the app only
  /// shows the "pay online" choice when it works. Module off reads as null.</summary>
  [HttpGet("config")]
  [Authorize(Policy = "CustomerOnly")]
  public async Task<IActionResult> Config(CancellationToken ct)
    => Ok(await _paymentService.GetConfigAsync(ct));

  /// <summary>Opens the payment page for an order. Gated by <c>online_payments</c>; status and the webhook
  /// deliberately are not, so a payment already started can finish.</summary>
  [HttpPost("create")]
  [RequireModule(ModuleKeys.OnlinePayments)]
  [Authorize(Policy = "CustomerOnly")]
  public async Task<IActionResult> Create([FromBody] CreatePaymentDto dto, CancellationToken ct)
  {
    if (dto.OrderId == Guid.Empty)
      return ValidationError("OrderId is required.");

    if (_currentUser.UserId == null)
      return Unauthorized(new ProblemDetails { Title = "Unable to resolve current user." });

    var result = await _paymentService.CreatePaymentAsync(_currentUser.UserId.Value, dto, ct);
    return ToResult(result);
  }

  [HttpGet("status/{invoiceId}")]
  [Authorize(Policy = "CustomerOnly")]
  public async Task<IActionResult> Status(string invoiceId, CancellationToken ct)
  {
    if (string.IsNullOrWhiteSpace(invoiceId))
      return ValidationError("InvoiceId is required.");

    if (_currentUser.UserId == null)
      return Unauthorized(new ProblemDetails { Title = "Unable to resolve current user." });

    var result = await _paymentService.GetPaymentStatusAsync(_currentUser.UserId.Value, invoiceId, ct);
    return ToResult(result);
  }

  /// <summary>Stripe webhook of one cafe. Stripe cannot send our X-Tenant header, so the cafe is in the address
  /// (the Platform screen shows it); the signature is checked with that cafe's own signing secret.</summary>
  [HttpPost("stripe/webhook/{cafe}")]
  [AllowAnonymous]
  public async Task<IActionResult> StripeWebhook(
      string cafe, [FromServices] ITenantDirectory directory, [FromServices] ITenantContext tenant, CancellationToken ct)
  {
    var match = (await directory.GetActiveAsync(ct))
        .FirstOrDefault(t => string.Equals(t.Slug, cafe, StringComparison.OrdinalIgnoreCase));
    if (match == null)
      return NotFound(new { error = "Unknown cafe." });
    tenant.Set(match.Id, match.Slug);

    using var reader = new StreamReader(Request.Body);
    var rawBody = await reader.ReadToEndAsync(ct);

    var headers = Request.Headers.ToDictionary(h => h.Key, h => h.Value.ToString(), StringComparer.OrdinalIgnoreCase);
    var result = await _paymentService.ProcessWebhookAsync(PaymentProvider.Stripe, rawBody, headers, ct);
    if (result.Succeeded)
      return Ok(new { received = true });

    return StatusCode(result.StatusCode, new { error = result.Error });
  }
}
