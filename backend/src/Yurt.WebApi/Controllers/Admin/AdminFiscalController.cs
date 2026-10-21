using Asp.Versioning;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Yurt.Application.Features.Fiscal;
using Yurt.Domain;
using Yurt.WebApi.Common;

namespace Yurt.WebApi.Controllers.Admin;

/// <summary>Official tax receipts: the "not fiscalized" list, receipts of one order, and "try again" (own_pos).</summary>
[ApiController]
[ApiVersion("1.0")]
[Route("api/v{version:apiVersion}/admin/fiscal")]
[Authorize(Policy = "AdminOnly")]
[RequireModule(ModuleKeys.OwnPos)]
public class AdminFiscalController(FiscalReceiptService fiscal) : ApiControllerBase
{
    /// <summary>Receipts not sent yet, oldest first. No branch means all branches.</summary>
    [HttpGet("waiting")]
    public async Task<IActionResult> Waiting([FromQuery] Guid? locationId, CancellationToken ct)
        => ToResult(await fiscal.ListWaitingAsync(locationId, ct));

    [HttpGet("orders/{orderId:guid}")]
    public async Task<IActionResult> ForOrder(Guid orderId, CancellationToken ct)
        => ToResult(await fiscal.ListForOrderAsync(orderId, ct));

    [HttpPost("receipts/{id:guid}/retry")]
    public async Task<IActionResult> Retry(Guid id, CancellationToken ct)
        => ToResult(await fiscal.RetryAsync(id, ct));
}
