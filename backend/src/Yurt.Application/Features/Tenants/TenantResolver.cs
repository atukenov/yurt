using Yurt.Domain.Entities;

namespace Yurt.Application.Features.Tenants;

/// <summary>
/// Matches a request to a cafe. In order: the cafe slug the app sends in the X-Tenant header (branded native apps and
/// single-host deployments), the host of the Origin header (a cafe's web app calling a shared API host), then the
/// host the request itself arrived on (a cafe with its own API address). Fail-open like the module gates: nothing
/// matching, an inactive tenant or an empty table all fall back to the default tenant, so a configuration slip can
/// never take the current cafe offline.
/// </summary>
public static class TenantResolver
{
    public const string HeaderName = "X-Tenant";

    public static (Guid Id, string Slug) Resolve(
        IReadOnlyList<Tenant> activeTenants, string? host, string? origin = null, string? tenantHeader = null)
    {
        if (!string.IsNullOrWhiteSpace(tenantHeader))
        {
            var slug = tenantHeader.Trim();
            var bySlug = activeTenants.FirstOrDefault(t => string.Equals(t.Slug, slug, StringComparison.OrdinalIgnoreCase));
            if (bySlug is not null) return (bySlug.Id, bySlug.Slug);
        }

        var originHost = OriginHost(origin);
        if (originHost is not null && ByDomain(activeTenants, originHost) is { } byOrigin)
            return (byOrigin.Id, byOrigin.Slug);

        if (!string.IsNullOrWhiteSpace(host) && ByDomain(activeTenants, host.Trim()) is { } byHost)
            return (byHost.Id, byHost.Slug);

        var fallback = activeTenants.FirstOrDefault(t => t.Id == Tenant.DefaultId);
        return (Tenant.DefaultId, fallback?.Slug ?? Tenant.DefaultSlug);
    }

    /// <summary>True when the origin is a web address of one of the active cafes (any scheme and port).</summary>
    public static bool IsCafeOrigin(IReadOnlyList<Tenant> activeTenants, string? origin)
        => OriginHost(origin) is { } host && ByDomain(activeTenants, host) is not null;

    private static Tenant? ByDomain(IReadOnlyList<Tenant> tenants, string host)
        => tenants.FirstOrDefault(t => t.Domain is not null && string.Equals(t.Domain, host, StringComparison.OrdinalIgnoreCase));

    private static string? OriginHost(string? origin)
        => !string.IsNullOrWhiteSpace(origin) && Uri.TryCreate(origin.Trim(), UriKind.Absolute, out var uri) ? uri.Host : null;
}
