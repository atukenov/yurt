using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
using Yurt.Application.Common.Interfaces;
using Yurt.Domain.Enums;

namespace Yurt.Infrastructure.Services;

public class OrderArchivalService : BackgroundService
{
    private readonly IServiceScopeFactory _scopeFactory;
    private readonly ILogger<OrderArchivalService> _logger;

    public OrderArchivalService(IServiceScopeFactory scopeFactory, ILogger<OrderArchivalService> logger)
    {
        _scopeFactory = scopeFactory;
        _logger = logger;
    }

    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        await ArchiveOldOrdersAsync(stoppingToken);

        using var timer = new PeriodicTimer(TimeSpan.FromHours(24));
        while (await timer.WaitForNextTickAsync(stoppingToken))
        {
            await ArchiveOldOrdersAsync(stoppingToken);
        }
    }

    private async Task ArchiveOldOrdersAsync(CancellationToken ct)
    {
        using var scope = _scopeFactory.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<IApplicationDbContext>();

        var cutoff = DateTime.UtcNow.AddDays(-90);
        var now = DateTime.UtcNow;

        var archived = await db.Orders
            .Where(o => o.Status == OrderStatus.Completed && o.CreatedAt < cutoff && !o.IsArchived)
            .ExecuteUpdateAsync(s => s
                .SetProperty(o => o.IsArchived, true)
                .SetProperty(o => o.ArchivedAt, now), ct);

        if (archived > 0)
            _logger.LogInformation("Archived {Count} orders older than 90 days.", archived);
    }
}
