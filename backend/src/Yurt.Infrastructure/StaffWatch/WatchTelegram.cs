using System.Net.Http.Json;
using System.Text.Json;
using Microsoft.Extensions.Logging;
using Yurt.Application.Common.Interfaces;

namespace Yurt.Infrastructure.StaffWatch;

/// <summary>
/// Telegram for staff watch, through the cafe's own bot. Uses its own HttpClient on purpose: the shared client factory
/// logs request URLs, and Telegram's URL holds the token.
/// </summary>
public sealed class WatchTelegram(ILogger<WatchTelegram> logger, HttpMessageHandler? handler = null) : IWatchTelegram, IDisposable
{
    private const string Api = "https://api.telegram.org";
    private const int MaxLength = 4000;

    private readonly HttpClient _http = new(handler ?? new SocketsHttpHandler(), disposeHandler: handler is null)
    {
        Timeout = TimeSpan.FromSeconds(10),
    };

    public async Task<bool> SendAsync(string botToken, string chatId, string text, CancellationToken ct = default)
    {
        try
        {
            var body = new { chat_id = chatId, text = text.Length <= MaxLength ? text : text[..MaxLength] + "…", disable_web_page_preview = true };
            using var res = await _http.PostAsJsonAsync($"{Api}/bot{botToken}/sendMessage", body, ct);
            if (res.IsSuccessStatusCode) return true;
            logger.LogWarning("Staff watch: Telegram refused a message ({Status})", (int)res.StatusCode);
            return false;
        }
        catch (Exception e) when (e is HttpRequestException or TaskCanceledException)
        {
            logger.LogWarning("Staff watch: Telegram not reachable: {Message}", e.Message);
            return false;
        }
    }

    public async Task<string?> LatestChatAsync(string botToken, CancellationToken ct = default)
    {
        try
        {
            using var res = await _http.GetAsync($"{Api}/bot{botToken}/getUpdates?limit=50", ct);
            if (!res.IsSuccessStatusCode) return null;
            using var doc = JsonDocument.Parse(await res.Content.ReadAsStringAsync(ct));
            if (!doc.RootElement.TryGetProperty("result", out var results)) return null;
            string? chat = null;
            foreach (var u in results.EnumerateArray())
            {
                foreach (var kind in new[] { "message", "channel_post", "my_chat_member" })
                    if (u.TryGetProperty(kind, out var m) && m.TryGetProperty("chat", out var c) && c.TryGetProperty("id", out var id))
                        chat = id.GetRawText();
            }
            return chat;
        }
        catch (Exception e) when (e is HttpRequestException or TaskCanceledException or JsonException)
        {
            logger.LogWarning("Staff watch: could not read Telegram updates: {Message}", e.Message);
            return null;
        }
    }

    public void Dispose() => _http.Dispose();
}
