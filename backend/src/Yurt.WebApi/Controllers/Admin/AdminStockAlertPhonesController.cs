using Asp.Versioning;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Yurt.Application.Features.Stock;
using Yurt.Domain;
using Yurt.WebApi.Common;

namespace Yurt.WebApi.Controllers.Admin;

/// <summary>Which phones get a push when stock runs low (own_pos). Anyone on staff can see the list; only admins change it.</summary>
[ApiController]
[ApiVersion("1.0")]
[Route("api/v{version:apiVersion}/admin/stock/alert-phones")]
[Authorize(Policy = "AdminOnly")]
[RequireModule(ModuleKeys.OwnPos)]
public class AdminStockAlertPhonesController(StockAlertPushService alerts) : ApiControllerBase
{
    [HttpGet]
    public async Task<IActionResult> Get(CancellationToken ct) => Ok(await alerts.GetPhonesAsync(ct));

    [HttpPut]
    [Authorize(Policy = "AdminRoleAdmin")]
    public async Task<IActionResult> Set([FromBody] SaveStockAlertPhonesDto dto, CancellationToken ct)
        => ToResult(await alerts.SetPhonesAsync(dto, ct));
}
