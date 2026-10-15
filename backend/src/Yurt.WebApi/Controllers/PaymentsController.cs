using System.Linq;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Asp.Versioning;
using Yurt.Application.Common.Interfaces;
using Yurt.Application.Features.Payments.DTOs;
using Yurt.Application.Features.Payments.Services;
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

  [HttpPost("create")]
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

    var result = await _paymentService.GetPaymentStatusAsync(invoiceId, ct);
    return ToResult(result);
  }

  /// <summary>Apple Pay feature flag + Apple Merchant ID, fetched at runtime so the app
  /// doesn't hardcode either and ops can kill the feature without a release.</summary>
  [HttpGet("apple-pay/config")]
  [Authorize(Policy = "CustomerOnly")]
  public IActionResult ApplePayConfig()
  {
    return Ok(_paymentService.GetApplePayConfig());
  }

  /// <summary>Confirms an Apple Pay payment after the native Freedom Pay SDK flow succeeds
  /// on-device. See <see cref="Services.PaymentService.ConfirmApplePayAsync"/> for the
  /// trust-model caveat (no independent Freedom Pay verification yet).</summary>
  [HttpPost("apple-pay/confirm")]
  [Authorize(Policy = "CustomerOnly")]
  public async Task<IActionResult> ConfirmApplePay([FromBody] ConfirmApplePayDto dto, CancellationToken ct)
  {
    if (string.IsNullOrWhiteSpace(dto.InvoiceId) || string.IsNullOrWhiteSpace(dto.FreedomPaymentId))
      return ValidationError("InvoiceId and FreedomPaymentId are required.");

    if (_currentUser.UserId == null)
      return Unauthorized(new ProblemDetails { Title = "Unable to resolve current user." });

    var result = await _paymentService.ConfirmApplePayAsync(_currentUser.UserId.Value, dto, ct);
    return ToResult(result);
  }

  [HttpPost("webhook")]
  [AllowAnonymous]
  public async Task<IActionResult> Webhook(CancellationToken ct)
  {
    using var reader = new StreamReader(Request.Body);
    var rawBody = await reader.ReadToEndAsync(ct);

    var headers = Request.Headers.ToDictionary(h => h.Key, h => h.Value.ToString(), StringComparer.OrdinalIgnoreCase);
    var result = await _paymentService.ProcessWebhookAsync(rawBody, headers, ct);
    if (result.Succeeded)
      return Ok(new { message = "Webhook processed" });

    return StatusCode(result.StatusCode, new { error = result.Error });
  }
}
