namespace Yurt.Application.Features.AppUpdate;

/// <summary>
/// Configuration for the client-side forced-update gate ("AppUpdate" section).
/// A blank minimum version disables the gate for that platform — the app never
/// blocks unless an operator explicitly sets a minimum version here.
/// </summary>
public class AppUpdateOptions
{
    /// <summary>Minimum required app version on iOS, e.g. "5.1.0". Blank disables the gate.</summary>
    public string MinVersionIos { get; set; } = string.Empty;

    /// <summary>Minimum required app version on Android, e.g. "5.1.0". Blank disables the gate.</summary>
    public string MinVersionAndroid { get; set; } = string.Empty;

    /// <summary>App Store URL shown when the iOS gate triggers.</summary>
    public string StoreUrlIos { get; set; } = string.Empty;

    /// <summary>Play Store URL shown when the Android gate triggers.</summary>
    public string StoreUrlAndroid { get; set; } = string.Empty;
}
