namespace Yurt.Infrastructure.Monitoring;

/// <summary>
/// Error alerts to a Telegram chat. Off until both <see cref="TelegramBotToken"/> and <see cref="TelegramChatId"/>
/// are set (env vars <c>Alerts__TelegramBotToken</c>, <c>Alerts__TelegramChatId</c>). See docs/monitoring.md.
/// </summary>
public sealed class AlertOptions
{
    public string? TelegramBotToken { get; set; }
    public string? TelegramChatId { get; set; }

    /// <summary>Shown in every alert, so staging and production can share one chat. Defaults to the environment name.</summary>
    public string? ServerName { get; set; }

    /// <summary>The same error is sent at most once per this many minutes; repeats are counted and reported with the next one.</summary>
    public int RepeatMinutes { get; set; } = 10;

    /// <summary>Hard cap so a broken release cannot flood the chat.</summary>
    public int MaxPerHour { get; set; } = 20;

    /// <summary>Send "server started" after each deploy or restart; an unexpected one means the server crashed.</summary>
    public bool NotifyOnStart { get; set; } = true;

    public string TelegramApiBaseUrl { get; set; } = "https://api.telegram.org";

    public bool Enabled => !string.IsNullOrWhiteSpace(TelegramBotToken) && !string.IsNullOrWhiteSpace(TelegramChatId);
}
