using Asp.Versioning;
using Microsoft.AspNetCore.Mvc;
using Yurt.Application.Features.Tables;
using Yurt.Domain;
using Yurt.WebApi.Common;

namespace Yurt.WebApi.Controllers;

/// <summary>Looks up the table behind a scanned QR code (table_ordering). Open to everyone: the guest may not be signed in yet.</summary>
[ApiController]
[ApiVersion("1.0")]
[Route("api/v{version:apiVersion}/tables")]
[RequireModule(ModuleKeys.TableOrdering)]
public class TablesController(TableService tables) : ApiControllerBase
{
    [HttpGet("{code}")]
    public async Task<IActionResult> Lookup(string code, CancellationToken ct) => ToResult(await tables.LookupAsync(code, ct));
}
