using System.Collections.Concurrent;
using System.Text.RegularExpressions;
using Microsoft.EntityFrameworkCore;
using Yurt.Application.Common.Interfaces;
using Yurt.Application.Common.Models;
using Yurt.Domain;
using Yurt.Domain.Enums;

namespace Yurt.Application.Features.Approvals;

/// <summary>A manager's PIN and the reason that came with it, as sent by the admin app or the till.</summary>
public record ManagerApprovalDto(string? Pin = null, string? Reason = null);

/// <summary>Set (or change) the signed-in manager's own PIN. The account password proves it is really them.</summary>
public record SetManagerPinDto(string CurrentPassword, string Pin);

/// <summary>What the apps need to know before asking for a PIN.</summary>
public record ManagerPinStatusDto(bool Required, bool AnyManagerHasPin, bool IHavePin);

/// <summary>Who approved an action, and why. Goes into the audit log next to the action.</summary>
public record ManagerApproval(Guid AdminId, string Username, string Reason)
{
    public string Describe() => $"{Reason} · approved by {Username}";
}

/// <summary>
/// The manager PIN gate (module own_pos): cancelling a sale, giving money back and taking money off a bill all need a
/// manager's PIN and a reason, so a cashier cannot do them alone. Managers are Admins and CEOs; each sets a PIN of
/// their own (4 to 6 digits, stored hashed, never shared between two managers so the audit log knows who approved).
/// Without own_pos the gate is off and these actions work as they always did.
/// </summary>
public class ManagerApprovalService(
    IApplicationDbContext db, IPasswordHasher hasher, IAuditLogService audit, IModuleGate modules, ICurrentUserService user)
{
    public const int MaxFailures = 5;
    public static readonly TimeSpan LockFor = TimeSpan.FromMinutes(5);
    public const int MaxReasonLength = 200;

    private static readonly Regex PinShape = new("^[0-9]{4,6}$", RegexOptions.Compiled);
    private static readonly AdminRole[] Managers = [AdminRole.Admin, AdminRole.Ceo];

    // Wrong PINs counted per signed-in staff member (ids are unique across cafes), so a cashier cannot guess a PIN.
    private static readonly ConcurrentDictionary<Guid, (int Fails, DateTime LockedUntil)> Failures = new();

    public Task<bool> IsRequiredAsync(CancellationToken ct = default) => modules.IsEnabledAsync(ModuleKeys.OwnPos, ct);

    public async Task<ManagerPinStatusDto> GetStatusAsync(CancellationToken ct = default)
    {
        var required = await IsRequiredAsync(ct);
        var withPin = await db.AdminUsers.Where(u => u.IsActive && u.ManagerPinHash != null && (u.Role == AdminRole.Admin || u.Role == AdminRole.Ceo))
            .Select(u => u.Id).ToListAsync(ct);
        return new ManagerPinStatusDto(required, withPin.Count > 0, user.UserId is { } me && withPin.Contains(me));
    }

    public async Task<Result> SetPinAsync(SetManagerPinDto dto, CancellationToken ct = default)
    {
        if (user.UserId is not { } meId) return Result.Failure("Sign in first.", 401);
        var me = await db.AdminUsers.FirstOrDefaultAsync(u => u.Id == meId, ct);
        if (me is null || !me.IsActive) return Result.Failure("Account not found.", 404);
        if (!Managers.Contains(me.Role)) return Result.Failure("Only an Admin or the CEO can have a manager PIN.", 403);
        if (!PinShape.IsMatch(dto.Pin ?? ""))
            return Result.Failure("The PIN must be 4 to 6 digits.", 400);
        if (!hasher.Verify(dto.CurrentPassword ?? "", me.PasswordHash))
            return Result.Failure("Current password is incorrect.", 400);

        var others = await db.AdminUsers
            .Where(u => u.Id != meId && u.IsActive && u.ManagerPinHash != null && (u.Role == AdminRole.Admin || u.Role == AdminRole.Ceo)).ToListAsync(ct);
        if (others.Any(o => hasher.Verify(dto.Pin!, o.ManagerPinHash!)))
            return Result.Failure("Another manager already uses this PIN. Choose a different one.", 409);

        me.ManagerPinHash = hasher.Hash(dto.Pin!);
        me.ManagerPinSetAt = DateTime.UtcNow;
        await db.SaveChangesAsync(ct);
        await audit.LogAsync("ManagerPinSet", "AdminUser", me.Id.ToString(), null, ct);
        return Result.Success();
    }

    /// <summary>
    /// Checks the PIN and reason for one action. Success(null) means the gate is off for this cafe (no own_pos), so the
    /// caller goes ahead as before. Success(approval) means a manager approved: put <see cref="ManagerApproval.Describe"/>
    /// in the audit entry of the action.
    /// </summary>
    public async Task<Result<ManagerApproval?>> RequireAsync(string action, ManagerApprovalDto? dto, CancellationToken ct = default)
    {
        if (!await IsRequiredAsync(ct)) return Result<ManagerApproval?>.Success(null);

        var reason = dto?.Reason?.Trim() ?? "";
        if (reason.Length == 0) return Result<ManagerApproval?>.Failure("Write the reason first.", 400);
        if (reason.Length > MaxReasonLength) return Result<ManagerApproval?>.Failure($"The reason is too long (up to {MaxReasonLength} characters).", 400);
        var pin = dto?.Pin?.Trim() ?? "";
        if (pin.Length == 0) return Result<ManagerApproval?>.Failure("A manager PIN is needed for this.", 422);

        var actor = user.UserId ?? Guid.Empty;
        if (Failures.TryGetValue(actor, out var f) && f.LockedUntil > DateTime.UtcNow)
        {
            var minutes = (int)Math.Ceiling((f.LockedUntil - DateTime.UtcNow).TotalMinutes);
            return Result<ManagerApproval?>.Failure($"Too many wrong PINs. Try again in {minutes} min.", 429);
        }

        var managers = await db.AdminUsers
            .Where(u => u.IsActive && u.ManagerPinHash != null && (u.Role == AdminRole.Admin || u.Role == AdminRole.Ceo)).ToListAsync(ct);
        if (managers.Count == 0)
            return Result<ManagerApproval?>.Failure("No manager has set a PIN yet. An Admin or the CEO sets one under Manager PIN.", 422);

        var match = PinShape.IsMatch(pin) ? managers.FirstOrDefault(m => hasher.Verify(pin, m.ManagerPinHash!)) : null;
        if (match is null)
        {
            var fails = (Failures.TryGetValue(actor, out var prev) ? prev.Fails : 0) + 1;
            Failures[actor] = fails >= MaxFailures ? (0, DateTime.UtcNow + LockFor) : (fails, DateTime.MinValue);
            await audit.LogAsync("ManagerPinRejected", "Approval", null, $"{action}: {reason}", ct);
            return Result<ManagerApproval?>.Failure("Wrong manager PIN.", 422);
        }

        Failures.TryRemove(actor, out _);
        return Result<ManagerApproval?>.Success(new ManagerApproval(match.Id, match.Username, reason));
    }

    /// <summary>For tests: forget counted wrong PINs.</summary>
    public static void ResetFailures() => Failures.Clear();
}
