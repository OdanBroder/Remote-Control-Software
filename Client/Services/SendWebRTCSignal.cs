using Newtonsoft.Json;
using System;
using System.Net.Http;
using System.Threading.Tasks;
using System.Net.Http.Headers;
using Client.Models;
using Client.Services;
using Client.Helpers;
using Microsoft.MixedReality.WebRTC;
using ScreenCaptureI420A;
using System.ComponentModel;
using Serilog;
using System.Collections.Generic;
using System.Data.Common;
using Video;
using Client.Views;

namespace Client.Services
{
    public class SendWebRTCSignal : IDisposable
    {
        private readonly HttpClient _httpClient;
        private readonly string baseUrl = AppSettings.BaseApiUri + "/remotecontrolhub";
        private PeerConnection _peerConnection;
        private VideoProcessor videoProcessor = new VideoProcessor();
        ScreenCaptureView streamingWindow = new ScreenCaptureView();
        private WebRTCService _webrtcClient;
        private ScreenCaptureDXGI _capture;
        private readonly SignalRService _signalRService;
        private string _connectionStatus;
        private string _connectionId;
        private DateTime? _connectedSince;
        private LocalVideoTrack _localVideoTrack;
        private bool _isDisposed;
        private bool _isStreaming;
        private PropertyChangedEventHandler _signalREventHandler;
        private Action<RemoteVideoTrack> _remoteTrackHandler;

        public string ConnectionStatus
        {
            get => _connectionStatus;
            set
            {
                if (_connectionStatus != value)
                {
                    _connectionStatus = value;
                    OnPropertyChanged(nameof(ConnectionStatus));
                }
            }
        }

        public string ConnectionId
        {
            get => _connectionId;
            set
            {
                if (_connectionId != value)
                {
                    _connectionId = value;
                    OnPropertyChanged(nameof(ConnectionId));
                }
            }
        }

        public DateTime? ConnectedSince
        {
            get => _connectedSince;
            set
            {
                if (_connectedSince != value)
                {
                    _connectedSince = value;
                    OnPropertyChanged(nameof(ConnectedSince));
                }
            }
        }

        public event PropertyChangedEventHandler PropertyChanged;
        protected void OnPropertyChanged(string propertyName = null)
        {
            PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(propertyName));
        }

        public SendWebRTCSignal(SignalRService signalRService)
        {
            _signalRService = signalRService;
            SubscribeToSignalREvents();
            _signalRService.OnVideoTrackAdded += OnRemoteVideoTrackAdded;
             streamingWindow.Show();

        }

        private void OnRemoteVideoTrackAdded(RemoteVideoTrack track)
        {
            Log.Information("Remote video track added: {TrackName}", track.Name);
            // Handle the remote video track (e.g., display it in UI)
            OnRemoteTrackReceived?.Invoke(track);
        }

        public event Action<RemoteVideoTrack> OnRemoteTrackReceived;

        public void SetAuthToken(string token)
        {
            _httpClient.DefaultRequestHeaders.Authorization =
                new AuthenticationHeaderValue("Bearer", token);
        }

        private void SubscribeToSignalREvents()
        {
            _signalREventHandler = (s, e) =>
            {
                switch (e.PropertyName)
                {
                    case nameof(SignalRService.ConnectionStatus):
                        ConnectionStatus = _signalRService.ConnectionStatus;
                        break;
                    case nameof(SignalRService.ConnectionId):
                        ConnectionId = _signalRService.ConnectionId;
                        if (!string.IsNullOrEmpty(_signalRService.ConnectionId))
                        {
                            ConnectedSince = DateTime.UtcNow;
                        }
                        break;
                    case nameof(SignalRService.IsConnected):
                        if (!_signalRService.IsConnected)
                        {
                            ConnectedSince = null;
                        }
                        break;
                }
            };
            _signalRService.PropertyChanged += _signalREventHandler;

            _remoteTrackHandler = OnRemoteVideoTrackAdded;
            _signalRService.OnVideoTrackAdded += _remoteTrackHandler;

            // WebRTC signal handlers

        }

