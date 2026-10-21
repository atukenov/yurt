using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
using Yurt.Application.Features.Printing;

namespace Yurt.Infrastructure.Services;

/// <summary>
/// Sends queued kitchen tickets and receipts to the network printers of every cafe (own_pos). Wakes as soon as a job is
/// queued, and every few seconds anyway to retry jobs whose printer was not reachable.
/// </summary>
public class PrintJobService(IServiceScopeFactory scopes, PrintQueueSignal signal, ILogger<PrintJobService> logger) : BackgroundService
{
    private static readonly TimeSpan RetryInterval = TimeSpan.FromSeconds(15);

    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        while (!stoppingToken.IsCancellationRequested)
        {
            try
            {
                await TenantJobs.ForEachTenantAsync(scopes, logger,
                    (sp, ct) => sp.GetRequiredService<PrintService>().DispatchPendingAsync(ct), stoppingToken);
                await signal.WaitAsync(RetryInterval, stoppingToken);
            }
            catch (OperationCanceledException) when (stoppingToken.IsCancellationRequested)
            {
                return;
            }
            catch (Exception ex)
            {
                logger.LogError(ex, "Print sender failed");
                try { await Task.Delay(RetryInterval, stoppingToken); }
                catch (OperationCanceledException) { return; }
            }
        }
    }
}
