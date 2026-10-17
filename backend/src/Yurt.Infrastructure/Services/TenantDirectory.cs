using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Caching.Memory;
using Microsoft.Extensions.DependencyInjection;
using Yurt.Application.Common.Interfaces;
using Yurt.Domain.Entities;
using Yurt.Infrastructure.Persistence;

namespace Yurt.Infrastructure.Services;

public class TenantDirectory(IServiceScopeFactory scopes, IMemoryCache cache) : ITenantDirectory
{
    private const string CacheKey = "tenants:active";
    private static readonly TimeSpan Ttl = TimeSpan.FromSeconds(60);

    public void Invalidate() => cache.Remove(CacheKey);

    public IReadOnlyList<Tenant> GetActive()
        => cache.TryGetValue(CacheKey, out IReadOnlyList<Tenant>? cached) && cached is not null
            ? cached
            : Task.Run(() => GetActiveAsync()).GetAwaiter().GetResult();

    public async Task<IReadOnlyList<Tenant>> GetActiveAsync(CancellationToken ct = default)
    {
        if (cache.TryGetValue(CacheKey, out IReadOnlyList<Tenant>? cached) && cached is not null) return cached;

        using var scope = scopes.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<ApplicationDbContext>();
        var tenants = await db.Tenants.AsNoTracking().Where(t => t.IsActive).ToListAsync(ct);
        cache.Set(CacheKey, (IReadOnlyList<Tenant>)tenants, Ttl);
        return tenants;
    }
}
