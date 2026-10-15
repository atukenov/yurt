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

    public WhatsAppOtpSender(HttpClient http, OtpOptions options)
    {
        _http = http;
        _options = options;
        _http.Timeout = TimeSpan.FromSeconds(15);
    }

    private record SendMessageRequest(string ChatId, string Message);

    public async Task SendAsync(string mobileNumber, string code, CancellationToken ct = default)
    {
        var url = $"{_options.GreenApiBaseUrl.TrimEnd('/')}/waInstance{_options.IdInstance}/sendMessage/{_options.ApiTokenInstance}";
        var chatId = ToChatId(mobileNumber);
        var message = _options.MessageTemplate.Replace("{code}", code);

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
