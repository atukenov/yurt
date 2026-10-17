using Yurt.Application.Features.Tenants;
using Yurt.Domain.Entities;

namespace Yurt.UnitTests;

public class TenantTokenPolicyTests
{
    private static readonly Guid Other = Guid.NewGuid();

    [Fact]
    public void TokenForThisCafe_IsAllowed()
        => Assert.True(TenantTokenPolicy.IsAllowed(Other.ToString(), Other, isSuperAdmin: false));

    [Fact]
    public void TokenForAnotherCafe_IsRejected()
        => Assert.False(TenantTokenPolicy.IsAllowed(Other.ToString(), Tenant.DefaultId, isSuperAdmin: false));

    [Fact]
    public void OldTokenWithoutTenant_BelongsToTheDefaultCafe()
    {
        Assert.True(TenantTokenPolicy.IsAllowed(null, Tenant.DefaultId, isSuperAdmin: false));
        Assert.False(TenantTokenPolicy.IsAllowed(null, Other, isSuperAdmin: false));
    }

    [Fact]
    public void GarbledClaim_IsTreatedLikeNoClaim()
        => Assert.False(TenantTokenPolicy.IsAllowed("not-a-guid", Other, isSuperAdmin: false));

    [Fact]
    public void PlatformOperator_WorksOnEveryCafe()
        => Assert.True(TenantTokenPolicy.IsAllowed(Other.ToString(), Tenant.DefaultId, isSuperAdmin: true));
}
