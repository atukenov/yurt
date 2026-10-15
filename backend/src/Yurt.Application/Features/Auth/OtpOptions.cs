namespace Yurt.Application.Features.Auth;

/// <summary>
/// Configuration for the WhatsApp one-time-password used during customer registration ("Otp" section).
/// Secrets (IdInstance, ApiTokenInstance) must come from environment variables, never committed config.
/// </summary>
public class OtpOptions
{
    /// <summary>
    /// When false no WhatsApp message is sent — the generated code is logged and returned
    /// in the start response as <c>devCode</c> so local/dev registration still works.
    /// </summary>
    public bool Enabled { get; set; } = false;

    /// <summary>Green API instance base URL, e.g. https://7201.api.green-api.com</summary>
    public string GreenApiBaseUrl { get; set; } = "https://api.green-api.com";

    /// <summary>Green API instance id (the number in <c>waInstance{IdInstance}</c>).</summary>
    public string IdInstance { get; set; } = string.Empty;

    /// <summary>Green API instance API token (the last path segment of the send URL).</summary>
    public string ApiTokenInstance { get; set; } = string.Empty;

    /// <summary>How long a code stays valid, in minutes.</summary>
    public int CodeExpiryMinutes { get; set; } = 15;

    /// <summary>Minimum seconds between two code requests for the same number.</summary>
    public int ResendCooldownSeconds { get; set; } = 60;

    /// <summary>Maximum wrong-code attempts before the code is rejected outright.</summary>
    public int MaxAttempts { get; set; } = 5;

    /// <summary><c>{code}</c> is replaced with the generated code.</summary>
    public string MessageTemplate { get; set; } = "Your Altyncup verification code: {code}";
}
