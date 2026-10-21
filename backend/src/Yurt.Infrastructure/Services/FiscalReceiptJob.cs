using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
using Yurt.Application.Features.Fiscal;

namespace Yurt.Infrastructure.Services;

/// <summary>
/// Sends official tax receipts in the background, once a minute, for every cafe that has a fiscal provider set
/// (<see cref="FiscalReceiptService"/>). A cafe with receipts off costs one small lookup per run.
/// </summary>
public class FiscalReceiptJob(IServiceProvider services, ILogger<FiscalReceiptJob> logger) : BackgroundService
{
    private static readonly TimeSpan Interval = TimeSpan.FromMinutes(1);

    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        while (!stoppingToken.IsCancellationRequested)
        {
            try
            {
                await TenantJobs.ForEachTenantAsync(
                    services.GetRequiredService<IServiceScopeFactory>(), logger,
                    (sp, ct) => sp.GetRequiredService<FiscalReceiptService>().RunAsync(ct),
                    stoppingToken);
            }
            catch (OperationCanceledException) when (stoppingToken.IsCancellationRequested)
            {
                return;
            }
            catch (Exception ex)
            {
                logger.LogError(ex, "Fiscal receipt run failed");
            }

            try { await Task.Delay(Interval, stoppingToken); }
            catch (OperationCanceledException) { return; }
        }
    }
}
