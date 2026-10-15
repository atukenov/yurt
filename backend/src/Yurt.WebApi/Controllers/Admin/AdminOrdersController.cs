using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Asp.Versioning;
using Yurt.Application.Features.Orders.DTOs;
using Yurt.Application.Features.Orders.Services;
using Yurt.Domain.Enums;
using Yurt.WebApi.Common;

namespace Yurt.WebApi.Controllers.Admin;

[ApiController]
[ApiVersion("1.0")]
[Route("api/v{version:apiVersion}/admin/orders")]
[Authorize(Policy = "AdminOnly")]
public class AdminOrdersController : ApiControllerBase
{
    private readonly OrderService _orderService;

    public AdminOrdersController(OrderService orderService) => _orderService = orderService;

    /// <summary>Get orders with optional filters, pagination, and search.</summary>
    [HttpGet]
    public async Task<IActionResult> GetOrders(
        [FromQuery] OrderStatus? status,
        [FromQuery] Guid? locationId,
        [FromQuery] string? search,
        [FromQuery] int page = 1,
        [FromQuery] int pageSize = 100,
        CancellationToken ct = default)
    {
        if (page < 1) page = 1;
        if (pageSize is < 1 or > 100) pageSize = 100;
        var (total, items) = await _orderService.GetAdminOrdersAsync(
            status, locationId, search, page, pageSize, ct);
        return Ok(new { total, page, pageSize, items });
    }

    /// <summary>Get a single order by ID.</summary>
    [HttpGet("{id:guid}")]
    public async Task<IActionResult> GetById(Guid id, CancellationToken ct)
        => ToResult(await _orderService.GetOrderForAdminAsync(id, ct));

    /// <summary>Accept an order with ETA.</summary>
    [HttpPost("{id:guid}/accept")]
    public async Task<IActionResult> Accept(
        Guid id, [FromBody] AcceptOrderDto dto, CancellationToken ct)
    {
        if (dto.EtaMinutes <= 0)
            return ValidationError("ETA minutes must be positive.");
        return ToResult(await _orderService.AcceptOrderAsync(id, dto, ct));
    }

    /// <summary>Decline an order with reason.</summary>
    [HttpPost("{id:guid}/decline")]
    public async Task<IActionResult> Decline(
        Guid id, [FromBody] DeclineOrderDto dto, CancellationToken ct)
    {
        if (string.IsNullOrWhiteSpace(dto.Reason))
            return ValidationError("Decline reason is required.");
        return ToResult(await _orderService.DeclineOrderAsync(id, dto, ct));
    }

    /// <summary>Update order status (Preparing → Ready → Completed).</summary>
    [HttpPost("{id:guid}/status")]
    public async Task<IActionResult> UpdateStatus(
        Guid id, [FromBody] UpdateOrderStatusDto dto, CancellationToken ct)
        => ToResult(await _orderService.UpdateStatusAsync(id, dto, ct));

    /// <summary>Update payment status and method.</summary>
    [HttpPost("{id:guid}/payment")]
    public async Task<IActionResult> UpdatePayment(
        Guid id, [FromBody] UpdatePaymentDto dto, CancellationToken ct)
        => ToResult(await _orderService.UpdatePaymentAsync(id, dto, ct));
}
