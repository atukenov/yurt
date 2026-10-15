using Microsoft.EntityFrameworkCore;
using Yurt.Application.Common.Interfaces;
using Yurt.Application.Common.Models;
using Yurt.Domain.Entities;
using Yurt.Domain.Enums;

namespace Yurt.Application.Features.Workers;

public class WorkerService
{
    private readonly IApplicationDbContext _db;
    private readonly IPasswordHasher _hasher;
    private readonly IAuditLogService _audit;

    public WorkerService(IApplicationDbContext db, IPasswordHasher hasher, IAuditLogService audit)
    {
        _db = db;
        _hasher = hasher;
        _audit = audit;
    }

    public async Task<List<WorkerDto>> GetWorkersAsync(CancellationToken ct = default)
        => await _db.AdminUsers
            .OrderBy(u => u.Username)
            .Select(u => new WorkerDto(u.Id, u.Username, u.Role.ToString(), u.IsActive, u.CreatedAt))
            .ToListAsync(ct);

    public async Task<Result<WorkerDto>> CreateWorkerAsync(CreateWorkerDto dto, CancellationToken ct = default)
    {
        if (await _db.AdminUsers.AnyAsync(u => u.Username == dto.Username, ct))
            return Result<WorkerDto>.Failure("Username already taken.", 409);

        var worker = new AdminUser
        {
            Username = dto.Username,
            PasswordHash = _hasher.Hash(dto.Password),
            Role = AdminRole.Worker,
            IsActive = true
        };
        _db.AdminUsers.Add(worker);
        await _db.SaveChangesAsync(ct);

        await _audit.LogAsync("WorkerCreated", "AdminUser", worker.Id.ToString(), worker.Username, ct);
        return Result<WorkerDto>.Success(
            new WorkerDto(worker.Id, worker.Username, worker.Role.ToString(), worker.IsActive, worker.CreatedAt), 201);
    }

    public async Task<Result<WorkerDto>> UpdateWorkerAsync(Guid id, UpdateWorkerDto dto, CancellationToken ct = default)
    {
        var worker = await _db.AdminUsers.FirstOrDefaultAsync(u => u.Id == id, ct);
        if (worker == null) return Result<WorkerDto>.NotFound();

        if (worker.Username != dto.Username &&
            await _db.AdminUsers.AnyAsync(u => u.Username == dto.Username, ct))
            return Result<WorkerDto>.Failure("Username already taken.", 409);

        worker.Username = dto.Username;
        worker.IsActive = dto.IsActive;
        worker.UpdatedAt = DateTime.UtcNow;
        await _db.SaveChangesAsync(ct);

        var action = dto.IsActive ? "WorkerActivated" : "WorkerDeactivated";
        await _audit.LogAsync(action, "AdminUser", id.ToString(), worker.Username, ct);
        return Result<WorkerDto>.Success(
            new WorkerDto(worker.Id, worker.Username, worker.Role.ToString(), worker.IsActive, worker.CreatedAt));
    }

    public async Task<Result<WorkerDto>> SetActiveAsync(Guid id, bool isActive, CancellationToken ct = default)
    {
        var worker = await _db.AdminUsers.FirstOrDefaultAsync(u => u.Id == id, ct);
        if (worker is null) return Result<WorkerDto>.NotFound();

        worker.IsActive = isActive;
        worker.UpdatedAt = DateTime.UtcNow;
        await _db.SaveChangesAsync(ct);

        var action = isActive ? "WorkerActivated" : "WorkerDeactivated";
        await _audit.LogAsync(action, "AdminUser", id.ToString(), worker.Username, ct);
        return Result<WorkerDto>.Success(
            new WorkerDto(worker.Id, worker.Username, worker.Role.ToString(), worker.IsActive, worker.CreatedAt));
    }

    public async Task<Result<WorkerDto>> ResetPasswordAsync(Guid id, ResetWorkerPasswordDto dto, CancellationToken ct = default)
    {
        var worker = await _db.AdminUsers.FirstOrDefaultAsync(u => u.Id == id, ct);
        if (worker == null) return Result<WorkerDto>.NotFound();

        worker.PasswordHash = _hasher.Hash(dto.NewPassword);
        worker.UpdatedAt = DateTime.UtcNow;
        await _db.SaveChangesAsync(ct);

        await _audit.LogAsync("WorkerPasswordReset", "AdminUser", id.ToString(), worker.Username, ct);
        return Result<WorkerDto>.Success(
            new WorkerDto(worker.Id, worker.Username, worker.Role.ToString(), worker.IsActive, worker.CreatedAt));
    }
}
