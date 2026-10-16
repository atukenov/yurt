using Yurt.Application.Features.Tenants;
using Yurt.Domain.Entities;

namespace Yurt.UnitTests;

public class TenantResolverTests
{
    private static readonly Tenant Default = new() { Id = Tenant.DefaultId, Slug = "default", Name = "Yurt" };
    private static readonly Tenant Blue = new() { Slug = "blue", Name = "Blue Cafe", Domain = "order.bluecafe.kz" };

    [Fact]
    public void KnownHost_ResolvesThatTenant()
    {
        var (id, slug) = TenantResolver.Resolve([Default, Blue], "order.bluecafe.kz");
        Assert.Equal(Blue.Id, id);
        Assert.Equal("blue", slug);
    }

    [Fact]
    public void HostMatch_IgnoresCase()
    {
        var (id, _) = TenantResolver.Resolve([Default, Blue], "Order.BlueCafe.KZ");
        Assert.Equal(Blue.Id, id);
    }

    [Theory]
    [InlineData("unknown.example.com")]
    [InlineData("localhost")]
    [InlineData("")]
    [InlineData(null)]
    public void UnknownOrMissingHost_FallsBackToDefault(string? host)
    {
        var (id, slug) = TenantResolver.Resolve([Default, Blue], host);
        Assert.Equal(Tenant.DefaultId, id);
        Assert.Equal("default", slug);
    }

    [Fact]
    public void EmptyTable_StillResolvesToDefault()
    {
        var (id, slug) = TenantResolver.Resolve([], "order.bluecafe.kz");
        Assert.Equal(Tenant.DefaultId, id);
        Assert.Equal("default", slug);
    }

    [Fact]
    public void InactiveTenantsAreNotPassedIn_SoItsHostFallsBack()
    {
        // The directory only returns active tenants; a switched-off cafe's host therefore gets the default.
        var (id, _) = TenantResolver.Resolve([Default], "order.bluecafe.kz");
        Assert.Equal(Tenant.DefaultId, id);
    }
}
