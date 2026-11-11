using System.Text.Json;
using System.Text.RegularExpressions;
using Microsoft.EntityFrameworkCore;
using Yurt.Application.Common.Interfaces;
using Yurt.Application.Common.Models;
using Yurt.Domain;
using Yurt.Domain.Entities;
using Yurt.Domain.Enums;

namespace Yurt.Application.Features.Platform;

/// <summary>
/// Operator tool to open a new cafe on the shared database: the cafe row, its first admin (who must change the
/// password at first login) and a starting brand with the cafe's name. Only the core module is on; every other module
/// starts switched off until the operator turns it on (and prices it) on the Platform screen. Menu and locations are
/// left empty; the new admin fills them in. Every change is audited and the cafe list cache is cleared so the new address works at once.
/// </summary>
public class CafeProvisioningService
{
    private static readonly Regex SlugPattern = new("^[a-z0-9][a-z0-9-]{0,38}[a-z0-9]$", RegexOptions.Compiled);
    private static readonly Regex DomainPattern = new(@"^(?=.{4,253}$)([a-z0-9]([a-z0-9-]{0,61}[a-z0-9])?\.)+[a-z]{2,}$", RegexOptions.Compiled);
    private static readonly JsonSerializerOptions BrandJson = new(JsonSerializerDefaults.Web);
    public const int MinPasswordLength = 8;

    private readonly IApplicationDbContext _db;
    private readonly ITenantDirectory _directory;
    private readonly IAuditLogService _audit;
    private readonly IPasswordHasher _hasher;

    public CafeProvisioningService(IApplicationDbContext db, ITenantDirectory directory, IAuditLogService audit, IPasswordHasher hasher)
    {
        _hasher = hasher;
        _db = db;
        _directory = directory;
        _audit = audit;
    }

    public async Task<List<CafeDto>> ListAsync(CancellationToken ct = default)
        => await _db.Tenants.AsNoTracking()
            .OrderBy(t => t.Name)
            .Select(t => new CafeDto(t.Id, t.Slug, t.Name, t.Domain, t.IsActive))
            .ToListAsync(ct);

    public async Task<Result<CafeDto>> CreateAsync(CreateCafeDto dto, CancellationToken ct = default)
    {
        var slug = (dto.Slug ?? string.Empty).Trim().ToLowerInvariant();
        var name = (dto.Name ?? string.Empty).Trim();
        var domain = string.IsNullOrWhiteSpace(dto.Domain) ? null : dto.Domain.Trim().ToLowerInvariant();
        var username = (dto.AdminUsername ?? string.Empty).Trim();
        var password = dto.AdminPassword ?? string.Empty;

        if (!SlugPattern.IsMatch(slug))
            return Result<CafeDto>.Failure("Key must be 2-40 characters: lowercase letters, digits and dashes.", 400);
        if (name.Length is < 1 or > 100)
            return Result<CafeDto>.Failure("Name is required (max 100 characters).", 400);
        if (domain is not null && !DomainPattern.IsMatch(domain))
            return Result<CafeDto>.Failure("Web address must be a host name such as order.mycafe.kz (no https://).", 400);
        if (username.Length is < 3 or > 50)
            return Result<CafeDto>.Failure("Admin username must be 3-50 characters.", 400);
        if (password.Length < MinPasswordLength)
            return Result<CafeDto>.Failure($"Admin password must be at least {MinPasswordLength} characters.", 400);

        if (await _db.Tenants.AnyAsync(t => t.Slug == slug, ct))
            return Result<CafeDto>.Failure("That key is already used by another cafe.", 409);
        if (domain is not null && await _db.Tenants.AnyAsync(t => t.Domain == domain, ct))
            return Result<CafeDto>.Failure("That web address is already used by another cafe.", 409);

        var tenant = new Tenant { Slug = slug, Name = name, Domain = domain };
        _db.Tenants.Add(tenant);
        _db.AdminUsers.Add(new AdminUser
        {
            TenantId = tenant.Id,
            Username = username,
            PasswordHash = _hasher.Hash(password),
            Role = AdminRole.Ceo, // the first account of a new cafe is its owner
            MustChangePassword = true,
        });
        var brand = BrandDto.Default with { Name = name };
        _db.PlatformSettings.Add(new PlatformSetting
        {
            TenantId = tenant.Id,
            Key = PlatformSettingKeys.Brand,
            Value = JsonSerializer.Serialize(brand, BrandJson),
        });
        foreach (var key in ModuleKeys.All.Where(k => k != ModuleKeys.Core))
        {
            _db.ModuleLicenses.Add(new ModuleLicense
            {
                TenantId = tenant.Id,
                ModuleKey = key,
                Enabled = false,
                Source = ModuleLicenseSource.Manual,
                Notes = "Off when the cafe was opened",
            });
        }
        await _db.SaveChangesAsync(ct);
        _directory.Invalidate();

        await _audit.LogPlatformAsync("CafeCreated", "Tenant", tenant.Id.ToString(), $"Opened cafe '{slug}'", ct);
        return Result<CafeDto>.Success(new CafeDto(tenant.Id, slug, name, domain, true));
    }

    public async Task<Result<CafeDto>> SetActiveAsync(Guid id, bool isActive, CancellationToken ct = default)
    {
        var tenant = await _db.Tenants.FirstOrDefaultAsync(t => t.Id == id, ct);
        if (tenant is null) return Result<CafeDto>.Failure("Cafe not found.", 404);
        if (id == Tenant.DefaultId && !isActive)
            return Result<CafeDto>.Failure("The default cafe cannot be switched off.", 400);

        tenant.IsActive = isActive;
        await _db.SaveChangesAsync(ct);
        _directory.Invalidate();

        await _audit.LogPlatformAsync(isActive ? "CafeEnabled" : "CafeDisabled", "Tenant", id.ToString(), null, ct);
        return Result<CafeDto>.Success(new CafeDto(tenant.Id, tenant.Slug, tenant.Name, tenant.Domain, tenant.IsActive));
    }

