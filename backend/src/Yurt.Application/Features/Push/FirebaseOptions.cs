namespace Yurt.Application.Features.Push;

/// <summary>
/// Configuration for sending remote push notifications via Firebase Cloud Messaging
/// ("Firebase" section). The service account JSON is a secret and must come from an
/// environment variable, never from committed config.
/// </summary>
public class FirebaseOptions
{
    /// <summary>Master feature flag. When false, no Firebase calls are made and
    /// <see cref="Services.PushNotificationService"/> is a no-op.</summary>
    public bool Enabled { get; set; } = false;

    public string ProjectId { get; set; } = string.Empty;

    /// <summary>Full service-account JSON (Firebase Console → Project Settings → Service
    /// Accounts → Generate new private key). Must come from an env var, never committed.</summary>
    public string ServiceAccountJson { get; set; } = string.Empty;
}
