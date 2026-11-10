namespace Yurt.Application.Common.Interfaces;

/// <summary>Sends staff watch alerts and reports through the cafe's own Telegram bot.</summary>
public interface IWatchTelegram
{
    Task<bool> SendAsync(string botToken, string chatId, string text, CancellationToken ct = default);

    /// <summary>The chat that last wrote to the bot (the owner sends it any message first), or null.</summary>
    Task<string?> LatestChatAsync(string botToken, CancellationToken ct = default);
}
