using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using Yurt.Application.Common.Interfaces;
using Yurt.Application.Features.Modules;
using Yurt.Domain;

namespace Yurt.Infrastructure.Services;

public class ModuleGate : IModuleGate
{
    private readonly IServiceScopeFactory _scopeFactory;
    private readonly ModuleOptions _options;
    private readonly ILogger<ModuleGate> _logger;
    private readonly Func<DateTime> _clock;

    private readonly SemaphoreSlim _loadLock = new(1, 1);
    private volatile Snapshot? _snapshot;

    private sealed record Snapshot(IReadOnlyList<ModuleStatus> Statuses, DateTime LoadedAtUtc);

    public ModuleGate(IServiceScopeFactory scopeFactory, ModuleOptions options, ILogger<ModuleGate> logger)
        : this(scopeFactory, options, logger, () => DateTime.UtcNow) { }

    public ModuleGate(IServiceScopeFactory scopeFactory, ModuleOptions options, ILogger<ModuleGate> logger, Func<DateTime> clock)
    {
        _scopeFactory = scopeFactory;
        _options = options;
        _logger = logger;
        _clock = clock;
    }

    public async Task<bool> IsEnabledAsync(string moduleKey, CancellationToken ct = default)
    {
        if (!ModuleKeys.IsKnown(moduleKey)) return true;
        var statuses = await GetStatusesAsync(ct);
        var status = statuses.FirstOrDefault(s => s.Key == moduleKey);
        return status?.IsEnabled ?? true;
    }

    public async Task<IReadOnlyList<ModuleStatus>> GetStatusesAsync(CancellationToken ct = default)
    {
        var current = _snapshot;
        if (current is not null && !IsStale(current))
            return current.Statuses;

        await _loadLock.WaitAsync(ct);
        try
        {
            current = _snapshot;
            if (current is not null && !IsStale(current))
                return current.Statuses;

            try
            {
                var statuses = await LoadAsync(ct);
                _snapshot = new Snapshot(statuses, _clock());
                return statuses;
            }
            catch (OperationCanceledException) when (ct.IsCancellationRequested)
            {
                throw;
            }
            catch (Exception ex)
            {
                // Fail open: keep serving the last known state, or "everything on" if we never had one.
                _logger.LogError(ex, "Module license lookup failed; failing open.");
                var fallback = current?.Statuses ?? AllActive();
                _snapshot = new Snapshot(fallback, _clock());
                return fallback;
            }
        }
        finally
        {
            _loadLock.Release();
        }
    }

    public void Invalidate() => _snapshot = null;

    private bool IsStale(Snapshot s)
        => (_clock() - s.LoadedAtUtc).TotalSeconds >= Math.Max(0, _options.CacheSeconds);

    private async Task<IReadOnlyList<ModuleStatus>> LoadAsync(CancellationToken ct)
    {
        using var scope = _scopeFactory.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<IApplicationDbContext>();
        var rows = await db.ModuleLicenses.AsNoTracking().ToListAsync(ct);
        var byKey = rows.GroupBy(r => r.ModuleKey).ToDictionary(g => g.Key, g => g.First());
        var now = _clock();

        return ModuleKeys.All
            .Select(key => ModuleEvaluator.Evaluate(key, byKey.GetValueOrDefault(key), now, _options.GraceDays))
            .ToList();
    }

    private IReadOnlyList<ModuleStatus> AllActive()
        => ModuleKeys.All.Select(k => ModuleEvaluator.Evaluate(k, null, _clock(), _options.GraceDays)).ToList();
}
