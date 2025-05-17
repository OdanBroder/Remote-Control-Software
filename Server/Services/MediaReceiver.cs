using Microsoft.MixedReality.WebRTC;

public class WebRTCManager
{
    private PeerConnection _peerConnection;

    public WebRTCManager()
    {
        _peerConnection = new PeerConnection();
    }
    public async Task InitAsync()
    {
        _peerConnection.VideoTrackAdded += OnVideoTrackAdded;
        await _peerConnection.InitializeAsync();
    }

    private void OnVideoTrackAdded(RemoteVideoTrack track)
    {
        Console.WriteLine("📹 Remote video track added!");
        track.I420AVideoFrameReady += OnFrameReceived;
    }

    private void OnFrameReceived(I420AVideoFrame frame)
    {
        Console.WriteLine($"🖼️ Received frame {frame.width}x{frame.height}");

        // Ví dụ: xử lý hiển thị/lưu lại
        // Bạn có thể gọi: FrameProcessor.Process(frame);
    }

    public async Task SetRemoteSdpAsync(SdpMessage message)
    {
        await _peerConnection.SetRemoteDescriptionAsync(message);
        _peerConnection.CreateAnswer(); // Sẽ gọi lại LocalSdpReadytoSend
    }

    public void AddIceCandidate(IceCandidate candidate)
    {
        _peerConnection.AddIceCandidate(candidate);
    }

    public event PeerConnection.IceCandidateReadytoSendDelegate IceCandidateReadyToSend
    {
        add { _peerConnection.IceCandidateReadytoSend += value; }
        remove { _peerConnection.IceCandidateReadytoSend -= value; }
    }

    public event PeerConnection.LocalSdpReadyToSendDelegate LocalSdpReadyToSend
    {
        add { _peerConnection.LocalSdpReadytoSend += value; }
        remove { _peerConnection.LocalSdpReadytoSend -= value; }
    }
}
