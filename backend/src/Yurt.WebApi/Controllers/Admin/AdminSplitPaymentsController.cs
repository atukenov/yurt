using Asp.Versioning;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Yurt.Application.Features.Shifts;
using Yurt.Domain;
using Yurt.WebApi.Common;

namespace Yurt.WebApi.Controllers.Admin;

/// <summary>Pay one order in parts with different methods (own_pos).</summary>
[ApiController]
[ApiVersion("1.0")]
[Route("api/v{version:apiVersion}/admin/orders/{id:guid}/split-payment")]
[Authorize(Policy = "AdminOnly")]
[RequireModule(ModuleKeys.OwnPos)]
public class AdminSplitPaymentsController(SplitPaymentService payments) : ApiControllerBase
{
    [HttpGet]
    public async Task<IActionResult> Get(Guid id, CancellationToken ct)
        => ToResult(await payments.GetAsync(id, ct));

    [HttpPut]
    public async Task<IActionResult> Save(Guid id, [FromBody] SplitPaymentInputDto dto, CancellationToken ct)
        => ToResult(await payments.SaveAsync(id, dto, ct));
}
