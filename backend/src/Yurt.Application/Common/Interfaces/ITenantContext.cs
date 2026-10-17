namespace Yurt.Application.Common.Interfaces;

/// <summary>The cafe the current request belongs to. Always set: with a single cafe it is the default tenant.</summary>
public interface ITenantContext
{
    Guid TenantId { get; }
    string Slug { get; }

    /// <summary>Called once per request by the tenant middleware.</summary>
    void Set(Guid tenantId, string slug);
}

/// <summary>Active tenants, cached for a short time, so each request does not query the database.</summary>
public interface ITenantDirectory
{
    Task<IReadOnlyList<Domain.Entities.Tenant>> GetActiveAsync(CancellationToken ct = default);

    /// <summary>Same list without awaiting, for the CORS check; loads (and blocks) only when the cache is empty.</summary>
    IReadOnlyList<Domain.Entities.Tenant> GetActive();

    /// <summary>Forget the cached list, for example after a cafe was created or switched off.</summary>
    void Invalidate();
}
