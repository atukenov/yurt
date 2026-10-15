using System.Text.Json;
using Asp.Versioning;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;
using Yurt.Application.Common.Interfaces;
using Yurt.Domain.Enums;
using Yurt.WebApi.Common;

namespace Yurt.WebApi.Controllers;

/// <summary>
/// Receives iiko's delivery-order webhooks. Audit-only in this phase — it never mutates
/// the customer-facing Order.Status (the admin panel stays the sole driver of that); it
/// only flags <see cref="IikoOrderSyncStatus.Failed"/> when iiko reports a cancellation
/// or error on a pushed order, so staff notice a mismatch instead of it going unnoticed.
/// </summary>
[ApiController]
[ApiVersion("1.0")]
[Route("api/v{version:apiVersion}/webhooks/iiko")]
public class IikoWebhookController : ApiControllerBase
{
    private static readonly JsonSerializerOptions JsonOptions = new(JsonSerializerDefaults.Web);

    private readonly IIikoWebhookValidator _validator;
    private readonly IApplicationDbContext _db;
    private readonly IAuditLogService _audit;
    private readonly ILogger<IikoWebhookController> _logger;

    public IikoWebhookController(
        IIikoWebhookValidator validator, IApplicationDbContext db, IAuditLogService audit,
        ILogger<IikoWebhookController> logger)
    {
        _validator = validator;
        _db = db;
        _audit = audit;
        _logger = logger;
    }

    [HttpPost]
    [AllowAnonymous]
    public async Task<IActionResult> Receive(CancellationToken ct)
    {
        var headers = Request.Headers.ToDictionary(h => h.Key, h => h.Value.ToString(), StringComparer.OrdinalIgnoreCase);
        if (!_validator.Validate(headers))
            return Unauthorized();

        using var reader = new StreamReader(Request.Body);
        var rawBody = await reader.ReadToEndAsync(ct);

        List<IikoWebhookEvent>? events;
        try
        {
            events = JsonSerializer.Deserialize<List<IikoWebhookEvent>>(rawBody, JsonOptions);
        }
        catch (JsonException ex)
        {
            _logger.LogWarning(ex, "Malformed iiko webhook payload");
            return Ok(); // ack anyway — iiko has no meaningful retry-on-4xx contract documented
        }

        foreach (var evt in events ?? [])
        {
            var iikoOrderId = evt.EventInfo?.Id;
            if (iikoOrderId == null) continue;

            var isProblem = evt.EventType == "DeliveryOrderError"
                || string.Equals(evt.EventInfo?.Order?.Status, "Cancelled", StringComparison.OrdinalIgnoreCase);
            if (!isProblem) continue;

            var order = await _db.Orders.FirstOrDefaultAsync(o => o.IikoDeliveryOrderId == iikoOrderId, ct);
            if (order == null) continue;

            order.IikoOrderSyncStatus = IikoOrderSyncStatus.Failed;
            await _db.SaveChangesAsync(ct);

            _logger.LogWarning(
                "iiko reported a problem with pushed order {OrderId} (iiko order {IikoOrderId}): {EventType}",
                order.Id, iikoOrderId, evt.EventType);
            await _audit.LogAsync("IikoOrderWebhookProblem", "Order", order.Id.ToString(), evt.EventType, ct);
        }

        return Ok();
    }

    private record IikoWebhookOrder(string? Status);
    private record IikoWebhookOrderInfo(Guid? Id, IikoWebhookOrder? Order);
    private record IikoWebhookEvent(string EventType, IikoWebhookOrderInfo? EventInfo);
}
