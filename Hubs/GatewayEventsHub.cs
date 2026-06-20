using Microsoft.AspNetCore.SignalR;

namespace Shluz.Hubs;

public sealed class GatewayEventsHub : Hub
{
    public Task SubscribeToGateway() => Groups.AddToGroupAsync(Context.ConnectionId, "gateway-observers");
}
