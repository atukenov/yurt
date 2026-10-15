namespace Yurt.Application.Features.AppUpdate;

public class AppUpdateInfoDto
{
    public string MinVersionIos { get; set; } = string.Empty;
    public string MinVersionAndroid { get; set; } = string.Empty;
    public string StoreUrlIos { get; set; } = string.Empty;
    public string StoreUrlAndroid { get; set; } = string.Empty;

    /// <summary>Newest published release (blank when none). Drives the soft "what's new" popup.</summary>
    public string LatestVersion { get; set; } = string.Empty;
    public string LatestNotesEn { get; set; } = string.Empty;
    public string LatestNotesRu { get; set; } = string.Empty;
    public string LatestNotesKk { get; set; } = string.Empty;
}
