using Microsoft.AspNetCore.SignalR;

namespace Server.Hubs
{
    public class RemoteControlHub : Hub
    {
        public async Task SendScreenData(string sessionId, byte[] imageData)
        {
            await Clients.Others.SendAsync("ReceiveScreenData", sessionId, imageData);
        }
        public async Task SendInputAction(string sessionId, string action)
        {
            await Clients.Others.SendAsync("ReceiveInputAction", sessionId, action);
        }
    }
}