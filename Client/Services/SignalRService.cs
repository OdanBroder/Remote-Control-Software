using Microsoft.AspNetCore.SignalR.Client;
using System;
using System.ComponentModel;
using System.Runtime.CompilerServices;
using System.Threading.Tasks;
using Client.Models;
using Microsoft.MixedReality.WebRTC;
using System.Collections.Generic;
using Newtonsoft.Json;
using Microsoft.Extensions.Logging;
using Client.Helpers;
using System.Net.Http;
using System.Data.Common;
using Microsoft.AspNetCore.Http.Connections;
using System.Security.Cryptography.X509Certificates;
using Serilog;
using ScreenCaptureI420A;
using Client.Views;
using System.Runtime.InteropServices;

namespace Client.Services
{
    public class SignalRService : IDisposable
    {
        // Add Windows API declarations
        [DllImport("user32.dll")]
        private static extern void mouse_event(uint dwFlags, int dx, int dy, uint dwData, int dwExtraInfo);

        [DllImport("user32.dll")]
        private static extern bool SetCursorPos(int x, int y);

        // Mouse event constants
        private const uint MOUSEEVENTF_LEFTDOWN = 0x0002;
        private const uint MOUSEEVENTF_LEFTUP = 0x0004;
        private const uint MOUSEEVENTF_RIGHTDOWN = 0x0008;
        private const uint MOUSEEVENTF_RIGHTUP = 0x0010;
        private const uint MOUSEEVENTF_MIDDLEDOWN = 0x0020;
        private const uint MOUSEEVENTF_MIDDLEUP = 0x0040;
        private const uint MOUSEEVENTF_ABSOLUTE = 0x8000;
        
        private HubConnection _connection;
        private string _connectionId;
        private bool _isConnected;
        private string _connectionStatus;
        private string _token, _sessionId;
        private SessionService _sessionService;
        private PeerConnection _peerConnection;
        private WebRTCService _webrtcClient;
        private readonly string _hubUrl = AppSettings.BaseApiUri + "/remotecontrolhub";
        private bool _connectionEstablished = false;
        private string _publicKey;
        private string _privateKey;
        private bool _isStreaming;
        private bool _isDisposed;
        private ScreenCaptureDXGI _capture;
        private LocalVideoTrack _localVideoTrack;
        private VideoProcessor _videoProcessor;
        private ScreenCaptureView _streamingWindow;
        private PropertyChangedEventHandler _signalREventHandler;
        private Action<RemoteVideoTrack> _remoteTrackHandler;

        public string ConnectionId
        {
            get => _connectionId;
            set { _connectionId = value; OnPropertyChanged(); }
        }

        public bool IsConnected
        {
            get => _isConnected;
            set { _isConnected = value; OnPropertyChanged(); }
        }

        public string ConnectionStatus
        {
            get => _connectionStatus;
            set { _connectionStatus = value; OnPropertyChanged(); }
        }

        public DateTime? ConnectedSince { get; private set; }

        public event PropertyChangedEventHandler PropertyChanged;
        public event Action<RemoteVideoTrack> OnVideoTrackAdded;

        private void OnPropertyChanged([CallerMemberName] string name = null) =>
            PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(name));

        public SignalRService()
        {
            _token = TokenStorage.LoadToken();
            _sessionId = SessionStorage.LoadSession();
            ConnectionStatus = "Disconnected";
            _videoProcessor = new VideoProcessor();
            _streamingWindow = new ScreenCaptureView();
            _streamingWindow.Show();
        }

