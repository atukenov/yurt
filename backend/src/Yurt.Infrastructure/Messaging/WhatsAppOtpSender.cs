using System.Net.Http.Json;
using Yurt.Application.Common.Interfaces;
using Yurt.Application.Features.Auth;

namespace Yurt.Infrastructure.Messaging;

/// <summary>
/// Sends the registration OTP as a WhatsApp message through the Green API
/// (<c>{BaseUrl}/waInstance{IdInstance}/sendMessage/{ApiTokenInstance}</c>).
/// </summary>
public class WhatsAppOtpSender : IOtpSender
{
    private readonly HttpClient _http;
    private readonly OtpOptions _options;
    private readonly ITenantSecrets _secrets;

    public WhatsAppOtpSender(HttpClient http, OtpOptions options, ITenantSecrets secrets)
    {
        _http = http;
        _options = options;
        _secrets = secrets;
        _http.Timeout = TimeSpan.FromSeconds(15);
    }

    private record SendMessageRequest(string ChatId, string Message);

    public async Task SendAsync(string mobileNumber, string code, string? brandName = null, CancellationToken ct = default)
    {
        // Each cafe sends from its own WhatsApp instance; a cafe without one cannot send codes (it never borrows another cafe's).
        var idInstance = await _secrets.GetAsync("Otp:IdInstance", ct);
        var apiToken = await _secrets.GetAsync("Otp:ApiTokenInstance", ct);
        if (string.IsNullOrWhiteSpace(idInstance) || string.IsNullOrWhiteSpace(apiToken))
            throw new OtpSendException("WhatsApp is not configured for this cafe.");

        var url = $"{_options.GreenApiBaseUrl.TrimEnd('/')}/waInstance{idInstance}/sendMessage/{apiToken}";
        var chatId = ToChatId(mobileNumber);
        var message = _options.MessageTemplate
            .Replace("{code}", code)
            .Replace("{brand}", string.IsNullOrWhiteSpace(brandName) ? "Yurt" : brandName);

        try
        {
            var resp = await _http.PostAsJsonAsync(url, new SendMessageRequest(chatId, message), ct);
            if (!resp.IsSuccessStatusCode)
            {
                var body = await resp.Content.ReadAsStringAsync(ct);
                throw new OtpSendException(
                    $"Green API sendMessage failed with {(int)resp.StatusCode}: {Truncate(body)}");
            }
        }
        catch (Exception ex) when (ex is not OtpSendException)
        {
            throw new OtpSendException("Green API sendMessage request failed.", ex);
        }
    }

    /// <summary>Green API expects <c>{digits}@c.us</c> with no leading + or spaces.</summary>
    private static string ToChatId(string mobileNumber)
    {
        var digits = new string(mobileNumber.Where(char.IsDigit).ToArray());
        return $"{digits}@c.us";
    }

    private static string Truncate(string s) => s.Length <= 500 ? s : s[..500];
}
