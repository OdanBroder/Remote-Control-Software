using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using Microsoft.MixedReality.WebRTC;
using ScreenCaptureI420A; // C++/CLI assembly
using Serilog;
using System.Runtime.InteropServices;
using System.Linq.Expressions;
using System.Diagnostics;
namespace Client.Services
{
    public class WebRTCService : IDisposable
    {
        private ExternalVideoTrackSource _trackSource;
        private GCHandle _bufferHandle;
        private DateTime _startTime = DateTime.UtcNow;
        private LocalVideoTrack _localVideoTrack;
        private int _currentWidth;
        private int _currentHeight;
        private int _currentAStride;
        private int _currentYStride;
        private int _currentUStride;
        private int _currentVStride;
        public byte[] _yBuffer, _uBuffer, _vBuffer, _aBuffer;
        GCHandle _yHandle, _uHandle, _vHandle, _aHandle;
        public WebRTCService()
        {
            // Tạo video track source từ callback ARGB32
            Log.Logger = new LoggerConfiguration()
                .WriteTo.Console()
                .CreateLogger();
        }

        public void OnI420AFrame(
        IntPtr yPlane, int yStride,
        IntPtr uPlane, int uStride,
        IntPtr vPlane, int vStride,
        IntPtr aPlane, int aStride,
        int width, int height)
        {
            //Log.Information("OnI420AFrame: frame received {W}x{H}", width, height);
            int ySize = yStride * height;
            int uvSize = uStride * ((height + 1) / 2);
            int aSize = aStride * height;
            if (width <= 0 || height <= 0 || yStride < width || uStride < (width + 1) / 2 || vStride < (width + 1) / 2 || aStride < width)
            {
                Log.Error("Invalid frame parameters: w={0}, h={1}, yStride={2}, uStride={3}, vStride={4}, aStride={5}", width, height, yStride, uStride, vStride, aStride);
                return;
            }
            if (yPlane == IntPtr.Zero || uPlane == IntPtr.Zero || vPlane == IntPtr.Zero || aPlane == IntPtr.Zero)
            {
                Log.Error("Null plane pointer received.");
                return;
            }
            try
            {
                if (_yBuffer == null || _yBuffer.Length != ySize)
                    _yBuffer = new byte[ySize];
                if (_uBuffer == null || _uBuffer.Length != uvSize)
                    _uBuffer = new byte[uvSize];
                if (_vBuffer == null || _vBuffer.Length != uvSize)
                    _vBuffer = new byte[uvSize];
                if (_aBuffer == null || _aBuffer.Length != aSize)
                    _aBuffer = new byte[aSize];

                Marshal.Copy(yPlane, _yBuffer, 0, ySize);
                Marshal.Copy(uPlane, _uBuffer, 0, uvSize);
                Marshal.Copy(vPlane, _vBuffer, 0, uvSize);
                Marshal.Copy(aPlane, _aBuffer, 0, aSize);

                if (_yHandle.IsAllocated) _yHandle.Free();
                if (_uHandle.IsAllocated) _uHandle.Free();
                if (_vHandle.IsAllocated) _vHandle.Free();
                if (_aHandle.IsAllocated) _aHandle.Free();

                _yHandle = GCHandle.Alloc(_yBuffer, GCHandleType.Pinned);
                _uHandle = GCHandle.Alloc(_uBuffer, GCHandleType.Pinned);
                _vHandle = GCHandle.Alloc(_vBuffer, GCHandleType.Pinned);
                _aHandle = GCHandle.Alloc(_aBuffer, GCHandleType.Pinned);

            }
            catch (Exception ex)
            {
                Log.Error("Error while initializing skipping frame...{ex} ",ex);
            }
            // Lưu width, height lại như trước
            _currentWidth = width;
            _currentHeight = height;
            _currentAStride = aStride;
            _currentYStride = yStride;
            _currentUStride = uStride;
            _currentVStride = vStride;

        }

        /// <summary>
        /// Gọi bởi WebRTC khi cần frame I420A để gửi
        /// </summary>
        public unsafe void OnFrameRequested(in FrameRequest request)
        {
            //Log.Information("OnFrameRequested: frame requested");
            // Nếu buffer chưa được cấp phát hoặc không hợp lệ thì không làm gì
            if (_yHandle.IsAllocated == false || _uHandle.IsAllocated == false || _vHandle.IsAllocated == false || _aHandle.IsAllocated == false)
                return;
            //Log.Information("OnFrameRequested: frame exist");
            // Đảm bảo rằng bạn đang làm việc với dữ liệu I420A đã có từ delegate
            try
            {
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
                    strideY = _currentYStride,
                    strideU = _currentUStride,
                    strideV = _currentVStride,
                    strideA = _currentAStride
                };
                try
                {
                    //Console.WriteLine("OnFrameRequested: Complete Request!");
                    request.CompleteRequest(in frame);
                }
                catch (Exception innerEx)
                {
                    Log.Error("Failed to complete request: {msg}", innerEx.Message);
                }
            }
            catch (Exception ex)
            {
                Log.Error("Error with requested frame: {err}", ex);
            }
        }

        private ScreenCaptureDXGI? _currentCapture;

        public void Attach(ScreenCaptureDXGI capture)
        {
            if (_currentCapture != null)
                _currentCapture.OnFrameCaptured -= OnI420AFrame;

            _currentCapture = capture;
            _currentCapture.OnFrameCaptured += OnI420AFrame;
        }

        public void Detach(ScreenCaptureDXGI capture)
        {
            capture.OnFrameCaptured -= OnI420AFrame;
            if (_currentCapture == capture)
                _currentCapture = null;
        }

        /// <summary>
        /// Tạo local video track từ một nguồn nội bộ để sử dụng trong WebRTC 
        /// </summary>
        public LocalVideoTrack CreateLocalVideoTrack()
        {
            // Tạo ExternalVideoTrackSource từ callback
            _trackSource = ExternalVideoTrackSource.CreateFromI420ACallback(OnFrameRequested);
            var config = new LocalVideoTrackInitConfig { trackName = "video_track" };
            _localVideoTrack = LocalVideoTrack.CreateFromSource(_trackSource, config);
            return _localVideoTrack;
        }

        public void Dispose()
        {
            if (_bufferHandle.IsAllocated)
                _bufferHandle.Free();
            _trackSource?.Dispose();
            _localVideoTrack?.Dispose();
        }
    }
}