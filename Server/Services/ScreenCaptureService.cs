using System.Drawing;
using System.Drawing.Imaging;
using System.Runtime.InteropServices;
using Microsoft.Extensions.Logging;
using SIPSorcery.Net;
using SIPSorcery.Media;
using System.IO;
using System.Threading.Tasks;
using System.Collections.Concurrent;
using System.Diagnostics;
using System.Threading;
using SIPSorceryMedia.Abstractions;
using Server.Models;

namespace Server.Services
{
    public class ScreenCaptureService : IDisposable
    {
        private readonly ILogger<ScreenCaptureService> _logger;
        private readonly ConcurrentDictionary<string, (RTCPeerConnection, MediaStreamTrack)> _peerConnections;
        private readonly string _rtmpUrl;
        private readonly string _hlsOutputPath;
        private readonly int _frameRate;
        private readonly Size _captureSize;
        private bool _isStreaming;
        private CancellationTokenSource _streamingCts;

        // Windows API imports for screen capture
        [DllImport("user32.dll")]
        private static extern IntPtr GetDesktopWindow();

        [DllImport("user32.dll")]
        private static extern IntPtr GetWindowDC(IntPtr hWnd);

        [DllImport("user32.dll")]
        private static extern int ReleaseDC(IntPtr hWnd, IntPtr hDC);

        [DllImport("gdi32.dll")]
        private static extern IntPtr CreateCompatibleDC(IntPtr hdc);

        [DllImport("gdi32.dll")]
        private static extern IntPtr CreateCompatibleBitmap(IntPtr hdc, int nWidth, int nHeight);

        [DllImport("gdi32.dll")]
        private static extern IntPtr SelectObject(IntPtr hdc, IntPtr hObject);

        [DllImport("gdi32.dll")]
        private static extern bool BitBlt(IntPtr hdcDest, int nXDest, int nYDest, int nWidth, int nHeight, IntPtr hdcSrc, int nXSrc, int nYSrc, uint dwRop);

        [DllImport("gdi32.dll")]
        private static extern bool DeleteObject(IntPtr hObject);

        [DllImport("gdi32.dll")]
        private static extern bool DeleteDC(IntPtr hdc);

        public ScreenCaptureService(
            ILogger<ScreenCaptureService> logger,
            string rtmpUrl = "rtmp://localhost/live",
            string hlsOutputPath = "wwwroot/hls",
            int frameRate = 30,
            Size? captureSize = null)
        {
            _logger = logger;
            _peerConnections = new ConcurrentDictionary<string, (RTCPeerConnection, MediaStreamTrack)>();
            _rtmpUrl = rtmpUrl;
            _hlsOutputPath = hlsOutputPath;
            _frameRate = frameRate;
            _captureSize = captureSize ?? new Size(1920, 1080);
            _isStreaming = false;
            _streamingCts = new CancellationTokenSource();

            // Ensure HLS output directory exists
            Directory.CreateDirectory(_hlsOutputPath);
        }

        public async Task StartStreaming(string sessionId)
        {
            if (_isStreaming)
            {
                _logger.LogWarning("Streaming is already in progress");
                return;
            }

            _isStreaming = true;
            _streamingCts = new CancellationTokenSource();

            try
            {
                // Start FFmpeg process for RTMP streaming
                var ffmpegProcess = StartFFmpegProcess();

                // Start screen capture and streaming loop
                await Task.Run(async () =>
                {
                    while (!_streamingCts.Token.IsCancellationRequested)
                    {
                        try
                        {
                            var frame = CaptureScreen();
                            await ProcessFrame(frame, ffmpegProcess);
                            await Task.Delay(1000 / _frameRate, _streamingCts.Token);
                        }
                        catch (OperationCanceledException)
                        {
                            break;
                        }
                        catch (Exception ex)
                        {
                            _logger.LogError(ex, "Error in streaming loop");
                        }
                    }
                }, _streamingCts.Token);

                _logger.LogInformation($"Started streaming for session {sessionId}");
            }
            catch (Exception ex)
            {
                _isStreaming = false;
                _logger.LogError(ex, "Failed to start streaming");
                throw;
            }
        }

