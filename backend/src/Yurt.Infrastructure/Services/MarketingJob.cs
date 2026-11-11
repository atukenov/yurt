using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
using Yurt.Application.Features.Marketing;

namespace Yurt.Infrastructure.Services;

/// <summary>
/// Sends the automatic guest messages (come back, birthday, thanks after the first order) every 15 minutes for every
/// cafe (<see cref="MarketingService.RunAutomaticAsync"/>). A cafe without automatic messages costs one small lookup.
/// </summary>
public class MarketingJob(IServiceProvider services, ILogger<MarketingJob> logger) : BackgroundService
{
    private static readonly TimeSpan Interval = TimeSpan.FromMinutes(15);

    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        // Let the app finish starting (migrations, seeding) first.
        try { await Task.Delay(TimeSpan.FromMinutes(1), stoppingToken); }
        catch (OperationCanceledException) { return; }

        while (!stoppingToken.IsCancellationRequested)
        {
            try
            {
                await TenantJobs.ForEachTenantAsync(
                    services.GetRequiredService<IServiceScopeFactory>(), logger,
                    (sp, ct) => sp.GetRequiredService<MarketingService>().RunAutomaticAsync(DateTime.UtcNow, ct),
                    stoppingToken);
            }
            catch (OperationCanceledException) when (stoppingToken.IsCancellationRequested)
            {
                return;
            }
            catch (Exception ex)
            {
                logger.LogError(ex, "Guest messages run failed");
            }

            try { await Task.Delay(Interval, stoppingToken); }
            catch (OperationCanceledException) { return; }
        }
    }
}
