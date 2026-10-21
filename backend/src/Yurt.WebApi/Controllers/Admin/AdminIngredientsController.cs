using Asp.Versioning;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Yurt.Application.Features.Stock;
using Yurt.Domain;
using Yurt.WebApi.Common;

namespace Yurt.WebApi.Controllers.Admin;

/// <summary>Ingredients, their counts per branch and the recipe of each menu item (own_pos).</summary>
[ApiController]
[ApiVersion("1.0")]
[Route("api/v{version:apiVersion}/admin/ingredients")]
[Authorize(Policy = "AdminOnly")]
[RequireModule(ModuleKeys.OwnPos)]
public class AdminIngredientsController(IngredientService ingredients) : ApiControllerBase
{
    [HttpGet]
    public async Task<IActionResult> List([FromQuery] Guid locationId, CancellationToken ct)
        => ToResult(await ingredients.ListAsync(locationId, ct));

    [HttpPost]
    public async Task<IActionResult> Create([FromBody] SaveIngredientDto dto, CancellationToken ct)
        => ToResult(await ingredients.CreateAsync(dto, ct));

    [HttpPut("{id:guid}")]
    public async Task<IActionResult> Update(Guid id, [FromBody] SaveIngredientDto dto, CancellationToken ct)
        => ToResult(await ingredients.UpdateAsync(id, dto, ct));

    [HttpDelete("{id:guid}")]
    public async Task<IActionResult> Delete(Guid id, CancellationToken ct)
        => ToResult(await ingredients.DeleteAsync(id, ct));

    [HttpPut("{id:guid}/stock/{locationId:guid}")]
    public async Task<IActionResult> SetStock(Guid id, Guid locationId, [FromBody] SetIngredientStockDto dto, CancellationToken ct)
        => ToResult(await ingredients.SetStockAsync(id, locationId, dto, ct));

    [HttpGet("recipes/{menuItemId:guid}")]
    public async Task<IActionResult> GetRecipe(Guid menuItemId, CancellationToken ct)
        => ToResult(await ingredients.GetRecipeAsync(menuItemId, ct));

    [HttpPut("recipes/{menuItemId:guid}")]
    public async Task<IActionResult> SaveRecipe(Guid menuItemId, [FromBody] SaveRecipeDto dto, CancellationToken ct)
        => ToResult(await ingredients.SaveRecipeAsync(menuItemId, dto, ct));

    [HttpGet("toppings/{toppingId:guid}")]
    public async Task<IActionResult> GetToppingUses(Guid toppingId, CancellationToken ct)
        => ToResult(await ingredients.GetToppingUsesAsync(toppingId, ct));

    [HttpPut("toppings/{toppingId:guid}")]
    public async Task<IActionResult> SaveToppingUses(Guid toppingId, [FromBody] SaveToppingUsesDto dto, CancellationToken ct)
        => ToResult(await ingredients.SaveToppingUsesAsync(toppingId, dto, ct));
}
