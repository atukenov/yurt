using Microsoft.AspNetCore.SignalR;
using NSubstitute;
using Yurt.Application.Common.Interfaces;
using Yurt.Domain.Entities;
using Yurt.Infrastructure.Hubs;

namespace Yurt.UnitTests;

/// <summary>Live order updates go to the order's own cafe's staff only, never to every cafe's staff.</summary>
public class OrdersHubTenantTests
{
    private static (OrdersHubService Svc, IHubClients Clients) Build(Guid currentCafe)
    {
        var hub = Substitute.For<IHubContext<OrdersHub>>();
        var clients = Substitute.For<IHubClients>();
        clients.Group(Arg.Any<string>()).Returns(Substitute.For<IClientProxy>());
        hub.Clients.Returns(clients);
        var tenant = Substitute.For<ITenantContext>();
        tenant.TenantId.Returns(currentCafe);
        return (new OrdersHubService(hub, null!, tenant), clients);
    }

    [Fact]
    public async Task NewOrder_GoesToItsOwnCafesStaffGroup()
    {
        var cafe = Guid.NewGuid();
        var (svc, clients) = Build(Tenant.DefaultId);

        await svc.NotifyOrderCreatedAsync(new Order { TenantId = cafe, CustomerUserId = Guid.NewGuid(), Location = new Location() });

        clients.Received(1).Group($"admins:{cafe}");
        clients.DidNotReceive().Group("admins");
        clients.DidNotReceive().Group($"admins:{Tenant.DefaultId}");
    }

    [Fact]
    public async Task PaymentEvents_AndGroupCarts_CarryTheCafe()
    {
        var cafe = Guid.NewGuid();
        var (svc, clients) = Build(cafe);
        var cart = Guid.NewGuid();

        await svc.NotifyPaymentSucceededAsync(new Order { TenantId = cafe, Location = new Location() });
        await svc.NotifyGroupCartUpdatedAsync(cart, null!);

        clients.Received(1).Group($"admins:{cafe}");
        clients.Received(1).Group($"group:{cafe}:{cart}");
    }

    [Fact]
    public void GroupNames_DifferPerCafe()
    {
        Assert.NotEqual(OrdersHub.AdminsGroup(Guid.NewGuid()), OrdersHub.AdminsGroup(Guid.NewGuid()));
        Assert.NotEqual(OrdersHub.LocationGroup(Guid.NewGuid(), "x"), OrdersHub.LocationGroup(Guid.NewGuid(), "x"));
    }
}
