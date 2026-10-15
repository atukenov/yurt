using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;
using Yurt.Application.Common.Interfaces;

namespace Yurt.Application.Features.Push.Services;

/// <summary>Sends a remote push to every device a customer has registered, pruning any
/// token FCM reports as dead. Never lets a push failure propagate — this runs inline from
/// order-status transitions, and a notification problem must never fail that request.</summary>
public class PushNotificationService
{
    private readonly IApplicationDbContext _db;
    private readonly IPushNotificationSender _sender;
    private readonly FirebaseOptions _options;
    private readonly ILogger<PushNotificationService> _logger;

    public PushNotificationService(
        IApplicationDbContext db,
        IPushNotificationSender sender,
        FirebaseOptions options,
        ILogger<PushNotificationService> logger)
    {
        _db = db;
        _sender = sender;
        _options = options;
        _logger = logger;
    }

    public async Task SendOrderStatusPushAsync(
        Guid customerUserId,
        string title,
        string body,
        IReadOnlyDictionary<string, string>? data = null,
        CancellationToken ct = default)
    {
        if (!_options.Enabled) return;

        try
        {
            var tokens = await _db.CustomerDeviceTokens
                .Where(t => t.CustomerUserId == customerUserId)
                .Select(t => t.Token)
                .ToListAsync(ct);
            if (tokens.Count == 0)
            {
                _logger.LogInformation(
                    "No registered device tokens for customer {CustomerUserId} — skipping push.", customerUserId);
                return;
            }

            _logger.LogInformation(
                "Sending push to {Count} device(s) for customer {CustomerUserId}.", tokens.Count, customerUserId);
            var result = await _sender.SendAsync(tokens, title, body, data, ct);
            if (result.DeadTokens.Count == 0) return;

            _logger.LogInformation(
                "Pruning {Count} dead device token(s) for customer {CustomerUserId}.",
                result.DeadTokens.Count, customerUserId);
            var deadTokens = await _db.CustomerDeviceTokens
                .Where(t => result.DeadTokens.Contains(t.Token))
                .ToListAsync(ct);
            _db.CustomerDeviceTokens.RemoveRange(deadTokens);
            await _db.SaveChangesAsync(ct);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to send push notification to customer {CustomerUserId}.", customerUserId);
        }
    }
}