        public async Task ConnectToHubAsync(string sessionId)
        {
            if (_connection != null && _connection.State == HubConnectionState.Connected)
                return;

            try
            {
                var handler = new HttpClientHandler
                {
                    ServerCertificateCustomValidationCallback = (message, cert, chain, errors) => true
                };
                _connection = new HubConnectionBuilder()
                    .WithUrl($"{_hubUrl}?sessionId={sessionId}", options =>
                    {
                        options.HttpMessageHandlerFactory = _ => handler;
                        options.AccessTokenProvider = () => Task.FromResult(_token);
                        options.Transports = HttpTransportType.WebSockets | HttpTransportType.ServerSentEvents | HttpTransportType.LongPolling;
                        options.SkipNegotiation = false;
                    })
                    .WithAutomaticReconnect(new[] { TimeSpan.Zero, TimeSpan.FromSeconds(2), TimeSpan.FromSeconds(5), TimeSpan.FromSeconds(10), TimeSpan.FromSeconds(20) })
                    .ConfigureLogging(logging =>
                    {
                        logging.SetMinimumLevel(LogLevel.Debug);
                    })
                    .Build();

                RegisterEventHandlers();

                ConnectionStatus = "Connecting...";
                await _connection.StartAsync();

                // Wait for connection to be fully established
                int retryCount = 0;
                const int maxRetries = 5;
                const int checkInterval = 1000; // 1 second

                while (retryCount < maxRetries)
                {
                    if (_connectionEstablished && _connection.State == HubConnectionState.Connected)
                    {
                        Log.Information("Connection is fully established and synchronized");
                        break;
                    }

                    Log.Information("Waiting for connection to be fully established... Attempt {Attempt}/{MaxRetries}", retryCount + 1, maxRetries);
                    await Task.Delay(checkInterval);
                    retryCount++;
                }

                if (!_connectionEstablished || _connection.State != HubConnectionState.Connected)
                {
                    throw new Exception($"Failed to establish connection after {maxRetries} attempts. Current state: {_connection.State}, Connection established: {_connectionEstablished}");
                }

                IsConnected = true;
                ConnectionStatus = "Connected";
                ConnectionId = _connection.ConnectionId;
                ConnectedSince = DateTime.UtcNow;
            }
            catch (Exception ex)
            {
                IsConnected = false;
                ConnectionStatus = $"Connection failed: {ex.Message}";
                Log.Error(ex, "Hub connection error");
                throw;
            }
        }

        public async Task DisconnectToHubAsync()
        {
            if (_connection != null)
            {
                await _connection.StopAsync();
                await _connection.DisposeAsync();
                IsConnected = false;
                ConnectionStatus = "Disconnected";
                ConnectedSince = null;
            }
        }

