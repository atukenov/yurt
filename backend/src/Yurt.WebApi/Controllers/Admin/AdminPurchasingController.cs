using Asp.Versioning;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Yurt.Application.Features.Stock;
using Yurt.Domain;
using Yurt.WebApi.Common;

namespace Yurt.WebApi.Controllers.Admin;

/// <summary>
/// Suppliers, orders to suppliers, delivery invoices and the "what to order" list (own_pos).
/// Staff can type in and post deliveries and see the order list; cafe admins manage suppliers, cancel posted invoices
/// and mark them paid.
/// </summary>
[ApiController]
[ApiVersion("1.0")]
[Route("api/v{version:apiVersion}/admin/purchasing")]
[Authorize(Policy = "AdminOnly")]
[RequireModule(ModuleKeys.OwnPos)]
public class AdminPurchasingController(PurchasingService purchasing) : ApiControllerBase
{
    private bool IsCafeAdmin => User.IsInRole("Admin") || User.IsInRole("SuperAdmin");

    /// <summary>Suppliers with what they sell. Cafe admins also see what the cafe owes each one.</summary>
    [HttpGet("suppliers")]
    public async Task<IActionResult> Suppliers(CancellationToken ct)
        => Ok(await purchasing.ListSuppliersAsync(IsCafeAdmin, ct));

    [HttpPost("suppliers")]
    [Authorize(Policy = "AdminRoleAdmin")]
    public async Task<IActionResult> CreateSupplier([FromBody] SaveSupplierDto dto, CancellationToken ct)
        => ToResult(await purchasing.CreateSupplierAsync(dto, ct));

    [HttpPut("suppliers/{id:guid}")]
    [Authorize(Policy = "AdminRoleAdmin")]
    public async Task<IActionResult> UpdateSupplier(Guid id, [FromBody] SaveSupplierDto dto, CancellationToken ct)
        => ToResult(await purchasing.UpdateSupplierAsync(id, dto, ct));

    /// <summary>Deletes the supplier, or only switches it off when it has orders or invoices (result false).</summary>
    [HttpDelete("suppliers/{id:guid}")]
    [Authorize(Policy = "AdminRoleAdmin")]
    public async Task<IActionResult> DeleteSupplier(Guid id, CancellationToken ct)
        => ToResult(await purchasing.DeleteSupplierAsync(id, ct));

    /// <summary>Orders and invoices, newest first. <c>status</c> is a comma list of ordered, draft, posted, cancelled.</summary>
    [HttpGet("purchases")]
    public async Task<IActionResult> Purchases(
        [FromQuery] Guid? locationId, [FromQuery] Guid? supplierId, [FromQuery] string? status,
        [FromQuery] bool unpaid = false, [FromQuery] int take = 100, CancellationToken ct = default)
        => Ok(await purchasing.ListAsync(locationId, supplierId, status, unpaid, take, ct));

    [HttpGet("purchases/{id:guid}")]
    public async Task<IActionResult> Purchase(Guid id, CancellationToken ct)
        => ToResult(await purchasing.GetAsync(id, ct));

    [HttpPost("purchases")]
    public async Task<IActionResult> CreatePurchase([FromBody] SavePurchaseDto dto, CancellationToken ct)
        => ToResult(await purchasing.CreateAsync(dto, ct));

    [HttpPut("purchases/{id:guid}")]
    public async Task<IActionResult> UpdatePurchase(Guid id, [FromBody] SavePurchaseDto dto, CancellationToken ct)
        => ToResult(await purchasing.UpdateAsync(id, dto, ct));

    [HttpDelete("purchases/{id:guid}")]
    public async Task<IActionResult> DeletePurchase(Guid id, CancellationToken ct)
        => ToResult(await purchasing.DeleteAsync(id, ct));

    /// <summary>The goods arrived: put every line on the shelf and update the costs.</summary>
    [HttpPost("purchases/{id:guid}/post")]
    public async Task<IActionResult> Post(Guid id, CancellationToken ct)
        => ToResult(await purchasing.PostAsync(id, ct));

    /// <summary>Undo a posted invoice: take it back off the shelf.</summary>
    [HttpPost("purchases/{id:guid}/cancel")]
    [Authorize(Policy = "AdminRoleAdmin")]
    public async Task<IActionResult> Cancel(Guid id, CancellationToken ct)
        => ToResult(await purchasing.CancelAsync(id, ct));

    [HttpPut("purchases/{id:guid}/paid")]
    [Authorize(Policy = "AdminRoleAdmin")]
    public async Task<IActionResult> SetPaid(Guid id, [FromBody] SetPurchasePaidDto dto, CancellationToken ct)
        => ToResult(await purchasing.SetPaidAsync(id, dto, ct));

    /// <summary>What a branch should order now, grouped by supplier.</summary>
    [HttpGet("to-order")]
    public async Task<IActionResult> ToOrder([FromQuery] Guid locationId, CancellationToken ct)
        => ToResult(await purchasing.ToOrderAsync(locationId, ct));

    [HttpPut("order-up-to/{ingredientId:guid}/{locationId:guid}")]
    public async Task<IActionResult> SetOrderUpTo(Guid ingredientId, Guid locationId, [FromBody] SetOrderUpToDto dto, CancellationToken ct)
        => ToResult(await purchasing.SetOrderUpToAsync(ingredientId, locationId, dto, ct));
}
