using FirebaseAdmin;
using FirebaseAdmin.Messaging;
using Google.Apis.Auth.OAuth2;
using Microsoft.Extensions.Logging;
using Yurt.Application.Common.Interfaces;
using Yurt.Application.Features.Push;

namespace Yurt.Infrastructure.Push;

/// <summary>FCM/APNs sender backed by the Firebase Admin SDK. Registered as a singleton
/// so the lazily-built <see cref="FirebaseApp"/> (and its credential handshake) happens
/// at most once per process. Disabled (<see cref="FirebaseOptions.Enabled"/> = false) or
/// missing credentials degrade to a no-op rather than throwing — this feature ships
/// before real Firebase credentials exist.</summary>
public class FcmPushNotificationSender : IPushNotificationSender
{
    private readonly FirebaseOptions _options;
    private readonly ILogger<FcmPushNotificationSender> _logger;
    private readonly object _initLock = new();
    private FirebaseApp? _app;
    private bool _initFailed;

    public FcmPushNotificationSender(FirebaseOptions options, ILogger<FcmPushNotificationSender> logger)
    {
        _options = options;
        _logger = logger;
    }

    public async Task<PushSendResult> SendAsync(
        IReadOnlyList<string> tokens,
        string title,
        string body,
        IReadOnlyDictionary<string, string>? data,
        CancellationToken ct = default)
    {
        if (tokens.Count == 0) return new PushSendResult([]);

        var app = GetOrCreateApp();
        if (app == null) return new PushSendResult([]);

        try
        {
            var message = new MulticastMessage
            {
                Tokens = tokens.ToList(),
                Notification = new Notification { Title = title, Body = body },
                Data = data,
            };

            var response = await FirebaseMessaging.GetMessaging(app).SendEachForMulticastAsync(message, ct);
            _logger.LogInformation(
                "FCM send: {SuccessCount} succeeded, {FailureCount} failed (of {Count}).",
                response.SuccessCount, response.FailureCount, tokens.Count);

            var dead = new List<string>();
            for (var i = 0; i < response.Responses.Count; i++)
            {
                var sendResponse = response.Responses[i];
                if (sendResponse.IsSuccess) continue;

                var errorCode = sendResponse.Exception?.MessagingErrorCode;
                if (errorCode is MessagingErrorCode.Unregistered or MessagingErrorCode.InvalidArgument)
                {
                    dead.Add(tokens[i]);
                }
                else
                {
                    _logger.LogWarning(sendResponse.Exception, "FCM send failed for one token: {ErrorCode}.", errorCode);
                }
            }
            return new PushSendResult(dead);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "FCM send failed for {Count} token(s).", tokens.Count);
            return new PushSendResult([]);
        }
    }

    private FirebaseApp? GetOrCreateApp()
    {
        if (_app != null) return _app;
        if (_initFailed) return null;
        if (!_options.Enabled || string.IsNullOrWhiteSpace(_options.ServiceAccountJson))
        {
            _logger.LogInformation("Push notifications disabled or not configured — skipping FCM send.");
            _initFailed = true;
            return null;
        }

        lock (_initLock)
        {
            if (_app != null) return _app;
            if (_initFailed) return null;
            try
            {
                _app = FirebaseApp.Create(new AppOptions
                {
                    Credential = GoogleCredential.FromJson(_options.ServiceAccountJson),
                    ProjectId = _options.ProjectId,
                });
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Failed to initialize FirebaseApp for push notifications.");
                _initFailed = true;
            }
            return _app;
        }
    }
}
