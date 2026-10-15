namespace Yurt.Application.Common.Interfaces;

public interface IMenuCacheService
{
    Task<T?> GetAsync<T>(string key) where T : class;
    Task SetAsync<T>(string key, T value, TimeSpan? ttl = null) where T : class;
    Task InvalidateAllMenuAsync();
}
