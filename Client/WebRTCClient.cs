using Microsoft.MixedReality.WebRTC;
using ScreenCaptureI420A; // C++/CLI assembly
using Serilog;
using System;
using System.Runtime.InteropServices;
using System.Windows.Media.Media3D;
public class WebRTCClient : IDisposable
{
    private ExternalVideoTrackSource _trackSource;
    private GCHandle _bufferHandle;
    private byte[] _managedBuffer;
    private DateTime _startTime = DateTime.UtcNow;

    private int _currentWidth;
    private int _currentHeight;
    private int _currentStride;
    public byte[] _yBuffer, _uBuffer, _vBuffer, _aBuffer;
    GCHandle _yHandle, _uHandle, _vHandle, _aHandle;
    public WebRTCClient()
    {
        // Tạo video track source từ callback ARGB32
        _trackSource = ExternalVideoTrackSource.CreateFromI420ACallback(OnFrameRequested);
    }

    /// <summary>
    /// Gán delegate từ C++/CLI sang hàm xử lý bên này
    /// </summary>
    public void AttachToScreenCapture(ScreenCaptureDXGI capture)
    {
        capture.OnFrameCaptured += OnI420AFrame;
    }
     
    /// <summary>
    /// Gọi bởi C++/CLI khi capture được frame
    /// </summary>
    public void OnI420AFrame(IntPtr yPlane,
        int width,
        int height,
        int stride,
        IntPtr uPlane,
        IntPtr vPlane,
        IntPtr aPlane)
    {
        Log.Information("OnI420AFrame: frame received {W}x{H}", width, height);
        int ySize = stride * height;
        int uvWidth = width / 2;
        int uvHeight = height / 2;
        int uvStride = uvWidth;
        int uvSize = uvStride * uvHeight;

        _yBuffer = new byte[ySize];
        _uBuffer = new byte[uvSize];
        _vBuffer = new byte[uvSize];
        _aBuffer = new byte[ySize];

        Marshal.Copy(yPlane, _yBuffer, 0, ySize);
        Marshal.Copy(uPlane, _uBuffer, 0, uvSize);
        Marshal.Copy(vPlane, _vBuffer, 0, uvSize);
        Marshal.Copy(aPlane, _aBuffer, 0, ySize);

        if (_yHandle.IsAllocated) _yHandle.Free();
        if (_uHandle.IsAllocated) _uHandle.Free();
        if (_vHandle.IsAllocated) _vHandle.Free();
        if (_aHandle.IsAllocated) _aHandle.Free();

        _yHandle = GCHandle.Alloc(_yBuffer, GCHandleType.Pinned);
        _uHandle = GCHandle.Alloc(_uBuffer, GCHandleType.Pinned);
        _vHandle = GCHandle.Alloc(_vBuffer, GCHandleType.Pinned);
        _aHandle = GCHandle.Alloc(_aBuffer, GCHandleType.Pinned);

        // Lưu width, height lại như trước
        _currentWidth = width;
        _currentHeight = height;
        _currentStride = stride;
    }

    /// <summary>
    /// Gọi bởi WebRTC khi cần frame I420A để gửi
    /// </summary>
    public unsafe void OnFrameRequested(in FrameRequest request)
    {
        Log.Information("OnFrameRequested: frame requested");
        // Nếu buffer chưa được cấp phát hoặc không hợp lệ thì không làm gì
        if (_yHandle.IsAllocated == false || _uHandle.IsAllocated == false || _vHandle.IsAllocated == false || _aHandle.IsAllocated == false)
            return;
        Log.Information("OnFrameRequested: frame exist");
        // Đảm bảo rằng bạn đang làm việc với dữ liệu I420A đã có từ delegate
        byte* yPtr = (byte*)_yHandle.AddrOfPinnedObject();
        byte* uPtr = (byte*)_uHandle.AddrOfPinnedObject();
        byte* vPtr = (byte*)_vHandle.AddrOfPinnedObject();
        byte* aPtr = (byte*)_aHandle.AddrOfPinnedObject();

        // Đã có dữ liệu I420A từ delegate
        var frame = new I420AVideoFrame
        {
            width = (uint)_currentWidth,
            height = (uint)_currentHeight,
            dataY = (IntPtr)yPtr,
            dataU = (IntPtr)uPtr,
            dataV = (IntPtr)vPtr,
            dataA = (IntPtr)aPtr,
            strideY = _currentStride,
            strideU = (_currentWidth / 2),
            strideV = (_currentWidth / 2),
            strideA = _currentStride
        };

        // Gọi CompleteRequest để trả về frame video
        request.CompleteRequest(in frame);
    }

    /// <summary>
    /// Tạo local video track từ một nguồn nội bộ để sử dụng trong WebRTC 
    /// </summary>
    public LocalVideoTrack CreateLocalVideoTrack()
    {
        // Tạo ExternalVideoTrackSource từ callback
        var trackSource = ExternalVideoTrackSource.CreateFromI420ACallback(OnFrameRequested);
        var config = new LocalVideoTrackInitConfig { trackName = "video_track" };
        var localVideoTrack = LocalVideoTrack.CreateFromSource(trackSource, config);

        return localVideoTrack;
    }

    public void Dispose()
    {
        if (_bufferHandle.IsAllocated)
            _bufferHandle.Free();
        _trackSource?.Dispose();
    }
}
