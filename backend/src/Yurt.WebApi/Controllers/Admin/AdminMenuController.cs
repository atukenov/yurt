using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Asp.Versioning;
using Yurt.Application.Features.Menu.DTOs;
using Yurt.Application.Features.Menu.Import;
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
    private readonly MenuImportService _import;

    public AdminMenuController(MenuService menuService, MenuImportService import)
    {
        _menuService = menuService;
        _import = import;
    }

    // ── IMPORT FROM EXCEL ────────────────────────────────────────────────────────

    /// <summary>
    /// Checks a menu spreadsheet (.xlsx or .csv) and, with <c>apply=true</c>, saves it when it has no problems.
    /// Problems come back by row with a code the admin app translates; nothing is saved while there are any.
    /// </summary>
    [HttpPost("import")]
    [RequestSizeLimit(MenuImportService.MaxFileBytes + 64 * 1024)]
    public async Task<IActionResult> Import(IFormFile? file, [FromQuery] bool apply, CancellationToken ct)
    {
        if (file is null || file.Length == 0)
            return BadRequest(new ProblemDetails { Title = "Choose a file to upload." });
        if (file.Length > MenuImportService.MaxFileBytes)
            return BadRequest(new ProblemDetails { Title = "The file is larger than 5 MB." });

        // The zip reader needs to seek, so the upload is copied to memory (at most 5 MB).
        using var ms = new MemoryStream();
        await file.CopyToAsync(ms, ct);
        ms.Position = 0;
        return Ok(await _import.ImportAsync(ms, file.FileName, apply, ct));
    }

    /// <summary>An empty menu spreadsheet with the column names in the given language and a few sample rows.</summary>
    [HttpGet("import/template")]
    public IActionResult ImportTemplate([FromQuery] string? lang)
    {
        var l = lang is "ru" or "kk" ? lang : "en";
        return File(MenuImportService.Template(l),
            "application/vnd.openxmlformats-officedocument.spreadsheetml.sheet", $"menu-template-{l}.xlsx");
    }

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
