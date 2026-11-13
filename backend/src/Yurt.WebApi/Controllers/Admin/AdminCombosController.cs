using Asp.Versioning;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Yurt.Application.Features.Combos;
using Yurt.Domain;
using Yurt.WebApi.Common;

namespace Yurt.WebApi.Controllers.Admin;

/// <summary>
/// Combos: dishes sold together at one set price. Staff who manage the menu make and change them; the money report is
/// for the CEO only (module <c>analytics</c>), like the other profit reports.
/// </summary>
[ApiController]
[ApiVersion("1.0")]
[Route("api/v{version:apiVersion}/admin/combos")]
[Authorize(Policy = "AdminOnly")]
public class AdminCombosController(ComboService combos) : ApiControllerBase
{
    [HttpGet]
    public async Task<IActionResult> List(CancellationToken ct) => Ok(await combos.ListAsync(ct));

    [HttpPost]
    public async Task<IActionResult> Create([FromBody] SaveComboDto dto, CancellationToken ct)
        => ToResult(await combos.CreateAsync(dto, ct));

    [HttpPut("{id:guid}")]
    public async Task<IActionResult> Update(Guid id, [FromBody] SaveComboDto dto, CancellationToken ct)
        => ToResult(await combos.UpdateAsync(id, dto, ct));

    /// <summary>Removes a combo. <c>true</c> = gone; <c>false</c> = switched off only, because old orders still show it.</summary>
    [HttpDelete("{id:guid}")]
    public async Task<IActionResult> Delete(Guid id, CancellationToken ct)
        => ToResult(await combos.DeleteAsync(id, ct));

    /// <summary>What combos sold in a month (<c>yyyy-MM</c>), what guests saved and what is left after the dishes.</summary>
    [HttpGet("report")]
    [Authorize(Policy = "CeoOnly")]
    [RequireModule(ModuleKeys.Analytics)]
    public async Task<IActionResult> Report([FromQuery] string month, [FromQuery] Guid? locationId, CancellationToken ct)
        => ToResult(await combos.ReportAsync(month, locationId, ct));
}
