using System.Collections.Concurrent;
using System.Text.RegularExpressions;
using Microsoft.EntityFrameworkCore;
using Yurt.Application.Common.Interfaces;
using Yurt.Application.Common.Models;
using Yurt.Domain.Entities;
using Yurt.Domain.Enums;

namespace Yurt.Application.Features.TimeClock;

public record SetStaffPinDto(string CurrentPassword, string Pin);
public record SetOtherStaffPinDto(string Pin);
public record StaffPinPersonDto(Guid Id, string Username, string Role, bool IsActive, bool HasPin);

/// <summary>
/// The staff PIN (module own_pos): 4 to 6 digits that belong to one person. Typed at the till it switches the till to
/// that person or clocks them in and out, so nobody has to type a password between customers. Stored hashed. A staff PIN
/// is different from every other staff PIN and from every manager PIN of the cafe.
/// </summary>
public class StaffPinService(IApplicationDbContext db, IPasswordHasher hasher, IAuditLogService audit, ICurrentUserService user)
{
    public const int MaxFailures = 5;
    public static readonly TimeSpan LockFor = TimeSpan.FromMinutes(5);

    private static readonly Regex PinShape = new("^[0-9]{4,6}$", RegexOptions.Compiled);

    // Wrong PINs counted per signed-in till login (ids are unique across cafes), so a PIN cannot be guessed at the till.
    private static readonly ConcurrentDictionary<Guid, (int Fails, DateTime LockedUntil)> Failures = new();

    public static void ResetFailures() => Failures.Clear();

    public async Task<bool> HasPinAsync(CancellationToken ct = default)
        => user.UserId is { } me && await db.AdminUsers.AnyAsync(u => u.Id == me && u.StaffPinHash != null, ct);

    /// <summary>Everyone the signed-in manager may set or clear a PIN for: an Admin their Workers, the CEO everybody.</summary>
    public async Task<List<StaffPinPersonDto>> ListAsync(CancellationToken ct = default)
    {
        var people = await db.AdminUsers.AsNoTracking().Where(u => u.Role != AdminRole.SuperAdmin).OrderBy(u => u.Username).ToListAsync(ct);
        return people.Where(CanManage).Select(u => new StaffPinPersonDto(u.Id, u.Username, u.Role.ToString(), u.IsActive, u.StaffPinHash != null)).ToList();
    }

    private bool CanManage(AdminUser target)
        => user.Role == "Ceo" || user.Role == "SuperAdmin" || target.Id == user.UserId || (user.Role == "Admin" && target.Role == AdminRole.Worker);

    /// <summary>Set my own staff PIN. My account password proves it is me.</summary>
    public async Task<Result> SetOwnAsync(SetStaffPinDto dto, CancellationToken ct = default)
    {
        if (user.UserId is not { } meId) return Result.Failure("Sign in first.", 401);
        var me = await db.AdminUsers.FirstOrDefaultAsync(u => u.Id == meId, ct);
        if (me is null || !me.IsActive || me.Role == AdminRole.SuperAdmin) return Result.Failure("Account not found.", 404);
        if (!hasher.Verify(dto.CurrentPassword ?? "", me.PasswordHash))
            return Result.Failure("Current password is incorrect.", 400);
        return await SetAsync(me, dto.Pin, ct);
    }

    /// <summary>A manager sets someone's PIN for them (a new cashier's first PIN).</summary>
    public async Task<Result> SetForAsync(Guid adminUserId, string? pin, CancellationToken ct = default)
    {
        var target = await db.AdminUsers.FirstOrDefaultAsync(u => u.Id == adminUserId && u.Role != AdminRole.SuperAdmin, ct);
        if (target is null || !CanManage(target)) return Result.Failure("Staff member not found.", 404);
        if (!target.IsActive) return Result.Failure("This account is switched off.", 409);
        return await SetAsync(target, pin, ct);
    }

    public async Task<Result> ClearAsync(Guid adminUserId, CancellationToken ct = default)
    {
        var target = await db.AdminUsers.FirstOrDefaultAsync(u => u.Id == adminUserId && u.Role != AdminRole.SuperAdmin, ct);
        if (target is null || !CanManage(target)) return Result.Failure("Staff member not found.", 404);
        target.StaffPinHash = null;
        target.StaffPinSetAt = null;
        await db.SaveChangesAsync(ct);
        await audit.LogAsync("StaffPinCleared", "AdminUser", target.Id.ToString(), target.Username, ct);
        return Result.Success();
    }

