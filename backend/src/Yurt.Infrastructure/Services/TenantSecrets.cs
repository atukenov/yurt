using Microsoft.Extensions.Configuration;
using Microsoft.EntityFrameworkCore;
using Yurt.Application.Common.Interfaces;
using Yurt.Domain.Entities;
using Yurt.Infrastructure.Persistence;

namespace Yurt.Infrastructure.Services;

public class TenantSecrets : ITenantSecrets
{
    private readonly ApplicationDbContext _db;
    private readonly ITenantContext _tenant;
    private readonly ISecretProtector _protector;
    private readonly IConfiguration _config;

    public TenantSecrets(ApplicationDbContext db, ITenantContext tenant, ISecretProtector protector, IConfiguration config)
    {
        _db = db;
        _tenant = tenant;
        _protector = protector;
        _config = config;
    }

    public async Task<string?> GetAsync(string key, CancellationToken ct = default)
    {
        var tenantId = _tenant.TenantId;
        var row = await _db.TenantSecrets.AsNoTracking().FirstOrDefaultAsync(s => s.TenantId == tenantId && s.Key == key, ct);
        if (row is not null && _protector.Unprotect(tenantId, key, row.Ciphertext) is { } stored) return stored;

        // Only the first cafe may use the server-wide values; another cafe without its own secret has none.
        if (tenantId != Tenant.DefaultId) return null;
        var fallback = _config[key];
        return string.IsNullOrWhiteSpace(fallback) || fallback.StartsWith("CHANGE_THIS", StringComparison.OrdinalIgnoreCase)
            ? null
            : fallback;
    }

    public async Task<IReadOnlyList<string>> ConfiguredKeysAsync(Guid tenantId, CancellationToken ct = default)
        => await _db.TenantSecrets.IgnoreQueryFilters().AsNoTracking()
            .Where(s => s.TenantId == tenantId)
            .Select(s => s.Key)
            .OrderBy(k => k)
            .ToListAsync(ct);

    public async Task SetAsync(Guid tenantId, string key, string value, CancellationToken ct = default)
    {
        var cipher = _protector.Protect(tenantId, key, value);
        var row = await _db.TenantSecrets.IgnoreQueryFilters().FirstOrDefaultAsync(s => s.TenantId == tenantId && s.Key == key, ct);
        if (row is null)
            _db.TenantSecrets.Add(new TenantSecret { TenantId = tenantId, Key = key, Ciphertext = cipher });
        else
        {
            row.Ciphertext = cipher;
            row.UpdatedAt = DateTime.UtcNow;
        }
        await _db.SaveChangesAsync(ct);
    }

    public async Task<bool> ClearAsync(Guid tenantId, string key, CancellationToken ct = default)
    {
        var row = await _db.TenantSecrets.IgnoreQueryFilters().FirstOrDefaultAsync(s => s.TenantId == tenantId && s.Key == key, ct);
        if (row is null) return false;
        _db.TenantSecrets.Remove(row);
        await _db.SaveChangesAsync(ct);
        return true;
    }
}