        private void RegisterEventHandlers()
        {
            _connection.On<string>("ConnectionEstablished", (connectionId) =>
            {
                Log.Information("Connection established with ID: {ConnectionId}", connectionId);
                _connectionEstablished = true;
                ConnectionId = connectionId;
                ConnectionStorage.SaveConnectionId(ConnectionId);
                IsConnected = true;
                ConnectionStatus = "Connected";
                ConnectedSince = DateTime.UtcNow;
            });

            _connection.On<string>("Error", (message) =>
            {
                Log.Error("Received error from server: {Message}", message);
                ConnectionStatus = $"Error: {message}";
            });

            _connection.On<string>("PeerConnected", (peerId) =>
            {
                Log.Information("Peer connected: {PeerId}", peerId);
            });

            _connection.On<string>("PeerDisconnected", (peerId) =>
            {
                Log.Information("Peer disconnected: {PeerId}", peerId);
            });

            _connection.On<string>("ReceiveInput", async (serializedAction) =>
            {
                try
                {
                    Console.WriteLine($"[DEBUG] Received input action: {serializedAction}");
                    var action = JsonConvert.DeserializeObject<InputAction>(serializedAction);

                    if (action.Type.ToLower() == "mouse")
                    {
                        ExecuteMouseAction(action);
                    }
                    else if (action == null || string.IsNullOrWhiteSpace(action.Type) || string.IsNullOrWhiteSpace(action.Action))
                        throw new Exception("Invalid input action format");

                    Console.WriteLine($"[DEBUG] Parsed action details:\n" +
                        $"Type: {action.Type}\n" +
                        $"Action: {action.Action}\n" +
                        $"Key: {action.Key ?? "N/A"}\n" +
                        $"Modifiers: {JsonConvert.SerializeObject(action.Modifiers ?? new string[0])}\n" +
                        $"X: {action.X ?? -1}\n" +
                        $"Y: {action.Y ?? -1}\n" +
                        $"Button: {action.Button ?? "N/A"}\n" +
                        $"Full Action: {JsonConvert.SerializeObject(action, Formatting.Indented)}");

                    await Task.Delay(100); // Small delay to ensure proper sequencing

                }
                catch (Exception ex)
                {
                    Console.WriteLine($"[ERROR] Error processing input: {ex.Message}");
                    await _connection.InvokeAsync("ReportInputError", new
                    {
                        error = ex.Message,
                        rawAction = serializedAction
                    });
                }
            });

            _connection.On<string>("ScreenDataUpdated", (imageBase64) =>
            {
                Log.Information("Received screen update: {Length} bytes", imageBase64.Length);
            });

            _connection.On<dynamic>("ReceiveWebRTCSignal", async payload =>
            {
                try
                {
                    var signalData = JsonConvert.DeserializeObject<WebRTCSignal>(JsonConvert.SerializeObject(payload));
                    Log.Information("Received WebRTC {SignalType} signal from {ConnectionId}", signalData.SignalType, signalData.ConnectionId);

                    switch (signalData.SignalType.ToLower())
                    {
                        case "offer":
                            await HandleSdpAsync(signalData.ConnectionId, signalData.SignalData.ToString(), "offer");
                            break;
                        case "answer":
                            await HandleSdpAsync(signalData.ConnectionId, signalData.SignalData.ToString(), "answer");
                            break;
                        case "ice-candidate":
                            var candidate = JsonConvert.DeserializeObject<IceCandidate>(signalData.SignalData.ToString());
                            await HandleIceCandidateAsync(
                                signalData.ConnectionId,
                                candidate.Content,
                                candidate.SdpMid,
                                candidate.SdpMlineIndex
                            );
                            break;
                    }
                }
                catch (Exception ex)
                {
                    Log.Error(ex, "Error processing WebRTC signal");
                }
            });

            _connection.Reconnecting += error =>
            {
                Log.Information("Connection lost. Reconnecting... Error: {Error}", error);
                IsConnected = false;
                ConnectionStatus = "Reconnecting...";
                return Task.CompletedTask;
            };

            _connection.Reconnected += connectionId =>
            {
                Log.Information("Reconnected successfully. Connection ID: {ConnectionId}", connectionId);
                IsConnected = true;
                ConnectionStatus = "Reconnected";
                ConnectionId = connectionId;
                return Task.CompletedTask;
            };

            _connection.Closed += error =>
            {
                Log.Information("Connection closed. Error: {Error}", error);
                IsConnected = false;
                ConnectionStatus = "Disconnected";
                ConnectedSince = null;
                return Task.CompletedTask;
            };
        }

