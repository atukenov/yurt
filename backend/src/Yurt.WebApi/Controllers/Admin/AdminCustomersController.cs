using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Asp.Versioning;
using Yurt.Application.Features.Customers;
using Yurt.Application.Features.Loyalty.Services;
using Yurt.WebApi.Common;

namespace Yurt.WebApi.Controllers.Admin;

[ApiController]
[ApiVersion("1.0")]
[Route("api/v{version:apiVersion}/admin/customers")]
[Authorize(Policy = "AdminOnly")]
public class AdminCustomersController : ApiControllerBase
{
    private readonly CustomerService _customers;
    private readonly LoyaltyService _loyalty;

    public AdminCustomersController(CustomerService customers, LoyaltyService loyalty)
    {
        _customers = customers;
        _loyalty = loyalty;
    }

    [HttpGet]
    public async Task<IActionResult> GetCustomers(
        [FromQuery] string? phone,
        [FromQuery] int page = 1,
        [FromQuery] int pageSize = 50,
        CancellationToken ct = default)
    {
        if (page < 1) page = 1;
        if (pageSize is < 1 or > 100) pageSize = 50;
        var (total, items) = await _customers.GetCustomersAsync(phone, page, pageSize, ct);
        return Ok(new { total, page, pageSize, items });
    }

    [HttpGet("{id:guid}")]
    public async Task<IActionResult> GetCustomer(Guid id, CancellationToken ct)
    {
        var detail = await _customers.GetCustomerDetailAsync(id, ct);
        return detail is null ? NotFound() : Ok(detail);
    }

    /// <summary>Customer's iiko loyalty balance. Degrades gracefully when iiko is unavailable.</summary>
    [HttpGet("{id:guid}/loyalty")]
    public async Task<IActionResult> GetLoyalty(Guid id, CancellationToken ct)
        => Ok(await _loyalty.GetBalanceAsync(id, linkIfMissing: false, ct));

    [HttpPatch("{id:guid}/active")]
    public async Task<IActionResult> SetActive(Guid id, [FromBody] SetActiveDto dto, CancellationToken ct)
    {
        var result = await _customers.SetActiveAsync(id, dto.IsActive, ct);
        return ToResult(result);
    }
}
