using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Asp.Versioning;
using Yurt.Application.Features.Promotions.DTOs;
using Yurt.Application.Features.Promotions.Services;
using Yurt.WebApi.Common;

namespace Yurt.WebApi.Controllers.Admin;

[ApiController]
[ApiVersion("1.0")]
[Route("api/v{version:apiVersion}/admin/promotions")]
[Authorize(Policy = "AdminOnly")]
public class AdminPromotionsController : ApiControllerBase
{
    private readonly PromotionService _promotionService;

    public AdminPromotionsController(PromotionService promotionService)
        => _promotionService = promotionService;

    [HttpGet]
    public async Task<IActionResult> GetAll(CancellationToken ct)
        => Ok(await _promotionService.GetAllPromotionsAsync(ct));

    [HttpPost]
    public async Task<IActionResult> Create([FromBody] CreatePromotionDto dto, CancellationToken ct)
        => ToResult(await _promotionService.CreateAsync(dto, ct));

    [HttpPut("{id:guid}")]
    public async Task<IActionResult> Update(
        Guid id, [FromBody] UpdatePromotionDto dto, CancellationToken ct)
        => ToResult(await _promotionService.UpdateAsync(id, dto, ct));

    [HttpDelete("{id:guid}")]
    public async Task<IActionResult> Delete(Guid id, CancellationToken ct)
        => ToResult(await _promotionService.DeleteAsync(id, ct));
}
