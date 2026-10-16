using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
using Yurt.Application.Common.Interfaces;
using Yurt.Domain;
using Yurt.Application.Features.IikoIntegration.Services;
using Yurt.Application.Features.Loyalty;

namespace Yurt.Infrastructure.Services;

/// <summary>
/// Background retry for iiko order-push/close operations that failed while iiko was
/// unavailable — mirrors <see cref="LoyaltyRetryService"/>'s loop shape exactly. Gated by
/// both <see cref="IikoOptions.Enabled"/> and <see cref="IikoOptions.PushOrdersEnabled"/>,
/// so order-push can stay off even while loyalty is on.
/// </summary>
public class IikoOrderSyncRetryService : BackgroundService
{
    private static readonly TimeSpan RetryInterval = TimeSpan.FromMinutes(5);

    private readonly IServiceProvider _services;
    private readonly IikoOptions _options;
    private readonly ILogger<IikoOrderSyncRetryService> _logger;

    public IikoOrderSyncRetryService(
        IServiceProvider services, IikoOptions options, ILogger<IikoOrderSyncRetryService> logger)
    {
        _services = services;
        _options = options;
        _logger = logger;
    }

    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        if (!_options.Enabled || !_options.PushOrdersEnabled) return;

        while (!stoppingToken.IsCancellationRequested)
        {
            try
            {
                // Module switched off (or license lapsed past its grace period): do no work, never delete data.
                var gate = _services.GetRequiredService<IModuleGate>();
                if (!await gate.IsEnabledAsync(ModuleKeys.IikoSync, stoppingToken))
                {
                    await Task.Delay(RetryInterval, stoppingToken);
                    continue;
                }

                await TenantJobs.ForEachTenantAsync(
                    _services.GetRequiredService<IServiceScopeFactory>(), _logger,
                    (sp, ct) => sp.GetRequiredService<IikoOrderSyncService>().RetryPendingAsync(ct),
                    stoppingToken);
            }
            catch (OperationCanceledException) when (stoppingToken.IsCancellationRequested)
            {
                return;
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "iiko order-sync retry failed");
            }

            try { await Task.Delay(RetryInterval, stoppingToken); }
            catch (OperationCanceledException) { return; }
        }
    }
}
