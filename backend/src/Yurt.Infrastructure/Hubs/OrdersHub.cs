using System.Security.Claims;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.SignalR;
using Yurt.Domain.Entities;
using Yurt.Infrastructure.Services;

namespace Yurt.Infrastructure.Hubs;

[Authorize]
public class OrdersHub : Hub
{
    /// <summary>Set by the tenant middleware on the request that opened the connection.</summary>
    public const string TenantItem = "TenantId";

    // Every group name carries the cafe, so one cafe's staff never hear another cafe's orders.
    public static string AdminsGroup(Guid tenantId) => $"admins:{tenantId}";
    public static string LocationGroup(Guid tenantId, string locationId) => $"location:{tenantId}:{locationId}";
    public static string GroupCartGroup(Guid tenantId, Guid groupCartId) => $"group:{tenantId}:{groupCartId}";

    /// <summary>The cafe this connection was opened for (already checked against the token by the tenant middleware).</summary>
    private Guid Cafe()
    {
        if (Context.GetHttpContext()?.Items[TenantItem] is Guid fromRequest) return fromRequest;
        return Guid.TryParse(Context.User?.FindFirstValue(JwtTokenService.TenantClaim), out var fromToken) ? fromToken : Tenant.DefaultId;
    }

    public override async Task OnConnectedAsync()
    {
        var userId = Context.User?.FindFirstValue(ClaimTypes.NameIdentifier);
        var userType = Context.User?.FindFirstValue("user_type");

        if (userId == null) return;

        if (userType == "customer")
        {
            // Customers join their personal group
            await Groups.AddToGroupAsync(Context.ConnectionId, $"customer:{userId}");
        }
        else if (userType == "admin")
        {
            // Staff join their own cafe's group to receive its order events
            await Groups.AddToGroupAsync(Context.ConnectionId, AdminsGroup(Cafe()));
        }

        await base.OnConnectedAsync();
    }

    // Admins call this to subscribe to a location's order stream
    public async Task SubscribeToLocation(string locationId)
    {
        var userType = Context.User?.FindFirstValue("user_type");
        if (userType != "admin") return;

        await Groups.AddToGroupAsync(Context.ConnectionId, LocationGroup(Cafe(), locationId));
    }

    public async Task UnsubscribeFromLocation(string locationId)
    {
        await Groups.RemoveFromGroupAsync(Context.ConnectionId, LocationGroup(Cafe(), locationId));
    }

    public async Task JoinGroupCart(string groupCartId)
    {
        var userType = Context.User?.FindFirstValue("user_type");
        if (userType != "customer") return;
        if (Guid.TryParse(groupCartId, out var id))
            await Groups.AddToGroupAsync(Context.ConnectionId, GroupCartGroup(Cafe(), id));
    }

    public async Task LeaveGroupCart(string groupCartId)
    {
        if (Guid.TryParse(groupCartId, out var id))
            await Groups.RemoveFromGroupAsync(Context.ConnectionId, GroupCartGroup(Cafe(), id));
    }
}
