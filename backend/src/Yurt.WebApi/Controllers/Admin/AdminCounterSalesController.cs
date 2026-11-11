using Asp.Versioning;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Yurt.Application.Features.Orders.DTOs;
using Yurt.Application.Features.Orders.Services;
using Yurt.Domain;
using Yurt.WebApi.Common;

namespace Yurt.WebApi.Controllers.Admin;

/// <summary>Sales rung up at the counter for walk-in guests (Yurt POS, own_pos).</summary>
[ApiController]
[ApiVersion("1.0")]
[Route("api/v{version:apiVersion}/admin/counter-sales")]
[Authorize(Policy = "AdminOnly")]
[RequireModule(ModuleKeys.OwnPos)]
public class AdminCounterSalesController(CounterSaleService sales) : ApiControllerBase
{
    /// <summary>
    /// Ring up a paid sale. Send the same IdempotencyKey again to retry safely: the first sale comes back (200) instead
    /// of a second one (201).
    /// </summary>
    [HttpPost]
    [BlockWhenCafeLimited]
    public async Task<IActionResult> Create([FromBody] CreateCounterSaleDto dto, CancellationToken ct)
        => ToResult(await sales.CreateAsync(dto, ct));
}
