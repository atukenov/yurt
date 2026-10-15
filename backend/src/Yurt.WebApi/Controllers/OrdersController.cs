using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Asp.Versioning;
using Yurt.Application.Common.Interfaces;
using Yurt.Application.Features.Orders.DTOs;
using Yurt.Application.Features.Orders.Services;
using Yurt.WebApi.Common;

namespace Yurt.WebApi.Controllers;

[ApiController]
[ApiVersion("1.0")]
[Route("api/v{version:apiVersion}/orders")]
[Authorize(Policy = "CustomerOnly")]
public class OrdersController : ApiControllerBase
{
    private readonly OrderService _orderService;
    private readonly ICurrentUserService _currentUser;

    public OrdersController(OrderService orderService, ICurrentUserService currentUser)
    {
        _orderService = orderService;
        _currentUser = currentUser;
    }

    /// <summary>Place a new order. Supply an Idempotency-Key header to safely retry.</summary>
    [HttpPost]
    public async Task<IActionResult> Create([FromBody] CreateOrderDto dto, CancellationToken ct)
    {
        if (dto.Items == null || dto.Items.Count == 0)
            return ValidationError("Order must contain at least one item.");

        var idempotencyKey = Request.Headers["Idempotency-Key"].FirstOrDefault();
        var effectiveDto = string.IsNullOrWhiteSpace(idempotencyKey) ? dto : dto with { IdempotencyKey = idempotencyKey };

        var result = await _orderService.CreateOrderAsync(_currentUser.UserId!.Value, effectiveDto, ct);
        return ToResult(result);
    }

    /// <summary>Get current active orders.</summary>
    [HttpGet("active")]
    public async Task<IActionResult> GetActive(CancellationToken ct)
        => Ok(await _orderService.GetActiveOrdersAsync(_currentUser.UserId!.Value, ct));

    /// <summary>Get completed order history.</summary>
    [HttpGet("history")]
    public async Task<IActionResult> GetHistory(CancellationToken ct)
        => Ok(await _orderService.GetHistoryAsync(_currentUser.UserId!.Value, ct));

    /// <summary>Get declined orders.</summary>
    [HttpGet("declined")]
    public async Task<IActionResult> GetDeclined(CancellationToken ct)
        => Ok(await _orderService.GetDeclinedOrdersAsync(_currentUser.UserId!.Value, ct));

    /// <summary>Get a single order (customer must own it).</summary>
    [HttpGet("{id:guid}")]
    public async Task<IActionResult> GetById(Guid id, CancellationToken ct)
        => ToResult(await _orderService.GetOrderAsync(id, _currentUser.UserId!.Value, ct));

    /// <summary>Cancel an order that has not been accepted yet.</summary>
    [HttpPost("{id:guid}/cancel")]
    public async Task<IActionResult> Cancel(Guid id, CancellationToken ct)
        => ToResult(await _orderService.CancelOrderAsync(id, _currentUser.UserId!.Value, ct));

    /// <summary>Rate a completed order (1-5 stars, optional comment). Once only.</summary>
    [HttpPost("{id:guid}/rating")]
    public async Task<IActionResult> Rate(Guid id, [FromBody] RateOrderDto dto, CancellationToken ct)
        => ToResult(await _orderService.RateOrderAsync(id, _currentUser.UserId!.Value, dto, ct));
}
