namespace Yurt.Application.Features.Tenants;

/// <summary>
/// A sign-in token is only good on the cafe it was issued for. Tokens issued before cafes existed carry no tenant
/// and belong to the default cafe. Platform operators (SuperAdmin) work across cafes.
/// </summary>
public static class TenantTokenPolicy
{
    public static bool IsAllowed(string? tokenTenantClaim, Guid requestTenantId, bool isSuperAdmin)
    {
        if (isSuperAdmin) return true;
        var tokenTenant = Guid.TryParse(tokenTenantClaim, out var parsed) ? parsed : Domain.Entities.Tenant.DefaultId;
        return tokenTenant == requestTenantId;
    }
}
