using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
using Yurt.Application.Common.Interfaces;
using Yurt.Domain.Enums;

namespace Yurt.Infrastructure.Services;

public class OrderTimerService : BackgroundService
{
    private readonly IServiceScopeFactory _scopeFactory;
    private readonly ILogger<OrderTimerService> _logger;

    public OrderTimerService(IServiceScopeFactory scopeFactory, ILogger<OrderTimerService> logger)
    {
        _scopeFactory = scopeFactory;
        _logger = logger;
    }

    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        using var timer = new PeriodicTimer(TimeSpan.FromSeconds(30));
        while (await timer.WaitForNextTickAsync(stoppingToken))
        {
            try { await DoWorkAsync(stoppingToken); }
            catch (Exception ex) { _logger.LogError(ex, "OrderTimerService tick failed"); }
        }
    }

    private async Task DoWorkAsync(CancellationToken ct)
    {
        using var scope = _scopeFactory.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<IApplicationDbContext>();
        var hub = scope.ServiceProvider.GetRequiredService<IOrdersHubService>();
        var now = DateTime.UtcNow;

        // Pass 1: Accepted + Paid → Preparing after 2 min
        var toStart = await db.Orders
            .Where(o => o.Status == OrderStatus.Accepted
                     && o.PaymentStatus == PaymentStatus.Paid
                     && o.AcceptedAt != null
                     && o.AcceptedAt <= now.AddMinutes(-2))
            .ToListAsync(ct);

        foreach (var order in toStart)
            order.Status = OrderStatus.Preparing;

        if (toStart.Count > 0) await db.SaveChangesAsync(ct);
        foreach (var order in toStart) await hub.NotifyOrderUpdatedAsync(order, ct);

        // Load active Preparing orders for timer passes (in-memory filtering avoids EF date-arithmetic translation)
        var preparing = await db.Orders
            .Where(o => o.Status == OrderStatus.Preparing
                     && o.AcceptedAt != null
                     && o.EtaMinutes != null)
            .ToListAsync(ct);

        // Pass 2: 5-min reminder
        var needs5Min = preparing
            .Where(o => !o.EtaNotif5MinSent
                     && o.EtaMinutes > 5
                     && o.AcceptedAt!.Value.AddMinutes(o.EtaMinutes!.Value - 5) <= now)
            .ToList();

        foreach (var order in needs5Min)
            order.EtaNotif5MinSent = true;

        if (needs5Min.Count > 0) await db.SaveChangesAsync(ct);
        foreach (var order in needs5Min)
            await hub.NotifyEtaReminderAsync(order.Id, order.CustomerUserId, 5, ct);

        // Pass 3: 1-min reminder
        var needs1Min = preparing
            .Where(o => !o.EtaNotif1MinSent
                     && o.EtaMinutes > 1
                     && o.AcceptedAt!.Value.AddMinutes(o.EtaMinutes!.Value - 1) <= now)
            .ToList();

        foreach (var order in needs1Min)
            order.EtaNotif1MinSent = true;

        if (needs1Min.Count > 0) await db.SaveChangesAsync(ct);
        foreach (var order in needs1Min)
            await hub.NotifyEtaReminderAsync(order.Id, order.CustomerUserId, 1, ct);

        // Pass 4: Preparing → Ready when ETA expires
        var toReady = preparing
            .Where(o => o.AcceptedAt!.Value.AddMinutes(o.EtaMinutes!.Value) <= now)
            .ToList();

        foreach (var order in toReady)
            order.Status = OrderStatus.Ready;

        if (toReady.Count > 0) await db.SaveChangesAsync(ct);
        foreach (var order in toReady) await hub.NotifyOrderUpdatedAsync(order, ct);
    }
}
