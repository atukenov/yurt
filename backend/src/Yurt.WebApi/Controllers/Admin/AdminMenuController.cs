using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Asp.Versioning;
using Yurt.Application.Features.Menu.DTOs;
using Yurt.Application.Features.Menu.Services;
using Yurt.WebApi.Common;

namespace Yurt.WebApi.Controllers.Admin;

[ApiController]
[ApiVersion("1.0")]
[Route("api/v{version:apiVersion}/admin/menu")]
[Authorize(Policy = "AdminOnly")]
public class AdminMenuController : ApiControllerBase
{
    private readonly MenuService _menuService;

    public AdminMenuController(MenuService menuService) => _menuService = menuService;

    // ── CATEGORIES ──────────────────────────────────────────────────────────────

    [HttpGet("categories")]
    public async Task<IActionResult> GetCategories(CancellationToken ct)
        => Ok(await _menuService.AdminGetCategoriesAsync(ct));

    [HttpPost("categories")]
    public async Task<IActionResult> CreateCategory(
        [FromBody] CreateCategoryDto dto, CancellationToken ct)
        => ToResult(await _menuService.CreateCategoryAsync(dto, ct));

    [HttpPut("categories/{id:guid}")]
    public async Task<IActionResult> UpdateCategory(
        Guid id, [FromBody] UpdateCategoryDto dto, CancellationToken ct)
        => ToResult(await _menuService.UpdateCategoryAsync(id, dto, ct));

    [HttpDelete("categories/{id:guid}")]
    public async Task<IActionResult> DeleteCategory(Guid id, CancellationToken ct)
        => ToResult(await _menuService.DeleteCategoryAsync(id, ct));

    [HttpPatch("categories/reorder")]
    public async Task<IActionResult> ReorderCategories(
        [FromBody] ReorderCategoriesDto dto, CancellationToken ct)
        => ToResult(await _menuService.ReorderCategoriesAsync(dto.OrderedIds, ct));

    // ── ITEMS ────────────────────────────────────────────────────────────────────

    [HttpGet("items")]
    public async Task<IActionResult> GetItems(CancellationToken ct)
        => Ok(await _menuService.AdminGetItemsAsync(ct));

    [HttpGet("items/{id:guid}")]
    public async Task<IActionResult> GetItem(Guid id, CancellationToken ct)
        => ToResult(await _menuService.GetItemByIdAsync(id, "en", null, ct));

    [HttpPost("items")]
    public async Task<IActionResult> CreateItem(
        [FromBody] CreateMenuItemDto dto, CancellationToken ct)
        => ToResult(await _menuService.CreateItemAsync(dto, ct));

    [HttpPut("items/{id:guid}")]
    public async Task<IActionResult> UpdateItem(
        Guid id, [FromBody] UpdateMenuItemDto dto, CancellationToken ct)
        => ToResult(await _menuService.UpdateItemAsync(id, dto, ct));

    [HttpDelete("items/{id:guid}")]
    public async Task<IActionResult> DeleteItem(Guid id, CancellationToken ct)
        => ToResult(await _menuService.DeleteItemAsync(id, ct));

    [HttpPatch("items/reorder")]
    public async Task<IActionResult> ReorderItems(
        [FromBody] ReorderMenuItemsDto dto, CancellationToken ct)
        => ToResult(await _menuService.ReorderItemsAsync(dto.CategoryId, dto.OrderedIds, ct));

    // ── TOPPINGS ─────────────────────────────────────────────────────────────────

    [HttpGet("toppings")]
    public async Task<IActionResult> GetToppings(CancellationToken ct)
        => Ok(await _menuService.AdminGetToppingsAsync(ct));

    [HttpPost("toppings")]
    public async Task<IActionResult> CreateTopping(
        [FromBody] CreateToppingDto dto, CancellationToken ct)
        => ToResult(await _menuService.CreateToppingAsync(dto, ct));

    [HttpPut("toppings/{id:guid}")]
    public async Task<IActionResult> UpdateTopping(
        Guid id, [FromBody] UpdateToppingDto dto, CancellationToken ct)
        => ToResult(await _menuService.UpdateToppingAsync(id, dto, ct));

    [HttpDelete("toppings/{id:guid}")]
    public async Task<IActionResult> DeleteTopping(Guid id, CancellationToken ct)
        => ToResult(await _menuService.DeleteToppingAsync(id, ct));
}
