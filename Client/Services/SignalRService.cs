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
using Microsoft.AspNetCore.Http.Connections;
using Serilog;
using ScreenCaptureI420A;
using Client.Views;
using System.Runtime.InteropServices;
using System.Windows.Forms;
using System.Runtime.InteropServices.ComTypes;
using System.Linq;
using System.Drawing;

using System.Windows.Media.Imaging;
using System.Windows.Media;
using System.Windows;
using Video;
namespace Client.Services
{
    public class SignalRService : IDisposable
    {
        // Add Windows API declarations
        [DllImport("user32.dll")]
        private static extern void mouse_event(uint dwFlags, int dx, int dy, uint dwData, int dwExtraInfo);

        [DllImport("user32.dll")]
        private static extern bool SetCursorPos(int x, int y);

        [DllImport("user32.dll")]
        private static extern void keybd_event(byte bVk, byte bScan, uint dwFlags, int dwExtraInfo);

        [DllImport("user32.dll")]
        private static extern bool BlockInput(bool fBlockIt);

        [DllImport("user32.dll")]
        private static extern bool GetWindowRect(IntPtr hWnd, out RECT lpRect);

        [DllImport("user32.dll")]
        private static extern bool GetClientRect(IntPtr hWnd, out RECT lpRect);

        [DllImport("user32.dll")]
        private static extern bool ClientToScreen(IntPtr hWnd, ref POINT lpPoint);

        // Mouse event constants
        private const uint MOUSEEVENTF_LEFTDOWN = 0x0002;
        private const uint MOUSEEVENTF_LEFTUP = 0x0004;
        private const uint MOUSEEVENTF_RIGHTDOWN = 0x0008;
        private const uint MOUSEEVENTF_RIGHTUP = 0x0010;
        private const uint MOUSEEVENTF_MIDDLEDOWN = 0x0020;
        private const uint MOUSEEVENTF_MIDDLEUP = 0x0040;
        private const uint MOUSEEVENTF_ABSOLUTE = 0x8000;
        private const uint MOUSEEVENTF_MOVE = 0x0001;

        // Keyboard event constants
        private const uint KEYEVENTF_KEYUP = 0x0002;
        private const uint KEYEVENTF_EXTENDEDKEY = 0x0001;

        private WriteableBitmap? _writeableBitmap = null;

        private HubConnection _connection;
        private string _connectionId;
        private bool _isConnected;
        private string _connectionStatus;
        private string _token, _sessionId;
        private PeerConnection pc;
        private WebRTCService _webrtcClient;
        private readonly string _hubUrl = AppSettings.BaseApiUri + "/remotecontrolhub";
        private bool _connectionEstablished = false;
        private string _publicKey;
        private string _privateKey;
        private bool _isStreaming;
        private bool _isDisposed;
        private ScreenCaptureDXGI _capture;
        private LocalVideoTrack _localVideoTrack;
        private VideoHelper _videoProcessor;
        ScreenCaptureView streamingWindow;
        private PropertyChangedEventHandler _signalREventHandler;
        private Action<RemoteVideoTrack> _remoteTrackHandler;
        private bool isSender = true;
        private bool _isInputBlocked = false;
        private Rectangle _blockedRegion;
        private bool _isInBlockedRegion = false;
        private ScreenCaptureView _streamingWindow;

        [StructLayout(LayoutKind.Sequential)]
        private struct RECT
        {
            public int Left;
            public int Top;
            public int Right;
            public int Bottom;
        }

