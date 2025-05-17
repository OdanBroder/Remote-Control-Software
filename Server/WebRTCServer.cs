using Microsoft.AspNetCore.SignalR;
using Microsoft.MixedReality.WebRTC;
using Serilog;
using System.Drawing.Imaging;
using System.Drawing;
using System;
using System.Runtime.InteropServices;
using Video;

public class WebRTCServer
{
    private readonly IHubContext<RemoteControlHub> _hub;
    private readonly Serilog.ILogger _logger;
    // Quản lý nhiều PeerConnection nếu nhiều client
    private readonly Dictionary<string, PeerConnection> _peers = new();

    public WebRTCServer(IHubContext<RemoteControlHub> hub, Serilog.ILogger logger)
    {
        _hub = hub;
        _logger = logger;
    }

    public async Task HandleSdpAsync(string connectionId, string sdp, string type)
    {
        // Lấy hoặc khởi tạo PeerConnection cho connectionId
        var pc = GetOrCreatePeer(connectionId);

        _logger.Information($"[{connectionId}] Received SDP {type}");
        if (type == "offer")
        {
            await pc.SetRemoteDescriptionAsync(new SdpMessage
            {
                Type = SdpMessageType.Offer,
                Content = sdp
            });
            pc.CreateAnswer();
            _logger.Information($"Sdp offer: {sdp}");
            _logger.Information($"[{connectionId}] Created answer");
        }
        else // "answer"
        {
            await pc.SetRemoteDescriptionAsync(new SdpMessage
            {
                Type = SdpMessageType.Answer,
                Content = sdp
            });
        }
    }

    public Task HandleIceCandidateAsync(string connectionId, string candidate, string sdpMid, int sdpMlineIndex)
    {
        var pc = GetOrCreatePeer(connectionId);
        _logger.Information($"[{connectionId}] ICE candidate");
        pc.AddIceCandidate(new IceCandidate
        {
            Content = candidate,
            SdpMid = sdpMid,
            SdpMlineIndex = sdpMlineIndex
        });
        return Task.CompletedTask;
    }

    private PeerConnection GetOrCreatePeer(string connectionId)
    {
        if (!_peers.TryGetValue(connectionId, out var pc))
        {
            pc = new PeerConnection();
            var config = new PeerConnectionConfiguration
            {
                IceServers = new List<IceServer>
                {
                    new IceServer { Urls = { "stun:stun.l.google.com:19302" } }
                }
            };
            pc.InitializeAsync(config).Wait();

            // Khi local sdp ready:
            pc.LocalSdpReadytoSend += async msg =>
            {
                await _hub.Clients.Client(connectionId)
                    .SendAsync("ReceiveSdp", msg.Content, msg.Type.ToString().ToLower());
            };
            // Khi ICE ready:
            pc.IceCandidateReadytoSend += async cand =>
            {
                await _hub.Clients.Client(connectionId)
                    .SendAsync("ReceiveIceCandidate", cand.Content, cand.SdpMid, cand.SdpMlineIndex);
            };
            //if(!pc.IsConnected)
            //{
            //    _logger.Warning("Connection is not established");
            //    return null;
            //}    
            _logger.Information($"Video track is adding...");
            // Khi video track thêm vào:
            pc.VideoTrackAdded += track =>
            {
                Log.Information("Video track added: {Name}", track.Name);

                track.I420AVideoFrameReady += frame =>
                {
                    _logger.Information("Frame received: Width = {Width}, Height = {Height}", frame.width, frame.height);

                    // Kiểm tra thông tin chi tiết của frame (ví dụ: kiểm tra kích thước dữ liệu Y, U, V, A)
                    _logger.Information("Y data size: {YSize}, U data size: {USize}, V data size: {VSize}, A data size: {ASize}",
                        frame.dataY, frame.dataU, frame.dataV, frame.dataA);

                    HandleVideoTrackAdded(track); // Gọi hàm xử lý frame
                };
            };
            _peers[connectionId] = pc;
        }
        else
        {
            _logger.Warning($"Unable to find peer");
        }
        return pc;
    }

    // Xử lý các video track nhận được từ client
    private void HandleVideoTrackAdded(RemoteVideoTrack track)
    {
        _logger.Information("Video track added.");
        // Bạn có thể hiển thị video hoặc xử lý theo nhu cầu
        track.I420AVideoFrameReady += OnVideoFrameReady;
    }

    // Xử lý khi một video frame được gửi tới
    private void OnVideoFrameReady(I420AVideoFrame frame)
    {
        int ySize = (int)(frame.strideY * frame.height);
        int uSize = (int)(frame.strideU * (frame.height / 2));
        int vSize = (int)(frame.strideV * (frame.height / 2));
        int aSize = (int)(frame.strideA * frame.height);

        _logger.Information($"Frame received: {frame.width}x{frame.height}, Y size: {ySize}, U size: {uSize}, V size: {vSize}, A size: {aSize}");
        Display(frame);
    }
    private void Display(I420AVideoFrame frame)
    {
        if (frame.dataY == IntPtr.Zero || frame.dataU == IntPtr.Zero || frame.dataV == IntPtr.Zero)
        {
            _logger.Warning("No frame so skipped.");
            return;
        }

        _logger.Information("Starting to display...");

        // Chuyển đổi I420 sang RGB
        byte[] rgbData = null;
        bool success = VideoProcessor.ConvertI420AToRGB(
            frame.dataY, frame.strideY,
            frame.dataU, frame.strideU,
            frame.dataV, frame.strideV,
            frame.dataA, frame.strideA,
            (int)frame.width, (int)frame.height,
            ref rgbData);

        if (!success || rgbData == null)
        {
            _logger.Error("Failed to convert I420 to RGB.");
            return;
        }

        _logger.Information("Saving frame...");

        // Tạo thư mục lưu frame
        string folder = "Frames";
        Directory.CreateDirectory(folder);

        // Tạo tên file với timestamp
        string timestamp = DateTime.Now.ToString("yyyyMMdd_HHmmss_fff");
        string path = Path.Combine(folder, $"frame_{timestamp}.png");

        try
        {
            // Tạo Bitmap từ dữ liệu RGB
            using (Bitmap bitmap = new Bitmap((int)frame.width, (int)frame.height, PixelFormat.Format24bppRgb))
            {
                BitmapData bmpData = bitmap.LockBits(
                    new Rectangle(0, 0, bitmap.Width, bitmap.Height),
                    ImageLockMode.WriteOnly,
                    PixelFormat.Format24bppRgb);

                // Copy dữ liệu RGB vào Bitmap
                System.Runtime.InteropServices.Marshal.Copy(rgbData, 0, bmpData.Scan0, rgbData.Length);

                bitmap.UnlockBits(bmpData);

                // Lưu thành file PNG
                bitmap.Save(path, ImageFormat.Png);
                _logger.Information($"Frame saved to {path}");
            }
        }
        catch (Exception ex)
        {
            _logger.Error($"Error saving frame: {ex.Message}");
        }
    }


    // Hàm phụ để sao chép từ unmanaged memory sang byte[]
    private byte[] CopyFramePlane(IntPtr source, int stride, uint width, uint height)
    {
        int size = checked((int)(stride * height));
        byte[] buffer = new byte[size];
        Marshal.Copy(source, buffer, 0, size);
        return buffer;
    }
}
