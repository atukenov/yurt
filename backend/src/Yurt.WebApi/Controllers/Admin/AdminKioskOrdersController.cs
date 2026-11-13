using Asp.Versioning;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Yurt.Application.Features.Orders.DTOs;
using Yurt.Application.Features.Orders.Services;
using Yurt.Domain;
using Yurt.WebApi.Common;

namespace Yurt.WebApi.Controllers.Admin;

/// <summary>Orders made by guests on the self-order kiosk tablet (module kiosk). The tablet signs in with a staff login.</summary>
[ApiController]
[ApiVersion("1.0")]
[Route("api/v{version:apiVersion}/admin/kiosk-orders")]
[Authorize(Policy = "AdminOnly")]
[RequireModule(ModuleKeys.Kiosk)]
public class AdminKioskOrdersController(CounterSaleService sales) : ApiControllerBase
{
    /// <summary>
    /// Send the guest's order to the cafe. It waits unpaid: the cashier takes the payment at the counter, then accepts it.
    /// Send the same IdempotencyKey again to retry safely: the first order comes back (200).
    /// </summary>
    [HttpPost]
    [BlockWhenCafeLimited]
    public async Task<IActionResult> Create([FromBody] CreateKioskOrderDto dto, CancellationToken ct)
        => ToResult(await sales.CreateKioskOrderAsync(dto, ct));
}
