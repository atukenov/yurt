using System.Security.Claims;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.SignalR;

namespace Yurt.Infrastructure.Hubs;

[Authorize]
public class OrdersHub : Hub
{
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
            // Admins join the global admin group to receive all order events
            await Groups.AddToGroupAsync(Context.ConnectionId, "admins");
        }

        await base.OnConnectedAsync();
    }

    // Admins call this to subscribe to a location's order stream
    public async Task SubscribeToLocation(string locationId)
    {
        var userType = Context.User?.FindFirstValue("user_type");
        if (userType != "admin") return;

        await Groups.AddToGroupAsync(Context.ConnectionId, $"location:{locationId}");
    }

    public async Task UnsubscribeFromLocation(string locationId)
    {
        await Groups.RemoveFromGroupAsync(Context.ConnectionId, $"location:{locationId}");
    }

    public async Task JoinGroupCart(string groupCartId)
    {
        var userType = Context.User?.FindFirstValue("user_type");
        if (userType != "customer") return;
        await Groups.AddToGroupAsync(Context.ConnectionId, $"group-{groupCartId}");
    }

    public async Task LeaveGroupCart(string groupCartId)
        => await Groups.RemoveFromGroupAsync(Context.ConnectionId, $"group-{groupCartId}");
}
