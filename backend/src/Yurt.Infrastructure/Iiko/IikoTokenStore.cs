namespace Yurt.Infrastructure.Iiko;

/// <summary>
/// Process-wide cache for the iiko session token (singleton).
/// iiko session keys live ~1 hour; we refresh proactively and on 401.
/// </summary>
public class IikoTokenStore
{
    private readonly SemaphoreSlim _lock = new(1, 1);
    private string? _token;
    private DateTime _expiresAtUtc = DateTime.MinValue;

    public async Task<string> GetOrRefreshAsync(
        Func<CancellationToken, Task<string>> fetch, TimeSpan lifetime, bool forceRefresh, CancellationToken ct)
    {
        if (!forceRefresh && _token != null && DateTime.UtcNow < _expiresAtUtc)
            return _token;

        await _lock.WaitAsync(ct);
        try
        {
            // Another caller may have refreshed while we waited
            if (!forceRefresh && _token != null && DateTime.UtcNow < _expiresAtUtc)
                return _token;

            _token = await fetch(ct);
            _expiresAtUtc = DateTime.UtcNow.Add(lifetime);
            return _token;
        }
        finally
        {
            _lock.Release();
        }
    }

    public void Invalidate() => _expiresAtUtc = DateTime.MinValue;
}
