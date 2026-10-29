using Asp.Versioning;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Yurt.Application.Features.Tables;
using Yurt.Domain;
using Yurt.WebApi.Common;

namespace Yurt.WebApi.Controllers.Admin;

/// <summary>Tables of a branch and their QR codes (table_ordering).</summary>
[ApiController]
[ApiVersion("1.0")]
[Route("api/v{version:apiVersion}/admin/tables")]
[Authorize(Policy = "AdminOnly")]
[RequireModule(ModuleKeys.TableOrdering)]
public class AdminTablesController(TableService tables) : ApiControllerBase
{
    [HttpGet]
    public async Task<IActionResult> List([FromQuery] Guid locationId, CancellationToken ct) => ToResult(await tables.ListAsync(locationId, ct));

    [HttpPost]
    public async Task<IActionResult> Create([FromBody] CreateTablesDto dto, CancellationToken ct) => ToResult(await tables.CreateAsync(dto, ct));

    [HttpPut("{id:guid}")]
    public async Task<IActionResult> Rename(Guid id, [FromBody] RenameTableDto dto, CancellationToken ct)
        => ToResult(await tables.RenameAsync(id, dto, ct));

    [HttpPost("{id:guid}/new-code")]
    public async Task<IActionResult> NewCode(Guid id, CancellationToken ct) => ToResult(await tables.NewCodeAsync(id, ct));

    [HttpDelete("{id:guid}")]
    public async Task<IActionResult> Delete(Guid id, CancellationToken ct) => ToResult(await tables.DeleteAsync(id, ct));
}
