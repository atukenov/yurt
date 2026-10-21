namespace Yurt.Application.Features.Fiscal;

/// <summary>Links to an order's official tax receipt on the provider's site, for the customer and the printed receipt.</summary>
public static class FiscalLinks
{
    /// <summary>
    /// The link as given when it is a plain web address (http or https), else null. The link comes from the cash
    /// register service, so anything else (a script link, a broken value) is never shown to a guest or printed.
    /// </summary>
    public static string? Safe(string? url)
    {
        var trimmed = url?.Trim();
        return Uri.TryCreate(trimmed, UriKind.Absolute, out var uri)
            && (uri.Scheme == Uri.UriSchemeHttps || uri.Scheme == Uri.UriSchemeHttp)
            ? trimmed
            : null;
    }
}
