namespace Yurt.Application.Common.Helpers;

public static class LocalizationHelper
{
    public static string Localize(string defaultVal, string? ru, string? kk, string lang) =>
        lang switch
        {
            "ru" => ru ?? defaultVal,
            "kk" => kk ?? defaultVal,
            _ => defaultVal,
        };
}
