using Microsoft.Extensions.Caching.Memory;
using Yurt.Application.Common.Interfaces;

namespace Yurt.Infrastructure.Services;

public sealed class MemoryMenuCacheService : IMenuCacheService
{
    private readonly IMemoryCache _cache;
    private static readonly TimeSpan DefaultTtl = TimeSpan.FromMinutes(5);
    private int _version;

    public MemoryMenuCacheService(IMemoryCache cache) => _cache = cache;

    private string Key(string raw) => $"menu:v{_version}:{raw}";

    public Task<T?> GetAsync<T>(string key) where T : class
    {
        _cache.TryGetValue(Key(key), out T? value);
        return Task.FromResult(value);
    }

    public Task SetAsync<T>(string key, T value, TimeSpan? ttl = null) where T : class
    {
        _cache.Set(Key(key), value, ttl ?? DefaultTtl);
        return Task.CompletedTask;
    }

    public Task InvalidateAllMenuAsync()
    {
        // Incrementing the version makes all previous cache keys unreachable.
        // Stale entries are evicted by IMemoryCache's own TTL sweep.
        // NOTE: for multi-instance deployments swap this for a distributed cache
        // (e.g. Redis via IDistributedCache) and broadcast the version via pub/sub.
        Interlocked.Increment(ref _version);
        return Task.CompletedTask;
    }
}
