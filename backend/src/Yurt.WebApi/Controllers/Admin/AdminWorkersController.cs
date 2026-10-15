using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Asp.Versioning;
using Yurt.Application.Features.Workers;
using Yurt.WebApi.Common;

namespace Yurt.WebApi.Controllers.Admin;

[ApiController]
[ApiVersion("1.0")]
[Route("api/v{version:apiVersion}/admin/workers")]
[Authorize(Policy = "AdminRoleAdmin")]
public class AdminWorkersController : ApiControllerBase
{
    private readonly WorkerService _workers;

    public AdminWorkersController(WorkerService workers) => _workers = workers;

    [HttpGet]
    public async Task<IActionResult> GetWorkers(CancellationToken ct)
        => Ok(await _workers.GetWorkersAsync(ct));

    [HttpPost]
    public async Task<IActionResult> CreateWorker([FromBody] CreateWorkerDto dto, CancellationToken ct)
    {
        if (string.IsNullOrWhiteSpace(dto.Username) || string.IsNullOrWhiteSpace(dto.Password))
            return ValidationError("Username and password are required.");
        if (dto.Password.Length < 6)
            return ValidationError("Password must be at least 6 characters.");

        var result = await _workers.CreateWorkerAsync(dto, ct);
        return ToResult(result);
    }

    [HttpPut("{id:guid}")]
    public async Task<IActionResult> UpdateWorker(Guid id, [FromBody] UpdateWorkerDto dto, CancellationToken ct)
    {
        if (string.IsNullOrWhiteSpace(dto.Username))
            return ValidationError("Username is required.");

        var result = await _workers.UpdateWorkerAsync(id, dto, ct);
        return ToResult(result);
    }

    [HttpPatch("{id:guid}/active")]
    public async Task<IActionResult> SetActive(Guid id, [FromBody] SetWorkerActiveDto dto, CancellationToken ct)
    {
        var result = await _workers.SetActiveAsync(id, dto.IsActive, ct);
        return ToResult(result);
    }

    [HttpPost("{id:guid}/reset-password")]
    public async Task<IActionResult> ResetPassword(Guid id, [FromBody] ResetWorkerPasswordDto dto, CancellationToken ct)
    {
        if (string.IsNullOrWhiteSpace(dto.NewPassword) || dto.NewPassword.Length < 6)
            return ValidationError("New password must be at least 6 characters.");

        var result = await _workers.ResetPasswordAsync(id, dto, ct);
        return ToResult(result);
    }
}
