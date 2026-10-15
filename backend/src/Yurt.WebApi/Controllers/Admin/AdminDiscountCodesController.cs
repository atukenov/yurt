using Asp.Versioning;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Yurt.Application.Features.DiscountCodes.DTOs;
using Yurt.Application.Features.DiscountCodes.Services;
using Yurt.WebApi.Common;

namespace Yurt.WebApi.Controllers.Admin;

[ApiController]
[ApiVersion("1.0")]
[Route("api/v{version:apiVersion}/admin/discount-codes")]
[Authorize(Policy = "AdminOnly")]
public class AdminDiscountCodesController : ApiControllerBase
{
    private readonly DiscountCodeService _service;

    public AdminDiscountCodesController(DiscountCodeService service)
        => _service = service;

    [HttpGet]
    public async Task<IActionResult> GetAll(CancellationToken ct)
        => Ok(await _service.GetAllAsync(ct));

    [HttpPost]
    public async Task<IActionResult> Create([FromBody] CreateDiscountCodeDto dto, CancellationToken ct)
        => ToResult(await _service.CreateAsync(dto, ct));

    [HttpPut("{id:guid}")]
    public async Task<IActionResult> Update(Guid id, [FromBody] UpdateDiscountCodeDto dto, CancellationToken ct)
        => ToResult(await _service.UpdateAsync(id, dto, ct));

    [HttpDelete("{id:guid}")]
    public async Task<IActionResult> Delete(Guid id, CancellationToken ct)
        => ToResult(await _service.DeleteAsync(id, ct));
}
