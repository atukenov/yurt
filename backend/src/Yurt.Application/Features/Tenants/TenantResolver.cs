using Yurt.Domain.Entities;

namespace Yurt.Application.Features.Tenants;

/// <summary>
/// Matches a request host to a cafe. Fail-open like the module gates: an unknown host, an inactive tenant or an empty
/// table all fall back to the default tenant, so a configuration slip can never take the current cafe offline.
/// </summary>
public static class TenantResolver
{
    public static (Guid Id, string Slug) Resolve(IReadOnlyList<Tenant> activeTenants, string? host)
    {
        if (!string.IsNullOrWhiteSpace(host))
        {
            var name = host.Trim().ToLowerInvariant();
            var match = activeTenants.FirstOrDefault(t =>
                t.Domain is not null && string.Equals(t.Domain, name, StringComparison.OrdinalIgnoreCase));
            if (match is not null) return (match.Id, match.Slug);
        }
        var fallback = activeTenants.FirstOrDefault(t => t.Id == Tenant.DefaultId);
        return (Tenant.DefaultId, fallback?.Slug ?? Tenant.DefaultSlug);
    }
}
