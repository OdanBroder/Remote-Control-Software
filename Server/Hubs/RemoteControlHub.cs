// Server/Hubs/RemoteControlHub.cs
using Microsoft.AspNetCore.SignalR;
using System.Threading.Tasks;

public class RemoteControlHub : Hub
{
    private readonly WebRTCServer _webrtc;

    public RemoteControlHub(WebRTCServer webrtc)
    {
        _webrtc = webrtc;
    }

    // Client gọi SendSdp(offer, "offer") hoặc ("answer")
    public async Task SendSdp(string sdp, string type)
    {
        await _webrtc.HandleSdpAsync(Context.ConnectionId, sdp, type);
    }

    // Client gọi SendIceCandidate(...)
    public async Task SendIceCandidate(string candidate, string sdpMid, int sdpMlineIndex)
    {
        await _webrtc.HandleIceCandidateAsync(Context.ConnectionId, candidate, sdpMid, sdpMlineIndex);
    }
}
