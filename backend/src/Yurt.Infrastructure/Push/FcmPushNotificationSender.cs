using System.Collections.Concurrent;
using System.Security.Cryptography;
using System.Text;
using System.Text.Json;
using FirebaseAdmin;
using FirebaseAdmin.Messaging;
using Google.Apis.Auth.OAuth2;
using Microsoft.Extensions.Logging;
using Yurt.Application.Common.Interfaces;
using Yurt.Application.Features.Push;

namespace Yurt.Infrastructure.Push;

/// <summary>
/// One <see cref="FirebaseApp"/> per cafe credential, built lazily and kept for the life of the process (the credential
/// handshake happens once). The key is the cafe plus a hash of its service-account JSON, so replacing the JSON in the
/// Platform page makes the next send use the new one.
/// </summary>
public class FirebaseAppCache
{
    private readonly ConcurrentDictionary<string, FirebaseApp?> _apps = new();
    private readonly object _lock = new();

    public FirebaseApp? GetOrCreate(Guid tenantId, string serviceAccountJson, string fallbackProjectId, ILogger logger)
    {
        var key = $"{tenantId:N}:{Convert.ToHexString(SHA256.HashData(Encoding.UTF8.GetBytes(serviceAccountJson)))}";
        if (_apps.TryGetValue(key, out var existing)) return existing;
        lock (_lock)
        {
            if (_apps.TryGetValue(key, out existing)) return existing;
            FirebaseApp? app = null;
            try
            {
                app = FirebaseApp.Create(new AppOptions
                {
                    Credential = GoogleCredential.FromJson(serviceAccountJson),
                    ProjectId = ProjectIdOf(serviceAccountJson) ?? fallbackProjectId,
                }, name: key);
            }
            catch (Exception ex)
            {
                logger.LogError(ex, "Failed to initialize FirebaseApp for push notifications.");
            }
            _apps[key] = app;
            return app;
        }
    }

    private static string? ProjectIdOf(string json)
    {
        try
        {
            using var doc = JsonDocument.Parse(json);
            return doc.RootElement.TryGetProperty("project_id", out var p) ? p.GetString() : null;
        }
        catch (JsonException) { return null; }
    }
}

/// <summary>FCM/APNs sender backed by the Firebase Admin SDK, using the current cafe's own service account (the first
/// cafe falls back to the server-wide setting). Disabled (<see cref="FirebaseOptions.Enabled"/> = false) or missing
/// credentials degrade to a no-op rather than throwing; a cafe never sends through another cafe's Firebase project.</summary>
public class FcmPushNotificationSender : IPushNotificationSender
{
    public const string SecretKey = "Firebase:ServiceAccountJson";

    private readonly FirebaseOptions _options;
    private readonly ILogger<FcmPushNotificationSender> _logger;
    private readonly ITenantSecrets _secrets;
    private readonly ITenantContext _tenant;
    private readonly FirebaseAppCache _apps;

    public FcmPushNotificationSender(
        FirebaseOptions options, ILogger<FcmPushNotificationSender> logger,
        ITenantSecrets secrets, ITenantContext tenant, FirebaseAppCache apps)
    {
        _options = options;
        _logger = logger;
        _secrets = secrets;
        _tenant = tenant;
        _apps = apps;
    }

    public async Task<PushSendResult> SendAsync(
        IReadOnlyList<string> tokens,
        string title,
        string body,
        IReadOnlyDictionary<string, string>? data,
        CancellationToken ct = default)
    {
        if (tokens.Count == 0) return new PushSendResult([]);

        var app = await GetAppAsync(ct);
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

    private async Task<FirebaseApp?> GetAppAsync(CancellationToken ct)
    {
        var json = _options.Enabled ? await _secrets.GetAsync(SecretKey, ct) : null;
        if (string.IsNullOrWhiteSpace(json))
        {
            _logger.LogInformation("Push notifications disabled or not configured for this cafe — skipping FCM send.");
            return null;
        }
        return _apps.GetOrCreate(_tenant.TenantId, json, _options.ProjectId, _logger);
    }
}