    /// <summary>The admin accounts of one cafe (not its workers), for the operator's password reset.</summary>
    public async Task<Result<List<CafeAdminDto>>> ListAdminsAsync(Guid cafeId, CancellationToken ct = default)
    {
        if (!await _db.Tenants.AnyAsync(t => t.Id == cafeId, ct))
            return Result<List<CafeAdminDto>>.Failure("Cafe not found.", 404);

        var admins = await _db.AdminUsers.IgnoreQueryFilters().AsNoTracking()
            .Where(u => u.TenantId == cafeId && (u.Role == AdminRole.Admin || u.Role == AdminRole.Ceo))
            .OrderBy(u => u.Role).ThenBy(u => u.Username)
            .Select(u => new CafeAdminDto(u.Id, u.Username, u.IsActive, u.Role.ToString()))
            .ToListAsync(ct);
        return Result<List<CafeAdminDto>>.Success(admins);
    }

    /// <summary>
    /// Only the platform operator resets a cafe admin's password. The admin must pick their own new password at the next
    /// sign-in and every session they have open is ended. Audited as a platform action; the password is never logged.
    /// </summary>
    public async Task<Result<CafeAdminDto>> ResetAdminPasswordAsync(Guid cafeId, Guid adminId, string newPassword, CancellationToken ct = default)
    {
        if (string.IsNullOrWhiteSpace(newPassword) || newPassword.Length < MinPasswordLength)
            return Result<CafeAdminDto>.Failure($"New password must be at least {MinPasswordLength} characters.", 422);

        var admin = await _db.AdminUsers.IgnoreQueryFilters()
            .FirstOrDefaultAsync(u => u.Id == adminId && u.TenantId == cafeId && (u.Role == AdminRole.Admin || u.Role == AdminRole.Ceo), ct);
        if (admin is null) return Result<CafeAdminDto>.Failure("Admin not found.", 404);

        admin.PasswordHash = _hasher.Hash(newPassword);
        admin.MustChangePassword = true;
        admin.UpdatedAt = DateTime.UtcNow;

        var now = DateTime.UtcNow;
        var open = await _db.RefreshTokens.IgnoreQueryFilters()
            .Where(t => t.UserType == RefreshTokenUserType.Admin && t.UserId == adminId && !t.IsRevoked)
            .ToListAsync(ct);
        foreach (var t in open) { t.IsRevoked = true; t.RevokedAt = now; }
        await _db.SaveChangesAsync(ct);

        await _audit.LogPlatformAsync("AdminPasswordReset", "AdminUser", adminId.ToString(), admin.Username, ct);
        return Result<CafeAdminDto>.Success(new CafeAdminDto(admin.Id, admin.Username, admin.IsActive, admin.Role.ToString()));
    }

    /// <summary>
    /// Makes one of the cafe's management accounts the CEO or an Admin. This is how the operator hands a cafe's money
    /// screens (reports, billing, payroll) to its owner. The change takes effect at the account's next sign-in or token
    /// refresh, so its open sessions are ended now. The cafe always keeps at least one active CEO.
    /// </summary>
    public async Task<Result<CafeAdminDto>> SetAdminRoleAsync(Guid cafeId, Guid adminId, string role, CancellationToken ct = default)
    {
        if (!Enum.TryParse<AdminRole>(role, true, out var newRole) || newRole is not (AdminRole.Ceo or AdminRole.Admin))
            return Result<CafeAdminDto>.Failure("Role must be Ceo or Admin.", 422);

        var admin = await _db.AdminUsers.IgnoreQueryFilters()
            .FirstOrDefaultAsync(u => u.Id == adminId && u.TenantId == cafeId && (u.Role == AdminRole.Admin || u.Role == AdminRole.Ceo), ct);
        if (admin is null) return Result<CafeAdminDto>.Failure("Admin not found.", 404);
        if (admin.Role == newRole) return Result<CafeAdminDto>.Success(new CafeAdminDto(admin.Id, admin.Username, admin.IsActive, admin.Role.ToString()));

        if (newRole == AdminRole.Admin &&
            !await _db.AdminUsers.IgnoreQueryFilters().AnyAsync(
                u => u.TenantId == cafeId && u.Role == AdminRole.Ceo && u.IsActive && u.Id != adminId, ct))
            return Result<CafeAdminDto>.Failure("The cafe must keep at least one active CEO. Make someone else the CEO first.", 409);

        admin.Role = newRole;
        admin.UpdatedAt = DateTime.UtcNow;
        var now = DateTime.UtcNow;
        var open = await _db.RefreshTokens.IgnoreQueryFilters()
            .Where(t => t.UserType == RefreshTokenUserType.Admin && t.UserId == adminId && !t.IsRevoked)
            .ToListAsync(ct);
        foreach (var t in open) { t.IsRevoked = true; t.RevokedAt = now; }
        await _db.SaveChangesAsync(ct);

        await _audit.LogPlatformAsync("AdminRoleChanged", "AdminUser", adminId.ToString(), $"{admin.Username} -> {newRole}", ct);
        return Result<CafeAdminDto>.Success(new CafeAdminDto(admin.Id, admin.Username, admin.IsActive, admin.Role.ToString()));
    }
}
