using Asp.Versioning;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Yurt.Application.Features.Accounting;
using Yurt.Domain;
using Yurt.WebApi.Common;

namespace Yurt.WebApi.Controllers.Admin;

/// <summary>
/// Files for the cafe's accountant (module <c>analytics</c>, CEO only): one Excel file with sales by day, orders, dishes
/// sold, shifts, deliveries and write-offs, or the same period as a 1C exchange file. <c>from</c> and <c>to</c> are days
/// in the cafe's local time (<c>yyyy-MM-dd</c>, both included).
/// </summary>
[ApiController]
[ApiVersion("1.0")]
[Route("api/v{version:apiVersion}/admin/accounting")]
[Authorize(Policy = "CeoOnly")]
[RequireModule(ModuleKeys.Analytics)]
public class AdminAccountingController(AccountingExportService accounting) : ApiControllerBase
{
    private const string Xlsx = "application/vnd.openxmlformats-officedocument.spreadsheetml.sheet";

    [HttpGet("excel")]
    public async Task<IActionResult> Excel(
        [FromQuery] string? from, [FromQuery] string? to, [FromQuery] Guid? locationId, [FromQuery] string? lang, CancellationToken ct)
    {
        var result = await accounting.BuildAsync(from, to, locationId, "excel", ct);
        if (!result.Succeeded) return ToResult(result);
        return File(AccountingSpreadsheet.Write(result.Data!, AccountingSpreadsheet.Lang(lang)), Xlsx, $"accounting-{from}-{to}.xlsx");
    }

    [HttpGet("1c")]
    public async Task<IActionResult> OneC([FromQuery] string? from, [FromQuery] string? to, [FromQuery] Guid? locationId, CancellationToken ct)
    {
        var result = await accounting.BuildAsync(from, to, locationId, "1c", ct);
        if (!result.Succeeded) return ToResult(result);
        var xml = OneCExchangeWriter.Write(result.Data!, AccountingExportService.ToLocal(DateTime.UtcNow));
        return File(xml, "application/xml", $"1c-{from}-{to}.xml");
    }
}
