using Asp.Versioning;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Yurt.Application.Features.Profit;
using Yurt.Domain;
using Yurt.WebApi.Common;

namespace Yurt.WebApi.Controllers.Admin;

/// <summary>
/// Profit reports (module <c>analytics</c>): what each dish earns after its ingredients, the month's profit, and the
/// monthly costs typed in by the CEO. CEO only. The month's profit includes wages, so like payroll it is hidden from the
/// platform operator too.
/// </summary>
[ApiController]
[ApiVersion("1.0")]
[Route("api/v{version:apiVersion}/admin/profit")]
[Authorize(Policy = "CeoOnly")]
[RequireModule(ModuleKeys.Analytics)]
public class AdminProfitController(ProfitService profit) : ApiControllerBase
{
    private const string Xlsx = "application/vnd.openxmlformats-officedocument.spreadsheetml.sheet";

    /// <summary>Every dish and size sold in the month (<c>yyyy-MM</c>), most profit first.</summary>
    [HttpGet("items")]
    public async Task<IActionResult> Items([FromQuery] string month, [FromQuery] Guid? locationId, CancellationToken ct)
        => ToResult(await profit.ItemsAsync(month, locationId, ct));

    [HttpGet("items/export")]
    public async Task<IActionResult> ItemsExport([FromQuery] string month, [FromQuery] Guid? locationId, [FromQuery] string? lang, CancellationToken ct)
    {
        var result = await profit.ItemsAsync(month, locationId, ct);
        if (!result.Succeeded) return ToResult(result);
        var l = ProfitSpreadsheet.Lang(lang);
        return File(ProfitSpreadsheet.Items(result.Data!, l), Xlsx, $"profit-by-dish-{month}.xlsx");
    }

    /// <summary>Menu analysis: stars, plowhorses, puzzles and dogs, ABC class, keep / push / fix / drop.</summary>
    [HttpGet("menu-analysis")]
    public async Task<IActionResult> MenuAnalysis([FromQuery] string month, [FromQuery] Guid? locationId, CancellationToken ct)
        => ToResult(await profit.MenuAnalysisAsync(month, locationId, ct));

    [HttpGet("menu-analysis/export")]
    public async Task<IActionResult> MenuAnalysisExport([FromQuery] string month, [FromQuery] Guid? locationId, [FromQuery] string? lang, CancellationToken ct)
    {
        var result = await profit.MenuAnalysisAsync(month, locationId, ct);
        if (!result.Succeeded) return ToResult(result);
        return File(ProfitSpreadsheet.MenuAnalysis(result.Data!, ProfitSpreadsheet.Lang(lang)), Xlsx, $"menu-analysis-{month}.xlsx");
    }

    /// <summary>The month's profit with last month next to it.</summary>
    [HttpGet("month")]
    [Authorize(Policy = "CeoPayroll")]
    public async Task<IActionResult> Month([FromQuery] string month, [FromQuery] Guid? locationId, CancellationToken ct)
        => ToResult(await profit.MonthAsync(month, locationId, ct));

    [HttpGet("month/export")]
    [Authorize(Policy = "CeoPayroll")]
    public async Task<IActionResult> MonthExport([FromQuery] string month, [FromQuery] Guid? locationId, [FromQuery] string? lang, CancellationToken ct)
    {
        var result = await profit.MonthAsync(month, locationId, ct);
        if (!result.Succeeded) return ToResult(result);
        var l = ProfitSpreadsheet.Lang(lang);
        return File(ProfitSpreadsheet.Month(result.Data!, l), Xlsx, $"profit-{month}.xlsx");
    }

    // ── monthly costs ──

    [HttpGet("expenses")]
    public async Task<IActionResult> Expenses([FromQuery] string month, CancellationToken ct)
        => ToResult(await profit.ExpensesAsync(month, ct));

    [HttpPost("expenses")]
    public async Task<IActionResult> AddExpense([FromBody] SaveCafeExpenseDto dto, CancellationToken ct)
        => ToResult(await profit.AddExpenseAsync(dto, ct));

    [HttpPut("expenses/{id:guid}")]
    public async Task<IActionResult> UpdateExpense(Guid id, [FromBody] SaveCafeExpenseDto dto, CancellationToken ct)
        => ToResult(await profit.UpdateExpenseAsync(id, dto, ct));

    [HttpDelete("expenses/{id:guid}")]
    public async Task<IActionResult> DeleteExpense(Guid id, CancellationToken ct)
    {
        var result = await profit.DeleteExpenseAsync(id, ct);
        return result.Succeeded ? NoContent() : ToResult(result);
    }

    /// <summary>Copies last month's costs into an empty month.</summary>
    [HttpPost("expenses/copy-previous")]
    public async Task<IActionResult> CopyPrevious([FromBody] CopyExpensesDto dto, CancellationToken ct)
        => ToResult(await profit.CopyPreviousAsync(dto.Month, ct));
}
