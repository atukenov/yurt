namespace Yurt.Application.Common.Interfaces;

/// <summary>Low-level FCM/APNs transport. Never throws for "not configured" or
/// delivery-failure cases — callers get a result they can act on instead.</summary>
public interface IPushNotificationSender
{
    /// <summary>Send the same notification to a batch of device tokens.</summary>
    Task<PushSendResult> SendAsync(
        IReadOnlyList<string> tokens,
        string title,
        string body,
        IReadOnlyDictionary<string, string>? data,
        CancellationToken ct = default);
}

/// <summary>Which of the sent tokens FCM reported as permanently invalid
/// (unregistered/invalid) — callers should stop using them.</summary>
public record PushSendResult(IReadOnlyList<string> DeadTokens);