    private async Task<Result> SetAsync(AdminUser target, string? pin, CancellationToken ct)
    {
        if (!PinShape.IsMatch(pin ?? "")) return Result.Failure("The PIN must be 4 to 6 digits.", 400);

        var others = await db.AdminUsers
            .Where(u => u.Id != target.Id && u.IsActive && (u.StaffPinHash != null || u.ManagerPinHash != null))
            .Select(u => new { u.StaffPinHash, u.ManagerPinHash }).ToListAsync(ct);
        if (others.Any(o => (o.StaffPinHash != null && hasher.Verify(pin!, o.StaffPinHash)) || (o.ManagerPinHash != null && hasher.Verify(pin!, o.ManagerPinHash))))
            return Result.Failure("Someone else already uses this PIN. Choose a different one.", 409);
        // My own manager PIN counts too: the audit log must never blur "approved" and "clocked in".
        var mine = target.ManagerPinHash;
        if (mine != null && hasher.Verify(pin!, mine))
            return Result.Failure("This is already your manager PIN. Choose a different one.", 409);

        target.StaffPinHash = hasher.Hash(pin!);
        target.StaffPinSetAt = DateTime.UtcNow;
        await db.SaveChangesAsync(ct);
        await audit.LogAsync("StaffPinSet", "AdminUser", target.Id.ToString(), target.Username, ct);
        return Result.Success();
    }

    /// <summary>
    /// Finds the active staff member of this cafe who owns the PIN. Wrong PINs are counted against the signed-in till login:
    /// 5 in a row lock it for 5 minutes. Never returns the platform operator.
    /// </summary>
    public async Task<Result<AdminUser>> FindByPinAsync(string? pin, string action, CancellationToken ct = default)
    {
        pin = pin?.Trim() ?? "";
        if (pin.Length == 0) return Result<AdminUser>.Failure("Type your PIN.", 400);

        var actor = user.UserId ?? Guid.Empty;
        if (Failures.TryGetValue(actor, out var f) && f.LockedUntil > DateTime.UtcNow)
        {
            var minutes = (int)Math.Ceiling((f.LockedUntil - DateTime.UtcNow).TotalMinutes);
            return Result<AdminUser>.Failure($"Too many wrong PINs. Try again in {minutes} min.", 429);
        }

        var candidates = await db.AdminUsers.Where(u => u.IsActive && u.StaffPinHash != null && u.Role != AdminRole.SuperAdmin).ToListAsync(ct);
        var match = PinShape.IsMatch(pin) ? candidates.FirstOrDefault(u => hasher.Verify(pin, u.StaffPinHash!)) : null;
        if (match is null)
        {
            var fails = (Failures.TryGetValue(actor, out var prev) ? prev.Fails : 0) + 1;
            Failures[actor] = fails >= MaxFailures ? (0, DateTime.UtcNow + LockFor) : (fails, DateTime.MinValue);
            await audit.LogAsync("StaffPinRejected", "Approval", null, action, ct);
            return Result<AdminUser>.Failure("Wrong PIN.", 422);
        }

        Failures.TryRemove(actor, out _);
        return Result<AdminUser>.Success(match);
    }

    /// <summary>Switch the till to another person: only Workers and Admins (the owner signs in with the password), and not before a new password is chosen.</summary>
    public async Task<Result<AdminUser>> FindForSwitchAsync(string? pin, CancellationToken ct = default)
    {
        var found = await FindByPinAsync(pin, "switch", ct);
        if (!found.Succeeded) return found;
        var who = found.Data!;
        if (who.Role == AdminRole.Ceo) return Result<AdminUser>.Failure("The owner signs in with the password, not a PIN.", 403);
        if (who.MustChangePassword) return Result<AdminUser>.Failure("Sign in with your password once to choose a new one first.", 403);
        await audit.LogAsync("StaffSwitched", "AdminUser", who.Id.ToString(), who.Username, ct);
        return found;
    }
}
