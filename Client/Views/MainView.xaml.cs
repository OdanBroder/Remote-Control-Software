using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Data;
using System.Windows.Documents;
using System.Windows.Input;
using System.Windows.Media;
using System.Windows.Media.Imaging;
using System.Windows.Shapes;
using Client.Src.Utils;
using Client.Src.Services;
using System.Text.RegularExpressions;
using ScreenCaptureI420A;
using System.Windows.Forms;
using Microsoft.MixedReality.WebRTC;
using Microsoft.AspNetCore.SignalR.Client;
using Serilog;



namespace Client.Views
{
    /// <summary>
    /// Interaction logic for MainView.xaml
    /// </summary>
    public partial class MainView : Window
    {
        private PeerConnection _peerConnection;
        private WebRTCClient _webrtcClient;
        private ScreenCaptureDXGI _capture;
        private HubConnection _signalR;
        private WriteableBitmap _bitmap;
        private object _bitmapLock = new object();
        private string _pendingSdp = null;
        private string _pendingSdpType = null;
        public MainView()
        {
            InitializeComponent();

            // Khởi tạo WebRTCClient
            _webrtcClient = new WebRTCClient();
            Loaded += MainView_Loaded;
            Log.Information("MainView is created");
        }

        private void StartButton_Click(object sender, RoutedEventArgs e)
        {
            Log.Debug("Start button clicked.");
        }

        private async void StopButton_Click(object sender, RoutedEventArgs e)
        {
            Log.Warning("Stop button clicked.");

            try
            {
                // 1. Dừng và hủy ScreenCaptureDXGI
                if (_capture != null)
                {
                    _capture.OnFrameCaptured -= _webrtcClient.OnI420AFrame; // Gỡ sự kiện
                    _capture.Stop();
                    _capture.Dispose();
                    _capture = null;
                    Log.Information("Screen capture stopped and disposed.");
                }

                // 2. Đóng và hủy WebRTCClient
                if (_webrtcClient != null)
                {
                    _webrtcClient.Dispose();
                    _webrtcClient = null;
                    Log.Information("WebRTC client disposed.");
                }

                // 3. Đóng và hủy PeerConnection
                if (_peerConnection != null)
                {
                    _peerConnection.Close();
                    _peerConnection.Dispose();
                    _peerConnection = null;
                    Log.Information("Peer connection closed and disposed.");
                }

                // 4. Dừng và hủy SignalR
                if (_signalR != null)
                {
                    await _signalR.StopAsync();
                    await _signalR.DisposeAsync();
                    _signalR = null;
                    Log.Information("SignalR connection stopped and disposed.");
                }

                System.Windows.MessageBox.Show("Đã dừng truyền màn hình.", "Thông báo", MessageBoxButton.OK, MessageBoxImage.Information);
            }
            catch (Exception ex)
            {
                Log.Error(ex, "Lỗi khi dừng client");
                System.Windows.MessageBox.Show($"Lỗi khi dừng client: {ex.Message}", "Lỗi", MessageBoxButton.OK, MessageBoxImage.Error);
            }
        }

