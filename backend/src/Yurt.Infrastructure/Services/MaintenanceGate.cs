using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using Yurt.Application.Common.Interfaces;
using Yurt.Application.Features.Platform;
using Yurt.Domain.Entities;

namespace Yurt.Infrastructure.Services;

public class MaintenanceGate : IMaintenanceGate
{
    private static readonly TimeSpan CacheFor = TimeSpan.FromSeconds(15);
    private static readonly MaintenanceDto Off = new(false, string.Empty);

    private readonly IServiceScopeFactory _scopeFactory;
    private readonly ILogger<MaintenanceGate> _logger;
    private volatile Snapshot? _snapshot;

    private sealed record Snapshot(MaintenanceDto Value, DateTime LoadedAtUtc);

    public MaintenanceGate(IServiceScopeFactory scopeFactory, ILogger<MaintenanceGate> logger)
    {
        _scopeFactory = scopeFactory;
        _logger = logger;
    }

    public async Task<MaintenanceDto> GetAsync(CancellationToken ct = default)
    {
        var current = _snapshot;
        if (current is not null && DateTime.UtcNow - current.LoadedAtUtc < CacheFor)
            return current.Value;

        try
        {
            using var scope = _scopeFactory.CreateScope();
            var db = scope.ServiceProvider.GetRequiredService<IApplicationDbContext>();
            var rows = await db.PlatformSettings.AsNoTracking()
                .Where(s => s.Key == PlatformSettingKeys.MaintenanceEnabled || s.Key == PlatformSettingKeys.MaintenanceMessage)
                .ToListAsync(ct);

            var enabled = rows.FirstOrDefault(r => r.Key == PlatformSettingKeys.MaintenanceEnabled)?.Value == "true";
            var message = rows.FirstOrDefault(r => r.Key == PlatformSettingKeys.MaintenanceMessage)?.Value ?? string.Empty;
            var value = new MaintenanceDto(enabled, message);
            _snapshot = new Snapshot(value, DateTime.UtcNow);
            return value;
        }
        catch (OperationCanceledException) when (ct.IsCancellationRequested)
        {
            throw;
        }
        catch (Exception ex)
        {
            // Fail open: a broken lookup must never lock customers out.
            _logger.LogError(ex, "Maintenance lookup failed; treating as not in maintenance.");
            var fallback = current?.Value ?? Off;
            _snapshot = new Snapshot(fallback, DateTime.UtcNow);
            return fallback;
        }
    }

    public void Invalidate() => _snapshot = null;
}