        public async Task<ApiResponse> StartStreaming(bool isStreamer = true)
        {
            try
            {
                if (_isStreaming)
                {
                    return new ApiResponse { Success = false, Message = "Already streaming" };
                }

                if (!IsConnected)
                {
                    Log.Error("SignalR is not connected");
                    return new ApiResponse { Success = false, Message = "SignalR is not connected" };
                }

                if (_peerConnection != null)
                {
                    Log.Warning("PeerConnection already exists");
                    return new ApiResponse { Success = false, Message = "PeerConnection already initialized" };
                }

                await InitializePeerConnection(isStreamer);

                if (isStreamer)
                {
                    SetupStreaming();
                }
                else
                {
                    SetupViewing();
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

        private async Task InitializePeerConnection(bool isStreamer)
        {
            _peerConnection = new PeerConnection();
            var config = new PeerConnectionConfiguration
            {
                IceServers = new List<IceServer>
                {
                    new IceServer { Urls = { "stun:stun.l.google.com:19302" } }
                }
            };

            await _peerConnection.InitializeAsync(config);

            _peerConnection.LocalSdpReadytoSend += async (SdpMessage msg) =>
            {
                var signal = new WebRTCSignal
                {
                    SessionIdentifier = _sessionId,
                    ConnectionId = ConnectionId,
                    SignalType = msg.Type.ToString().ToLower(),
                    SignalData = msg.Content
                };

                await SendWebRTCSignal(signal);
            };

            _peerConnection.IceCandidateReadytoSend += async (IceCandidate candidate) =>
            {
                var signal = new WebRTCSignal
                {
                    SessionIdentifier = _sessionId,
                    ConnectionId = ConnectionId,
                    SignalType = "ice-candidate",
                    SignalData = new
                    {
                        candidate = candidate.Content,
                        sdpMid = candidate.SdpMid,
                        sdpMLineIndex = candidate.SdpMlineIndex
                    }
                };

                await SendWebRTCSignal(signal);
            };
            _peerConnection.VideoTrackAdded += track =>
            {
                Log.Information("Video track added: {Name}", track.Name);

                track.I420AVideoFrameReady += frame =>
                {
                    Log.Information("Y data size: {YSize}, U data size: {USize}, V data size: {VSize}, A data size: {ASize}",
                        frame.dataY, frame.dataU, frame.dataV, frame.dataA);

                    //HandleVideoTrackAdded(track); // Gọi hàm xử lý frame
                };
            };
        }

        private void SetupStreaming()
        {
            if (_connection == null || _connection.State != HubConnectionState.Connected)
            {
                Log.Warning("Connection SignalR should be run first...");
                return;
            }
            try
            {
                _capture = new ScreenCaptureDXGI();
                _webrtcClient = new WebRTCService();

                _localVideoTrack = _webrtcClient.CreateLocalVideoTrack();
                _capture.OnFrameCaptured += _webrtcClient.OnI420AFrame;

                var transceiverInit = new TransceiverInitSettings
                {
                    Name = "video",
                    StreamIDs = new List<string> { "stream1" }
                };

                var videoTransceiver = _peerConnection.AddTransceiver(MediaKind.Video, transceiverInit);
                videoTransceiver.DesiredDirection = Transceiver.Direction.SendOnly;
                videoTransceiver.LocalVideoTrack = _localVideoTrack;

                if (videoTransceiver.LocalVideoTrack == null)
                {
                    throw new InvalidOperationException("Failed to add video track");
                }

                Log.Information("Creating offer...");
                if (!_peerConnection.CreateOffer())
                {
                    throw new InvalidOperationException("Offer creation failed");
                }

                _capture.Start();
            }
            catch (Exception ex)
            {
                Log.Error($"Exception when start streaming {ex.Message}");
            }
        }

        private void SetupViewing()
        {
            if (_connection == null || _connection.State != HubConnectionState.Connected)
            {
                Log.Warning("Connection SignalR should be run first...");
                return;
            }
            var transceiverInit = new TransceiverInitSettings
            {
                Name = "video",
                StreamIDs = new List<string> { "stream1" }
            };

            var videoTransceiver = _peerConnection.AddTransceiver(MediaKind.Video, transceiverInit);
            videoTransceiver.DesiredDirection = Transceiver.Direction.ReceiveOnly;
        }

        public void HandleIceCandidateAsync(string connectionId, string candidate, string sdpMid, int sdpMlineIndex)
        {
            if (_peerConnection == null)
            {
                Log.Error("PeerConnection not found");
                return;
            }
            try
            {
                Log.Information("Adding ICE candidate for {ConnectionId}", connectionId);
                _peerConnection.AddIceCandidate(new IceCandidate
                {
                    Content = candidate,
                    SdpMid = sdpMid,
                    SdpMlineIndex = sdpMlineIndex
                });
            }
            catch (Exception ex)
            {
                Log.Error(ex, "Error adding ICE candidate for {ConnectionId}", connectionId);
            }
        }

        public async Task HandleSdpAsync(string connectionId, string sdp, string type)
        {
            if (_peerConnection == null)
            {
                Log.Error("PeerConnection not found");
                return;
            }

            try
            {
                Log.Information("Handling SDP of type {Type} for {ConnectionId}", type, connectionId);
                var sdpMessage = new SdpMessage
                {
                    Type = type.ToLower() == "offer" ? SdpMessageType.Offer : SdpMessageType.Answer,
                    Content = sdp
                };

                await _peerConnection.SetRemoteDescriptionAsync(sdpMessage);

                if (type.ToLower() == "offer")
                {
                    Log.Information("Creating answer for {ConnectionId}", connectionId);
                    _peerConnection.CreateAnswer();
                }
            }
            catch (Exception ex)
            {
                Log.Error(ex, "Error handling SDP message for {ConnectionId}", connectionId);
            }
        }

        public async Task SendWebRTCSignal(WebRTCSignal signal)
        {
            if (_connection == null || _connection.State != HubConnectionState.Connected)
            {
                Log.Error("SignalR connection is not established. State: {State}", _connection?.State);
                throw new InvalidOperationException("SignalR connection is not established");
            }

            try
            {
                _sessionId = SessionStorage.LoadSession();
                Log.Information("Sending WebRTC signal - Type: {SignalType}, SessionId: {SessionId}", signal.SignalType, _sessionId);
                await _connection.InvokeAsync("SendWebRTCSignal", _sessionId, signal);
                Log.Information("WebRTC signal sent successfully - Type: {SignalType}", signal.SignalType);
            }
            catch (Exception ex)
            {
                Log.Error(ex, "Failed to send WebRTC signal - Type: {SignalType}", signal.SignalType);
                throw;
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


        public async Task SendInputActionAsync(string sessionId, string serializedAction)
        {
            if (_connection == null || _connection.State != HubConnectionState.Connected)
            {
                throw new InvalidOperationException("SignalR connection is not established");
            }

            await _connection.InvokeAsync("SendInputAction", sessionId, serializedAction);
        }

        private void ExecuteMouseAction(InputAction action)
        {
            try
            {
                Log.Information($"Executing mouse action: {JsonConvert.SerializeObject(action)}");
                return;
                // Convert nullable int to int for mouse coordinates
                int x = action.X ?? 0;
                int y = action.Y ?? 0;

                // Set cursor position
                SetCursorPos(x, y);

                // Execute mouse action based on type
                switch (action.Action.ToLower())
                {
                    case "mousedown":
                        switch (action.Button?.ToLower())
                        {
                            case "left":
                                mouse_event(MOUSEEVENTF_LEFTDOWN, x, y, 0, 0);
                                break;
                            case "right":
                                mouse_event(MOUSEEVENTF_RIGHTDOWN, x, y, 0, 0);
                                break;
                            case "middle":
                                mouse_event(MOUSEEVENTF_MIDDLEDOWN, x, y, 0, 0);
                                break;
                        }
                        break;

                    case "mouseup":
                        switch (action.Button?.ToLower())
                        {
                            case "left":
                                mouse_event(MOUSEEVENTF_LEFTUP, x, y, 0, 0);
                                break;
                            case "right":
                                mouse_event(MOUSEEVENTF_RIGHTUP, x, y, 0, 0);
                                break;
                            case "middle":
                                mouse_event(MOUSEEVENTF_MIDDLEUP, x, y, 0, 0);
                                break;
                        }
                        break;

                    case "mousemove":
                        // Cursor position is already set above
                        break;
                }
            }
            catch (Exception ex)
            {
                Log.Error($"Failed to execute mouse action: {ex.Message}");
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
                    _connection?.DisposeAsync();
                }
                _isDisposed = true;
            }
        }

        ~SignalRService()
        {
            Dispose(false);
        }
    }
}