        private void MainForm_FormClosing(object sender, FormClosingEventArgs e)
        {
            // Dọn dẹp khi form đóng
            _webrtcClient.Dispose();
        }
        private async void MainView_Loaded(object sender, RoutedEventArgs e)
        {
            // khởi tạo peer connection
            _peerConnection = new PeerConnection();
            var config = new PeerConnectionConfiguration
            {
                IceServers = new List<IceServer>
                {
                    new IceServer { Urls = { "stun:stun.l.google.com:19302" } }
                }
            };
            await _peerConnection.InitializeAsync(config); // <- thêm IceServe

            // Gửi SDP offer
            _peerConnection.LocalSdpReadytoSend += async (sdp) =>
            {
                Log.Warning($"SDP: {sdp.Type} =\n{sdp.Content}");
                await _signalR.InvokeAsync("SendSdp", sdp.Content, sdp.Type.ToString().ToLower());
            };

            // Gửi ICE khi có
            _peerConnection.IceCandidateReadytoSend += async (candidate) =>
            {
                await _signalR.InvokeAsync("SendIceCandidate", candidate.Content, candidate.SdpMid, candidate.SdpMlineIndex);
            };

            // Bắt đầu kết nối SignalR
            _signalR = new HubConnectionBuilder()
                .WithUrl("http://localhost:5000/signal")
                .WithAutomaticReconnect()
                .Build();
       
            // Lắng nghe SDP từ server
            _signalR.On<string, string>("ReceiveSdp", async (sdp, type) =>
            {
                if(_peerConnection==null||!_peerConnection.Initialized)
                {
                    Log.Warning("peer connection is not Initialized");
                    return;
                }    
                if (type == "offer")
                {
                    await _peerConnection.SetRemoteDescriptionAsync(new SdpMessage
                    {
                        Type = SdpMessageType.Offer,
                        Content = sdp
                    });

                    _peerConnection.CreateAnswer();
                }
                else if (type == "answer")
                {
                    await _peerConnection.SetRemoteDescriptionAsync(new SdpMessage
                    {
                        Type = SdpMessageType.Answer,
                        Content = sdp
                    });
                }
            });
            
            // Lắng nghe ICE candidate từ server
            _signalR.On<string, string, int>("ReceiveIceCandidate", (candidate, sdpMid, sdpMlineIndex) =>
            {
                _peerConnection.AddIceCandidate(new IceCandidate
                {
                    Content = candidate,
                    SdpMid = sdpMid,
                    SdpMlineIndex = sdpMlineIndex
                });
            });

            await _signalR.StartAsync();
            _capture = new ScreenCaptureDXGI();
            _webrtcClient = new WebRTCClient();
            //_webrtcClient.AttachToScreenCapture(_capture);

   
            // Đảm bảo rằng video track đã được thêm vào peer connection
            
            Log.Information("Adding video track...");
            // Tạo video track
            _capture.OnFrameCaptured += _webrtcClient.OnI420AFrame;
            var localTrack = _webrtcClient.CreateLocalVideoTrack();

            // Tạo Transceiver với video track
            var transceiverInit = new TransceiverInitSettings
            {
                Name = "video",
                StreamIDs = new List<string> { "stream1" }
            };

            var videoTransceiver = _peerConnection.AddTransceiver(MediaKind.Video, transceiverInit);
            videoTransceiver.DesiredDirection = Transceiver.Direction.SendOnly;
            videoTransceiver.LocalVideoTrack = localTrack;
            
            // Log trạng thái của video track
            if (videoTransceiver.LocalVideoTrack != null)
            {
                Log.Information("Video track added and is being sent.");
            }
            else
            {
                Log.Warning("Failed to add video track.");
            }

            // Tạo offer nếu là client chủ động
            _peerConnection.CreateOffer(); // Offer sẽ được gửi trong LocalSdpReadytoSend

            // Bắt đầu capture màn hình
            Log.Information("Starting screen capture...");
            _capture.Start();

        }

        /*
        private async Task HandleSdpAsync(string sdp, string type)
        {
            if (type == "offer")
            {
                await _peerConnection.SetRemoteDescriptionAsync(new SdpMessage
                {
                    Type = SdpMessageType.Offer,
                    Content = sdp
                });

                _peerConnection.CreateAnswer();
            }
            else if (type == "answer")
            {
                await _peerConnection.SetRemoteDescriptionAsync(new SdpMessage
                {
                    Type = SdpMessageType.Answer,
                    Content = sdp
                });
            }
        }
        */
        protected override async void OnClosed(EventArgs e)
        {
            try
            {
                // Dừng và hủy ScreenCaptureDXGI
                if (_capture != null)
                {
                    _capture.OnFrameCaptured -= _webrtcClient.OnI420AFrame; // Gỡ sự kiện
                    _capture.Stop();
                    _capture.Dispose();
                    _capture = null;
                    Log.Information("Screen capture stopped and disposed in OnClosed.");
                }

                // Hủy WebRTCClient
                if (_webrtcClient != null)
                {
                    _webrtcClient.Dispose();
                    _webrtcClient = null;
                    Log.Information("WebRTC client disposed in OnClosed.");
                }

                // Đóng và hủy PeerConnection
                if (_peerConnection != null)
                {
                    _peerConnection.Close();
                    _peerConnection.Dispose();
                    _peerConnection = null;
                    Log.Information("Peer connection closed and disposed in OnClosed.");
                }

                // Dừng và hủy SignalR
                if (_signalR != null)
                {
                    await _signalR.StopAsync();
                    await _signalR.DisposeAsync();
                    _signalR = null;
                    Log.Information("SignalR connection stopped and disposed in OnClosed.");
                }
            }
            catch (Exception ex)
            {
                Log.Error(ex, "Lỗi khi đóng cửa sổ");
            }

            base.OnClosed(e);
        }

    }
}
