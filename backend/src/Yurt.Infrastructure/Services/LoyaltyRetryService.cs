using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
using Yurt.Application.Features.Loyalty;
using Yurt.Application.Features.Loyalty.Services;

namespace Yurt.Infrastructure.Services;

/// <summary>
/// Background loyalty reconciliation:
///  - every tick: retries iiko wallet operations (credit / chargeoff / hold release)
///    that failed while iiko was unavailable, so no hold is stranded, no spend goes
///    uncharged, and no earn goes uncredited;
///  - every SweepInterval: safety-net sweep for completed orders with no earn-credit
///    record at all (catches gaps outside the tracked-retry path);
///  - every WalletRevalidationInterval: proactively re-validates cached iiko wallet
///    ids for all linked customers, instead of only self-healing reactively.
/// </summary>
public class LoyaltyRetryService : BackgroundService
{
    private static readonly TimeSpan RetryInterval = TimeSpan.FromMinutes(5);
    private static readonly TimeSpan SweepInterval = TimeSpan.FromMinutes(30);
    private static readonly TimeSpan WalletRevalidationInterval = TimeSpan.FromHours(24);

    private readonly IServiceProvider _services;
    private readonly IikoOptions _options;
    private readonly ILogger<LoyaltyRetryService> _logger;

    private DateTime _nextSweepDueUtc = DateTime.UtcNow;
    private DateTime _nextWalletRevalidationDueUtc = DateTime.UtcNow;

    public LoyaltyRetryService(
        IServiceProvider services, IikoOptions options, ILogger<LoyaltyRetryService> logger)
    {
        _services = services;
        _options = options;
        _logger = logger;
    }

    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        if (!_options.Enabled) return;

        while (!stoppingToken.IsCancellationRequested)
        {
            try
            {
                await using var scope = _services.CreateAsyncScope();
                var loyalty = scope.ServiceProvider.GetRequiredService<LoyaltyService>();

                await loyalty.RetryPendingAsync(stoppingToken);

                var now = DateTime.UtcNow;

                if (now >= _nextSweepDueUtc)
                {
                    await loyalty.SweepMissedCreditsAsync(ct: stoppingToken);
                    _nextSweepDueUtc = now + SweepInterval;
                }

                if (now >= _nextWalletRevalidationDueUtc)
                {
                    await loyalty.RevalidateWalletLinksAsync(stoppingToken);
                    _nextWalletRevalidationDueUtc = now + WalletRevalidationInterval;
                }
            }
            catch (OperationCanceledException) when (stoppingToken.IsCancellationRequested)
            {
                return;
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Loyalty retry/sweep failed");
            }

            try { await Task.Delay(RetryInterval, stoppingToken); }
            catch (OperationCanceledException) { return; }
        }
    }
}