        public void StopStreaming()
        {
            if (!_isStreaming)
            {
                return;
            }

            _streamingCts.Cancel();
            _isStreaming = false;
            _logger.LogInformation("Stopped streaming");
        }

        public async Task<RTCPeerConnection> CreateWebRTCConnection(string sessionId)
        {
            var pc = new RTCPeerConnection(new RTCConfiguration
            {
                iceServers = new List<RTCIceServer>
                {
                    new RTCIceServer { urls = "stun:stun.l.google.com:19302" }
                }
            });

            // Add video track
            var videoTrack = new MediaStreamTrack(SDPMediaTypesEnum.video, false, new List<SDPAudioVideoMediaFormat> 
            { 
                new SDPAudioVideoMediaFormat(SDPMediaTypesEnum.video, 96, "VP8", 90000)
            });
            pc.addTrack(videoTrack);
            _peerConnections.TryAdd(sessionId, (pc, videoTrack));
            
            // Set up event handlers
            pc.onconnectionstatechange += (state) =>
            {
                _logger.LogInformation($"WebRTC connection state changed to {state}");
                if (state == RTCPeerConnectionState.closed)
                {
                    _peerConnections.TryRemove(sessionId, out _);
                }
            };

            // Return the task (even if it's done synchronously)
            await Task.CompletedTask;
            return pc;
        }


        private byte[] CaptureScreen()
        {
            IntPtr hdcSrc = GetWindowDC(GetDesktopWindow());
            IntPtr hdcDest = CreateCompatibleDC(hdcSrc);
            IntPtr hBitmap = CreateCompatibleBitmap(hdcSrc, _captureSize.Width, _captureSize.Height);
            IntPtr hOld = SelectObject(hdcDest, hBitmap);

            BitBlt(hdcDest, 0, 0, _captureSize.Width, _captureSize.Height, hdcSrc, 0, 0, 0x00CC0020);

            SelectObject(hdcDest, hOld);
            DeleteDC(hdcDest);
            ReleaseDC(GetDesktopWindow(), hdcSrc);

            using (var bitmap = Image.FromHbitmap(hBitmap))
            using (var ms = new MemoryStream())
            {
                bitmap.Save(ms, ImageFormat.Jpeg);
                DeleteObject(hBitmap);
                return ms.ToArray();
            }
        }

        private Process StartFFmpegProcess()
        {
            var ffmpegArgs = $"-f rawvideo -pix_fmt bgr24 -s {_captureSize.Width}x{_captureSize.Height} -r {_frameRate} -i - " +
                            $"-c:v libx264 -preset ultrafast -tune zerolatency -f flv {_rtmpUrl} " +
                            $"-c:v libx264 -preset ultrafast -tune zerolatency -f hls -hls_time 2 -hls_list_size 3 -hls_flags delete_segments {Path.Combine(_hlsOutputPath, "stream.m3u8")}";

            var process = new Process
            {
                StartInfo = new ProcessStartInfo
                {
                    FileName = "ffmpeg",
                    Arguments = ffmpegArgs,
                    UseShellExecute = false,
                    RedirectStandardInput = true,
                    RedirectStandardError = true,
                    CreateNoWindow = true
                }
            };

            process.ErrorDataReceived += (sender, e) =>
            {
                if (!string.IsNullOrEmpty(e.Data))
                {
                    _logger.LogDebug($"FFmpeg: {e.Data}");
                }
            };

            process.Start();
            process.BeginErrorReadLine();
            return process;
        }

        private async Task ProcessFrame(byte[] frame, Process ffmpegProcess)
        {
            // Send frame to FFmpeg for RTMP/HLS streaming
            await ffmpegProcess.StandardInput.BaseStream.WriteAsync(frame, 0, frame.Length);

            // Send frame to WebRTC connections
            foreach (var (pc, track) in _peerConnections.Values)
            {
                try
                {
                    // track.SendVideo(_captureSize.Width, _captureSize.Height, frame);
                }
                catch (Exception ex)
                {
                    _logger.LogError(ex, "Error sending frame to WebRTC connection");
                }
            }
        }

        public void Dispose()
        {
            StopStreaming();
            foreach (var (pc, track) in _peerConnections.Values)
            {
                pc.Close("normal");
            }
            _peerConnections.Clear();
        }
    }
}