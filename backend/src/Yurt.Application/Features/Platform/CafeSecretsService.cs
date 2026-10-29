using Microsoft.EntityFrameworkCore;
using Yurt.Application.Common.Interfaces;
using Yurt.Application.Common.Models;
using Yurt.Domain.Entities;

namespace Yurt.Application.Features.Platform;

/// <summary>
/// Operator tool to set or clear a cafe's secrets. Values go in encrypted and never come back out: the list shows only
/// which keys are set. The audit log records the key name and the action, never the value.
/// </summary>
public class CafeSecretsService
{
    public const int MaxValueLength = 20000; // a Firebase service-account JSON is a few KB

    private readonly IApplicationDbContext _db;
    private readonly ITenantSecrets _secrets;
    private readonly ISecretProtector _protector;
    private readonly IAuditLogService _audit;

    public CafeSecretsService(IApplicationDbContext db, ITenantSecrets secrets, ISecretProtector protector, IAuditLogService audit)
    {
        _db = db;
        _secrets = secrets;
        _protector = protector;
        _audit = audit;
    }

    public async Task<Result<List<CafeSecretStatusDto>>> ListAsync(Guid cafeId, CancellationToken ct = default)
    {
        if (!await _db.Tenants.AnyAsync(t => t.Id == cafeId, ct))
            return Result<List<CafeSecretStatusDto>>.Failure("Cafe not found.", 404);
        var set = (await _secrets.ConfiguredKeysAsync(cafeId, ct)).ToHashSet();
        return Result<List<CafeSecretStatusDto>>.Success(
            TenantSecretKeys.All.Select(k => new CafeSecretStatusDto(k, set.Contains(k))).ToList());
    }

    public async Task<Result<CafeSecretStatusDto>> SetAsync(Guid cafeId, string key, string? value, CancellationToken ct = default)
    {
        if (!TenantSecretKeys.All.Contains(key))
            return Result<CafeSecretStatusDto>.Failure($"Unknown secret '{key}'.", 404);
        if (!await _db.Tenants.AnyAsync(t => t.Id == cafeId, ct))
            return Result<CafeSecretStatusDto>.Failure("Cafe not found.", 404);
        if (string.IsNullOrWhiteSpace(value))
            return Result<CafeSecretStatusDto>.Failure("Value is required.", 400);
        if (value.Length > MaxValueLength)
            return Result<CafeSecretStatusDto>.Failure($"Value is too long (max {MaxValueLength} characters).", 400);
        if (!_protector.IsAvailable)
            return Result<CafeSecretStatusDto>.Failure("Secrets:MasterKey is not configured on the server, so secrets cannot be stored.", 503);

        await _secrets.SetAsync(cafeId, key, value.Trim(), ct);
        await _audit.LogPlatformAsync("CafeSecretSet", "Tenant", cafeId.ToString(), key, ct);
        return Result<CafeSecretStatusDto>.Success(new CafeSecretStatusDto(key, true));
    }

    public async Task<Result<CafeSecretStatusDto>> ClearAsync(Guid cafeId, string key, CancellationToken ct = default)
    {
        if (!TenantSecretKeys.All.Contains(key))
            return Result<CafeSecretStatusDto>.Failure($"Unknown secret '{key}'.", 404);
        if (await _secrets.ClearAsync(cafeId, key, ct))
            await _audit.LogPlatformAsync("CafeSecretCleared", "Tenant", cafeId.ToString(), key, ct);
        return Result<CafeSecretStatusDto>.Success(new CafeSecretStatusDto(key, false));
    }
}