        [StructLayout(LayoutKind.Sequential)]
        private struct POINT
        {
            public int X;
            public int Y;
        }

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
                    var action = JsonConvert.DeserializeObject<InputAction>(serializedAction);
                    if (action == null || string.IsNullOrWhiteSpace(action.Type) || string.IsNullOrWhiteSpace(action.Action))
                    {
                        throw new Exception("Invalid input action format");
                    }
                    // Console.WriteLine($"[DEBUG] Parsed action details:\n" +
                    //     $"Type: {action.Type}\n" +
                    //     $"Action: {action.Action}\n" +
                    //     $"Key: {action.Key ?? "N/A"}\n" +
                    //     $"Modifiers: {JsonConvert.SerializeObject(action.Modifiers ?? new string[0])}\n" +
                    //     $"X: {action.X ?? -1}\n" +
                    //     $"Y: {action.Y ?? -1}\n" +
                    //     $"Button: {action.Button ?? "N/A"}\n" +
                    //     $"Full Action: {JsonConvert.SerializeObject(action, Formatting.Indented)}");

                    // await Task.Delay(100); // Small delay to ensure proper sequencing
                    ExecuteInputAction(action);
                }
                catch (Exception ex)
                {
                    Log.Error($"Error processing input: {ex.Message}");
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

            _connection.On<WebRTCSignal>("ReceiveWebRTCSignal", async payload =>
            {
                try
                {
                    if (pc == null || !pc.IsConnected) await GetOrCreatePeer();
                    var signalData = JsonConvert.DeserializeObject<WebRTCSignal>(JsonConvert.SerializeObject(payload));
                    Console.WriteLine($"Debug: {signalData}");
                    Log.Information("Received WebRTC {SignalType} signal from {ConnectionId}", signalData.SignalType, signalData.ConnectionId);

                    switch (signalData.SignalType.ToLower())
                    {
                        case "offer":
                            await HandleSdpAsync(signalData.ConnectionId, signalData.Content, "offer");
                            break;
                        case "answer":
                            await HandleSdpAsync(signalData.ConnectionId, signalData.Content, "answer");
                            break;
                        case "ice-candidate":
                            await HandleIceCandidateAsync(
                                signalData.ConnectionId,
                                signalData.Content,
                                signalData.SdpMid,
                                signalData.SdpMLineIndex ?? 0
                            );
                            break;
                    }
                }
                catch (Exception ex)
                {
                    Log.Error(ex, "Error processing WebRTC signal");
                }
            });

            _connection.On<int, string, long>("FileTransferRequested", (transferId, fileName, fileSize) =>
            {
                System.Windows.Application.Current.Dispatcher.Invoke(() =>
                {
                    var vm = new FileReceiveRequestViewModel
                    {
                        TransferId = transferId,
                        FileName = fileName,
                        FileSize = fileSize
                    };
                    var dialog = new FileReceiveRequestView(vm);
                    var result = dialog.ShowDialog();

                    if (result == true)
                    {
                        _ = AcceptFileTransfer(transferId);
                    }
                    else
                    {
                        _ = RejectFileTransfer(transferId);
                    }
                });
            });

            _connection.On<int>("FileTransferAccepted", transferId =>
            {
                System.Windows.Application.Current.Dispatcher.Invoke(() =>
                {
                    var vm = new FileTransferViewModel();
                    //var view = new FileTransferView(vm);

                    vm.StartTcpFileTransfer(transferId);
                });
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

        public async Task AcceptFileTransfer(int transferId)
        {
            if (_connection != null && _connection.State == HubConnectionState.Connected)
            {
                await _connection.InvokeAsync("AcceptFileTransfer", transferId);
            }
        }

        public async Task RejectFileTransfer(int transferId)
        {
            if (_connection != null && _connection.State == HubConnectionState.Connected)
            {
                await _connection.InvokeAsync("RejectFileTransfer", transferId);
            }
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

                if (pc != null)
                {
                    Log.Warning("PeerConnection already exists");
                    return new ApiResponse { Success = false, Message = "PeerConnection already initialized" };
                }
                isSender = isStreamer;

                if (isStreamer) await SetupStreaming();
                else await SetupViewing();
                _isStreaming = true;
                return new ApiResponse { Success = true, Message = "Streaming started successfully" };
            }
            catch (Exception ex)
            {
                Log.Error(ex, "Failed to start streaming");
                return new ApiResponse { Success = false, Message = $"Failed to start streaming: {ex.Message}" };
            }
        }

        private async Task GetOrCreatePeer()
        {
            if (pc != null)
            {
                return;
            }
            string connectionId = ConnectionStorage.LoadConnectionId();
            string sessionId = SessionStorage.LoadSession();
            Log.Debug("Peer connection created: {peerId}", connectionId);
            pc = new PeerConnection();
            var config = new PeerConnectionConfiguration
            {
                IceServers = new List<IceServer>
                {
                    new IceServer { Urls = { "stun:stun.l.google.com:19302" } }
                }
            };

            await pc.InitializeAsync(config);
            // set up environment
            if (isSender)
            {
                _capture = new ScreenCaptureDXGI();
                _webrtcClient = new WebRTCService();
                _capture.OnFrameCaptured += _webrtcClient.OnArgbFrame;
                var localtrack = _webrtcClient.CreateLocalVideoTrack();
                // Tạo Transceiver với video track
                var transceiverInit = new TransceiverInitSettings
                {
                    Name = "video",
                    StreamIDs = new List<string> { "stream1" }
                };
                var videoTransceiver = pc.AddTransceiver(MediaKind.Video, transceiverInit);
                videoTransceiver.DesiredDirection = Transceiver.Direction.SendOnly;
                videoTransceiver.LocalVideoTrack = localtrack;

                // Log trạng thái của video track
                if (videoTransceiver.LocalVideoTrack != null)
                {
                    Log.Information("Video track added and is being sent.");
                }
                else
                {
                    Log.Warning("Failed to add video track.");
                }

                Log.Information("Starting screen capture...");
            }
            // Set up event handlers
            pc.LocalSdpReadytoSend += async msg =>
            {
                Log.Debug("Content of local sdp: {Content}", msg.Content);
                var message = new WebRTCSignal
                {
                    Content = msg.Content,
                    SignalType = msg.Type.ToString().ToLower(),
                    SessionIdentifier = sessionId,
                    ConnectionId = ConnectionId,
                };
                await SendWebRTCSignal(message);
            };
            pc.IceCandidateReadytoSend += async cand =>
            {
                var message = new WebRTCSignal
                {
                    SessionIdentifier = sessionId,
                    ConnectionId = ConnectionId,
                    SignalType = "ice-candidate",
                    Content = cand.Content,
                    SdpMid = cand.SdpMid,
                    SdpMLineIndex = cand.SdpMlineIndex
                };
                await SendWebRTCSignal(message);
            };
            Log.Information($"Video track is adding...");

            pc.VideoTrackAdded += track =>
            {
                Log.Information("Video track added: {Name}", track.Name);

                track.Argb32VideoFrameReady += frame =>
                {
                    try
                    {
                        int width = (int)frame.width;
                        int height = (int)frame.height;
                        int stride = (int)frame.stride;
                        int bufferSize = stride * height;

                        Log.Information("Processing ARGB frame: {Width}x{Height}, stride={Stride}", width, height, stride);

                        byte[] argbData = new byte[bufferSize];
                        Marshal.Copy(frame.data, argbData, 0, bufferSize);

                        if (streamingWindow != null)
                        {
                            streamingWindow.Dispatcher.BeginInvoke(new Action(() =>
                            {
                                try
                                {
                                    if (_writeableBitmap == null ||
                                        _writeableBitmap.PixelWidth != width ||
                                        _writeableBitmap.PixelHeight != height)
                                    {
                                        _writeableBitmap = new WriteableBitmap(
                                            width,
                                            height,
                                            96,
                                            96,
                                            PixelFormats.Bgra32, // hoặc PixelFormats.Pbgra32 nếu có alpha premultiplied
                                            null);

                                        streamingWindow.CaptureImage.Source = _writeableBitmap;
                                    }
                                    _writeableBitmap.Lock();

                                    Marshal.Copy(argbData, 0, _writeableBitmap.BackBuffer, argbData.Length);

                                    _writeableBitmap.AddDirtyRect(new Int32Rect(0, 0, width, height));
                                    _writeableBitmap.Unlock();

                                    Log.Debug("ARGB frame rendered successfully.");
                                }
                                catch (Exception ex)
                                {
                                    Log.Error(ex, "Error rendering ARGB frame to UI.");
                                }
                            }));
                        }
                        else
                        {
                            Log.Warning("Streaming window not ready.");
                        }
                    }
                    catch (Exception ex)
                    {
                        Log.Error(ex, "Error processing ARGB video frame.");
                    }
                };

            };
            if (isSender)
            {
                Log.Information("Creating offer...");
                pc.CreateOffer();
            }
        }


        private async Task SetupStreaming()
        {
            if (_connection == null || _connection.State != HubConnectionState.Connected)
            {
                Log.Warning("Connection SignalR should be run first...");
                return;
            }
            try
            {
                await GetOrCreatePeer();
                _capture.Start();
            }
            catch (Exception ex)
            {
                Log.Error($"Exception when start streaming {ex.Message}");
            }
        }

        private async Task SetupViewing()
        {
            if (_connection == null || _connection.State != HubConnectionState.Connected)
            {
                Log.Warning("Connection SignalR should be run first...");
                return;
            }
            try
            {
                Log.Information("Setting up viewing mode...");
                streamingWindow = new ScreenCaptureView(this);
                _videoProcessor = new VideoHelper();
                streamingWindow.Show();
                await GetOrCreatePeer();
                Log.Information("Viewing setup completed");
            }
            catch (Exception ex)
            {
                Log.Error(ex, "Error in SetupViewing");
                throw;
            }
        }

        public async Task HandleIceCandidateAsync(string connectionId, string candidate, string sdpMid, int sdpMlineIndex)
        {
            if (pc == null || !pc.Initialized)
            {
                await GetOrCreatePeer();
            }

            try
            {
                Log.Information("Adding ICE candidate for {ConnectionId}", connectionId);
                pc.AddIceCandidate(new IceCandidate
                {
                    Content = candidate,
                    SdpMid = sdpMid,
                    SdpMlineIndex = sdpMlineIndex
                });
            }
            catch (Exception ex)
            {
                Log.Error(ex, "Error adding ICE candidate for {ConnectionId}", connectionId);
                throw new InvalidOperationException($"Error adding ICE candidate for {connectionId}");

            }
        }

        public async Task HandleSdpAsync(string connectionId, string sdp, string type)
        {
            if (pc == null || !pc.Initialized)
            {
                await GetOrCreatePeer();
            }
            try
            {
                Log.Debug($"Handle sdp: {sdp}");
                Log.Information("Handling SDP of {Type} for {ConnectionId}", type, connectionId);
                if (string.IsNullOrWhiteSpace(sdp))
                {
                    Log.Error("Received empty SDP for {ConnectionId}", connectionId);
                    return;
                }

                if (type == "offer")
                {
                    await pc.SetRemoteDescriptionAsync(new SdpMessage
                    {
                        Type = SdpMessageType.Offer,
                        Content = sdp
                    });
                    pc.CreateAnswer();
                }
                else
                {
                    await pc.SetRemoteDescriptionAsync(new SdpMessage
                    {
                        Type = SdpMessageType.Answer,
                        Content = sdp
                    });
                    Log.Information("Creating answer for {ConnectionId}", connectionId);
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
                // Validate coordinates
                if (!action.X.HasValue || !action.Y.HasValue)
                {
                    Log.Error("Mouse action coordinates are null");
                    return;
                }

                // Ensure coordinates are within valid range (0–100%)
                float xPercent = Clamp(action.X.Value, 0, 100);
                float yPercent = Clamp(action.Y.Value, 0, 100);

                // Get screen dimensions using Windows Forms
                var screen = Screen.PrimaryScreen;
                int screenWidth = screen.Bounds.Width;
                int screenHeight = screen.Bounds.Height;

                // Convert percentage coordinates to absolute screen coordinates
                int absoluteX = (int)((xPercent / 100.0) * screenWidth);
                int absoluteY = (int)((yPercent / 100.0) * screenHeight);

                // Convert screen coordinates to client coordinates
                if (_streamingWindow != null)
                {
                    var handle = new System.Windows.Interop.WindowInteropHelper(_streamingWindow).Handle;
                    POINT clientPoint = new POINT { X = absoluteX, Y = absoluteY };
                    ScreenToClient(handle, ref clientPoint);
                    absoluteX = clientPoint.X;
                    absoluteY = clientPoint.Y;
                }

                // For mouse movement, use SetCursorPos directly as it's more efficient
                if (action.Action?.ToLower() == "mousemove")
                {
                    SetCursorPos(absoluteX, absoluteY);
                    return;
                }

                // For clicks and other actions
                switch (action.Action?.ToLower())
                {
                    case "mousedown":
                        SetCursorPos(absoluteX, absoluteY);
                        switch (action.Button?.ToLower())
                        {
                            case "left":
                                mouse_event(MOUSEEVENTF_LEFTDOWN, 0, 0, 0, 0);
                                break;
                            case "right":
                                mouse_event(MOUSEEVENTF_RIGHTDOWN, 0, 0, 0, 0);
                                break;
                            case "middle":
                                mouse_event(MOUSEEVENTF_MIDDLEDOWN, 0, 0, 0, 0);
                                break;
                            default:
                                Log.Error($"Unsupported mouse button: {action.Button}");
                                return;
                        }
                        break;

                    case "mouseup":
                        SetCursorPos(absoluteX, absoluteY);
                        switch (action.Button?.ToLower())
                        {
                            case "left":
                                mouse_event(MOUSEEVENTF_LEFTUP, 0, 0, 0, 0);
                                break;
                            case "right":
                                mouse_event(MOUSEEVENTF_RIGHTUP, 0, 0, 0, 0);
                                break;
                            case "middle":
                                mouse_event(MOUSEEVENTF_MIDDLEUP, 0, 0, 0, 0);
                                break;
                            default:
                                Log.Error($"Unsupported mouse button: {action.Button}");
                                return;
                        }
                        break;

                    case "click":
                        SetCursorPos(absoluteX, absoluteY);
                        switch (action.Button?.ToLower())
                        {
                            case "left":
                                mouse_event(MOUSEEVENTF_LEFTDOWN | MOUSEEVENTF_LEFTUP, 0, 0, 0, 0);
                                break;
                            case "right":
                                mouse_event(MOUSEEVENTF_RIGHTDOWN | MOUSEEVENTF_RIGHTUP, 0, 0, 0, 0);
                                break;
                            case "middle":
                                mouse_event(MOUSEEVENTF_MIDDLEDOWN | MOUSEEVENTF_MIDDLEUP, 0, 0, 0, 0);
                                break;
                            default:
                                Log.Error($"Unsupported mouse button: {action.Button}");
                                return;
                        }
                        break;

                    case "doubleclick":
                        SetCursorPos(absoluteX, absoluteY);
                        switch (action.Button?.ToLower())
                        {
                            case "left":
                                mouse_event(MOUSEEVENTF_LEFTDOWN | MOUSEEVENTF_LEFTUP, 0, 0, 0, 0);
                                mouse_event(MOUSEEVENTF_LEFTDOWN | MOUSEEVENTF_LEFTUP, 0, 0, 0, 0);
                                break;
                            case "right":
                                mouse_event(MOUSEEVENTF_RIGHTDOWN | MOUSEEVENTF_RIGHTUP, 0, 0, 0, 0);
                                mouse_event(MOUSEEVENTF_RIGHTDOWN | MOUSEEVENTF_RIGHTUP, 0, 0, 0, 0);
                                break;
                            case "middle":
                                mouse_event(MOUSEEVENTF_MIDDLEDOWN | MOUSEEVENTF_MIDDLEUP, 0, 0, 0, 0);
                                mouse_event(MOUSEEVENTF_MIDDLEDOWN | MOUSEEVENTF_MIDDLEUP, 0, 0, 0, 0);
                                break;
                            default:
                                Log.Error($"Unsupported mouse button: {action.Button}");
                                return;
                        }
                        break;

                    case "wheel":
                        // Handle mouse wheel events with improved sensitivity
                        if (action.Y.HasValue)
                        {
                            // Adjust wheel sensitivity (120 is the standard Windows wheel delta)
                            int wheelDelta = (int)(action.Y.Value * 40); // Reduced multiplier for better control
                            mouse_event(0x0800, 0, 0, (uint)wheelDelta, 0); // MOUSEEVENTF_WHEEL
                        }
                        break;

                    default:
                        Log.Error($"Unsupported mouse action: {action.Action}");
                        return;
                }
            }
            catch (Exception ex)
            {
                Log.Error($"Failed to execute mouse action: {ex.Message}");
            }
        }

        private float Clamp(float value, float min, float max)
        {
            if (value < min) return min;
            if (value > max) return max;
            return value;
        }

        private void ExecuteKeyboardAction(InputAction action)
        {
            try
            {
                if (string.IsNullOrEmpty(action.Key))
                {
                    Log.Error("Keyboard key is null or empty");
                    return;
                }

                // Convert string key to virtual key code
                Keys key;
                if (!Enum.TryParse(action.Key, true, out key))
                {
                    Log.Error($"Invalid key: {action.Key}");
                    return;
                }

                // Handle modifier keys
                if (action.Modifiers != null)
                {
                    foreach (var modifier in action.Modifiers)
                    {
                        if (Enum.TryParse(modifier, true, out Keys modifierKey))
                        {
                            // Press modifier key
                            keybd_event((byte)modifierKey, 0, 0, 0);
                        }
                    }
                }

                // Execute the main key action
                switch (action.Action?.ToLower())
                {
                    case "keydown":
                        keybd_event((byte)key, 0, 0, 0);
                        break;

                    case "keyup":
                        keybd_event((byte)key, 0, KEYEVENTF_KEYUP, 0);
                        break;

                    case "keypress":
                        keybd_event((byte)key, 0, 0, 0);
                        keybd_event((byte)key, 0, KEYEVENTF_KEYUP, 0);
                        break;

                    default:
                        Log.Error($"Unsupported keyboard action: {action.Action}");
                        break;
                }

                // Release modifier keys in reverse order
                if (action.Modifiers != null)
                {
                    var reversedModifiers = action.Modifiers.Reverse();
                    foreach (var modifier in reversedModifiers)
                    {
                        if (Enum.TryParse(modifier, true, out Keys modifierKey))
                        {
                            keybd_event((byte)modifierKey, 0, KEYEVENTF_KEYUP, 0);
                        }
                    }
                }
            }
            catch (Exception ex)
            {
                Log.Error($"Failed to execute keyboard action: {ex.Message}");
            }
        }

        private void ExecuteInputAction(InputAction action)
        {
            if (action == null || string.IsNullOrEmpty(action.Type))
            {
                Log.Error("Invalid input action");
                return;
            }

            switch (action.Type.ToLower())
            {
                case "mouse":
                    ExecuteMouseAction(action);
                    break;
                case "keyboard":
                    ExecuteKeyboardAction(action);
                    break;
                default:
                    Log.Error($"Unsupported input type: {action.Type}");
                    break;
            }
        }

        [DllImport("user32.dll")]
        private static extern bool ScreenToClient(IntPtr hWnd, ref POINT lpPoint);

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