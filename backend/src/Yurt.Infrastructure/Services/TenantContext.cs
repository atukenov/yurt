using Yurt.Application.Common;
using Yurt.Application.Common.Interfaces;
using Yurt.Domain.Entities;

namespace Yurt.Infrastructure.Services;

public class TenantContext : ITenantContext
{
    public Guid TenantId { get; private set; } = Tenant.DefaultId;
    public string Slug { get; private set; } = Tenant.DefaultSlug;

    public void Set(Guid tenantId, string slug)
    {
        TenantId = tenantId;
        Slug = slug;
        TenantAmbient.Current = tenantId;
    }
}
