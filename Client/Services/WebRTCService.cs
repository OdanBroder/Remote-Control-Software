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
namespace Client.Services
{
    public class WebRTCService : IDisposable
    {
        private ExternalVideoTrackSource _trackSource;
        private GCHandle _bufferHandle;
        private DateTime _startTime = DateTime.UtcNow;

        private byte[] _argbBuffer;
        private GCHandle _argbHandle;

        private int _currentWidth;
        private int _currentHeight;
        private int _currentStride;

        public WebRTCService()
        {
            // Tạo video track source từ callback ARGB32
            Log.Logger = new LoggerConfiguration()
                .WriteTo.Console()
                .CreateLogger();
            _trackSource = ExternalVideoTrackSource.CreateFromI420ACallback(OnFrameRequested);
        }

        /// <summary>
        /// Gán delegate từ C++/CLI sang hàm xử lý bên này
        /// </summary>
        public void AttachToScreenCapture(ScreenCaptureDXGI capture)
        {
            capture.OnFrameCaptured += OnArgbFrame;
        }

        /// <summary>
        /// Gọi bởi C++/CLI khi capture được frame
        /// </summary>
        public void OnArgbFrame(IntPtr argbBuffer, int width, int height, int stride)
        {
            int bufferSize = stride * height;
            _argbBuffer = new byte[bufferSize];

            // Copy từ unmanaged sang managed
            Marshal.Copy(argbBuffer, _argbBuffer, 0, bufferSize);

            // Giải phóng handle cũ nếu cần
            if (_argbHandle.IsAllocated) _argbHandle.Free();

            // Pin lại buffer mới
            _argbHandle = GCHandle.Alloc(_argbBuffer, GCHandleType.Pinned);

            _currentWidth = width;
            _currentHeight = height;
            _currentStride = stride;
        }


        /// <summary>
        /// Gọi bởi WebRTC khi cần frame I420A để gửi
        /// </summary>
        public unsafe void OnFrameRequested(in FrameRequest request)
        {
            try
            {
                bool allocated = _argbHandle.IsAllocated;

                if (!allocated)
                {
                    // Nếu chưa có frame nào được capture
                    return;
                }

                byte* argbPtr = (byte*)_argbHandle.AddrOfPinnedObject();

                var frame = new Argb32VideoFrame
                {
                    width = (uint)_currentWidth,
                    height = (uint)_currentHeight,
                    data = (IntPtr)argbPtr,
                    stride = _currentStride
                };

                try
                {
                    request.CompleteRequest(in frame);
                }
                catch (Exception innerEx)
                {
                    Log.Error("Failed to complete ARGB request: {msg}", innerEx.Message);
                }
            }
            catch (Exception ex)
            {
                Log.Error("Error with ARGB requested frame: {err}", ex);
                throw new InvalidOperationException(ex.Message);
            }
        }



        /// <summary>
        /// Tạo local video track từ một nguồn nội bộ để sử dụng trong WebRTC 
        /// </summary>
        public LocalVideoTrack CreateLocalVideoTrack()
        {
            // Tạo ExternalVideoTrackSource từ callback
            var trackSource = ExternalVideoTrackSource.CreateFromArgb32Callback(OnFrameRequested);
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
}
