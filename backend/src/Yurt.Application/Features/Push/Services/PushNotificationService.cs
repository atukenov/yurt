using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;
using Yurt.Application.Common.Interfaces;
using Yurt.Domain;

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
    private readonly IModuleGate _modules;

    public PushNotificationService(
        IApplicationDbContext db,
        IPushNotificationSender sender,
        FirebaseOptions options,
        ILogger<PushNotificationService> logger,
        IModuleGate modules)
    {
        _db = db;
        _sender = sender;
        _options = options;
        _logger = logger;
        _modules = modules;
    }

    public Task SendOrderStatusPushAsync(
        Guid customerUserId,
        string title,
        string body,
        IReadOnlyDictionary<string, string>? data = null,
        CancellationToken ct = default)
        => SendToCustomersAsync([customerUserId], title, body, data, ct);

    /// <summary>True when remote pushes can go out at all: Firebase is set up and the push module is on.</summary>
    public async Task<bool> IsAvailableAsync(CancellationToken ct = default)
        => _options.Enabled && await _modules.IsEnabledAsync(ModuleKeys.Push, ct);

    /// <summary>
    /// Sends one guest a message on each of their phones in that phone's app language (guest messages, marketing).
    /// <paramref name="textFor"/> gets the phone's language (ru, kk, en or null) and returns the title and text.
    /// Returns false when the guest has no phone registered, so nothing was sent. Dead tokens are pruned.
    /// The caller checks <see cref="IsAvailableAsync"/> first.
    /// </summary>
    public async Task<bool> SendInLanguageAsync(
        Guid customerUserId,
        Func<string?, (string Title, string Body)> textFor,
        IReadOnlyDictionary<string, string>? data = null,
        CancellationToken ct = default)
    {
        var tokens = await _db.CustomerDeviceTokens
            .Where(t => t.CustomerUserId == customerUserId)
            .Select(t => new { t.Token, t.Language })
            .ToListAsync(ct);
        if (tokens.Count == 0) return false;

        var dead = new List<string>();
        foreach (var group in tokens.GroupBy(t => t.Language))
        {
            var (title, body) = textFor(group.Key);
            try
            {
                var result = await _sender.SendAsync(group.Select(t => t.Token).ToList(), title, body, data, ct);
                dead.AddRange(result.DeadTokens);
            }
            catch (Exception ex) when (ex is not OperationCanceledException)
            {
                _logger.LogError(ex, "Failed to send a guest message push.");
            }
        }
        if (dead.Count > 0)
        {
            var deadRows = await _db.CustomerDeviceTokens.Where(t => dead.Contains(t.Token)).ToListAsync(ct);
            _db.CustomerDeviceTokens.RemoveRange(deadRows);
            await _db.SaveChangesAsync(ct);
        }
        return true;
    }

    /// <summary>Sends one push to every device of the given customers (for example staff who use the cafe app
    /// on their phone and get stock alerts on it). Same rules as order pushes: a no-op while Firebase or the push
    /// module is off, dead tokens are pruned, and a failure never propagates.</summary>
    public async Task SendToCustomersAsync(
        IReadOnlyCollection<Guid> customerUserIds,
        string title,
        string body,
        IReadOnlyDictionary<string, string>? data = null,
        CancellationToken ct = default)
    {
        if (!_options.Enabled || customerUserIds.Count == 0) return;

        try
        {
            // No remote pushes while the push module is off; order updates still reach open apps over SignalR.
            if (!await _modules.IsEnabledAsync(ModuleKeys.Push, ct)) return;

            var tokens = await _db.CustomerDeviceTokens
                .Where(t => customerUserIds.Contains(t.CustomerUserId))
                .Select(t => t.Token)
                .ToListAsync(ct);
            if (tokens.Count == 0)
            {
                _logger.LogInformation(
                    "No registered device tokens for {Count} customer(s) — skipping push.", customerUserIds.Count);
                return;
            }

            _logger.LogInformation(
                "Sending push to {Count} device(s) for {Customers} customer(s).", tokens.Count, customerUserIds.Count);
            var result = await _sender.SendAsync(tokens, title, body, data, ct);
            if (result.DeadTokens.Count == 0) return;

            _logger.LogInformation("Pruning {Count} dead device token(s).", result.DeadTokens.Count);
            var deadTokens = await _db.CustomerDeviceTokens
                .Where(t => result.DeadTokens.Contains(t.Token))
                .ToListAsync(ct);
            _db.CustomerDeviceTokens.RemoveRange(deadTokens);
            await _db.SaveChangesAsync(ct);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to send push notification to {Count} customer(s).", customerUserIds.Count);
        }
    }
}