        public async Task<ApiResponse> StartStreaming(bool isStreamer = true)
        {
            try
            {
                if (_isStreaming)
                {
                    return new ApiResponse { Success = false, Message = "Already streaming" };
                }
                if (!_signalRService.IsConnected)
                {
                    Log.Error("SignalR is not working");
                    return new ApiResponse { Success = false, Message = "SignalR is not working" };
                }
                if (_peerConnection != null)
                {
                    Log.Warning("PeerConnection already exists. Aborting initialization.");
                    return new ApiResponse { Success = false, Message = "PeerConnection already initialized." };
                }
                // Initialize WebRTC peer connection
                _peerConnection = new PeerConnection();
                var config = new PeerConnectionConfiguration
                {
                    IceServers = new List<IceServer>
                    {
                        new IceServer { Urls = { "stun:stun.l.google.com:19302" } }
                    }
                };

                await _peerConnection.InitializeAsync(config);
                Log.Information("Initialize successful");

                // Set up WebRTC event handlers
                _peerConnection.LocalSdpReadytoSend += async (SdpMessage msg) =>
                {
                    var signal = new WebRTCSignal
                    {
                        SessionIdentifier = _signalRService.ConnectionId,
                        ConnectionId = _signalRService.ConnectionId,
                        SignalType = msg.Type.ToString().ToLower(),
                        SignalData = msg.Content
                    };

                    await _signalRService.SendWebRTCSignal(signal);
                };

                _peerConnection.IceCandidateReadytoSend += async (IceCandidate candidate) =>
                {
                    var signal = new WebRTCSignal
                    {
                        SessionIdentifier = _signalRService.ConnectionId,
                        ConnectionId = _signalRService.ConnectionId,
                        SignalType = "ice-candidate",
                        SignalData = new
                        {
                            candidate = candidate.Content,
                            sdpMid = candidate.SdpMid,
                            sdpMLineIndex = candidate.SdpMlineIndex
                        }
                    };

                    await _signalRService.SendWebRTCSignal(signal);
                };
                _peerConnection.VideoTrackAdded += track =>
                {
                    Log.Information("Video track added: {Name}", track.Name);

                    track.I420AVideoFrameReady += frame =>
                    {
                        Console.WriteLine("Frame received: Width = {Width}, Height = {Height}", frame.width, frame.height);

                        Log.Information("Y data size: {YSize}, U data size: {USize}, V data size: {VSize}, A data size: {ASize}",
                            frame.dataY, frame.dataU, frame.dataV, frame.dataA);
                        var bitmap = videoProcessor.ConvertI420AToRGB(frame);
                        if (bitmap != null)
                        {
                            streamingWindow.Dispatcher.BeginInvoke(new Action(() =>
                            {
                                streamingWindow.UpdateFrame(bitmap);
                            }));

                        }
                    };
                };
                if (isStreamer)
                {
                    // Setup for streaming
                    _capture = new ScreenCaptureDXGI();
                    _webrtcClient = new WebRTCService();

                    // Create and add local video track
                    _localVideoTrack = _webrtcClient.CreateLocalVideoTrack();

                    _capture.OnFrameCaptured += _webrtcClient.OnI420AFrame;
                    var localTrack = _webrtcClient.CreateLocalVideoTrack();

                    // Create Transceiver with video track
                    var transceiverInit = new TransceiverInitSettings
                    {
                        Name = "video",
                        StreamIDs = new List<string> { "stream1" }
                    };

                    var videoTransceiver = _peerConnection.AddTransceiver(MediaKind.Video, transceiverInit);
                    videoTransceiver.DesiredDirection = Transceiver.Direction.SendOnly;
                    videoTransceiver.LocalVideoTrack = localTrack;
                    if (videoTransceiver.LocalVideoTrack != null)
                    {
                        Log.Information("Video track added and is being sent.");
                    }
                    else
                    {
                        Log.Warning("Failed to add video track.");
                    }

                    // Create and send offer
                    Log.Information("Creating offer...");
                    bool offer = _peerConnection.CreateOffer();
                    if(!offer)
                    {
                        Log.Information("Fail to create offer.");
                        throw new InvalidOperationException("Offer creation failed.");
                    }    
                    Log.Information("Offer created and sent.");

                    Log.Information("Starting screen capture...");
                    _capture.Start();
                }
                else
                {
                    // Setup for receiving
                    var transceiverInit = new TransceiverInitSettings
                    {
                        Name = "video",
                        StreamIDs = new List<string> { "stream1" }
                    };

                    var videoTransceiver = _peerConnection.AddTransceiver(MediaKind.Video, transceiverInit);
                    videoTransceiver.DesiredDirection = Transceiver.Direction.ReceiveOnly;
                }

                _isStreaming = true;
                return new ApiResponse { Success = true, Message = "Streaming started successfully" };
            }
            catch (Exception ex)
            {
                Log.Error(ex, "Failed to start streaming");
                return new ApiResponse { Success = false, Message = $"Failed to start streaming: {ex.Message}" };
            }
        }

        public void StopStreaming()
        {
            try
            {
                if (_capture != null)
                {
                    _capture.Stop();
                }

                if (_localVideoTrack != null)
                {
                    _localVideoTrack.Dispose();
                    _localVideoTrack = null;
                }

                if (_peerConnection != null)
                {
                    _peerConnection.Close();
                    _peerConnection.Dispose();
                    _peerConnection = null;
                }

                _isStreaming = false;
            }
            catch (Exception ex)
            {
                Log.Error(ex, "Error stopping streaming");
            }
        }

        public void Dispose()
        {
            Dispose(true);
            GC.SuppressFinalize(this);
        }

        protected virtual void Dispose(bool disposing)
        {
            if (!_isDisposed)
            {
                if (disposing)
                {
                    StopStreaming();
                    _webrtcClient?.Dispose();
                    _capture?.Dispose();
                    _httpClient?.Dispose();
                    _signalRService.PropertyChanged -= _signalREventHandler;
                    _signalRService.OnVideoTrackAdded -= _remoteTrackHandler;

                }
                _isDisposed = true;
            }
        }

        ~SendWebRTCSignal()
        {
            Dispose(false);
        }
    }
}
