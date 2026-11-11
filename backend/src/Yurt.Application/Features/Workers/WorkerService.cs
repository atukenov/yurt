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

    // Platform operators are invisible to the cafe's staff page and cannot be edited from it.
    private IQueryable<AdminUser> CafeStaff => _db.AdminUsers.Where(u => u.Role != AdminRole.SuperAdmin);

    public async Task<List<WorkerDto>> GetWorkersAsync(CancellationToken ct = default)
        => await CafeStaff
            .OrderBy(u => u.Username)
            .Select(u => new WorkerDto(u.Id, u.Username, u.Role.ToString(), u.IsActive, u.CreatedAt))
            .ToListAsync(ct);

    /// <summary>
    /// Who may change whose account. A Worker account is managed by any Admin or CEO. An Admin account only by the CEO.
    /// A CEO account only by the platform operator (Platform screen), so nobody can take over the owner's login.
    /// </summary>
    private static bool CanManage(AdminRole caller, AdminRole target) => target switch
    {
        AdminRole.Worker => true,
        AdminRole.Admin => caller == AdminRole.Ceo,
        _ => false,
    };

    private const string NotAllowed = "You are not allowed to change this account.";

    public async Task<Result<WorkerDto>> CreateWorkerAsync(CreateWorkerDto dto, AdminRole caller, CancellationToken ct = default)
    {
        var role = string.Equals(dto.Role, nameof(AdminRole.Admin), StringComparison.OrdinalIgnoreCase)
            ? AdminRole.Admin
            : AdminRole.Worker;
        if (!CanManage(caller, role))
            return Result<WorkerDto>.Failure("Only the CEO can add an admin.", 403);

        if (await _db.AdminUsers.AnyAsync(u => u.Username == dto.Username, ct))
            return Result<WorkerDto>.Failure("Username already taken.", 409);

        var worker = new AdminUser
        {
            Username = dto.Username,
            PasswordHash = _hasher.Hash(dto.Password),
            Role = role,
            IsActive = true
        };
        _db.AdminUsers.Add(worker);
        await _db.SaveChangesAsync(ct);

        await _audit.LogAsync(role == AdminRole.Admin ? "AdminCreated" : "WorkerCreated", "AdminUser", worker.Id.ToString(), worker.Username, ct);
        return Result<WorkerDto>.Success(
            new WorkerDto(worker.Id, worker.Username, worker.Role.ToString(), worker.IsActive, worker.CreatedAt), 201);
    }

    public async Task<Result<WorkerDto>> UpdateWorkerAsync(Guid id, UpdateWorkerDto dto, AdminRole caller, CancellationToken ct = default)
    {
        var worker = await CafeStaff.FirstOrDefaultAsync(u => u.Id == id, ct);
        if (worker == null) return Result<WorkerDto>.NotFound();
        if (!CanManage(caller, worker.Role)) return Result<WorkerDto>.Failure(NotAllowed, 403);

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

    public async Task<Result<WorkerDto>> SetActiveAsync(Guid id, bool isActive, AdminRole caller, CancellationToken ct = default)
    {
        var worker = await CafeStaff.FirstOrDefaultAsync(u => u.Id == id, ct);
        if (worker is null) return Result<WorkerDto>.NotFound();
        if (!CanManage(caller, worker.Role)) return Result<WorkerDto>.Failure(NotAllowed, 403);

        worker.IsActive = isActive;
        worker.UpdatedAt = DateTime.UtcNow;
        await _db.SaveChangesAsync(ct);

        var action = isActive ? "WorkerActivated" : "WorkerDeactivated";
        await _audit.LogAsync(action, "AdminUser", id.ToString(), worker.Username, ct);
        return Result<WorkerDto>.Success(
            new WorkerDto(worker.Id, worker.Username, worker.Role.ToString(), worker.IsActive, worker.CreatedAt));
    }

    public async Task<Result<WorkerDto>> ResetPasswordAsync(Guid id, ResetWorkerPasswordDto dto, AdminRole caller, CancellationToken ct = default)
    {
        var worker = await CafeStaff.FirstOrDefaultAsync(u => u.Id == id, ct);
        if (worker == null) return Result<WorkerDto>.NotFound();

        // An Admin resets workers' passwords, the CEO also resets Admins'. The CEO's own password is reset by the
        // platform operator (Platform screen), so nobody can take over the owner's account.
        if (!CanManage(caller, worker.Role))
            return Result<WorkerDto>.Failure(
                worker.Role == AdminRole.Worker ? NotAllowed : "Only the CEO or the platform operator can reset this password.", 403);

        worker.PasswordHash = _hasher.Hash(dto.NewPassword);
        worker.UpdatedAt = DateTime.UtcNow;
        await _db.SaveChangesAsync(ct);

        await _audit.LogAsync("WorkerPasswordReset", "AdminUser", id.ToString(), worker.Username, ct);
        return Result<WorkerDto>.Success(
            new WorkerDto(worker.Id, worker.Username, worker.Role.ToString(), worker.IsActive, worker.CreatedAt));
    }
}
