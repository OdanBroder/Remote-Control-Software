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

namespace Client.Services
{
    public class SignalRService
    {
        private HubConnection _connection;
        private string _connectionId;
        private bool _isConnected;
        private string _connectionStatus;
        private readonly string _token;
        private PeerConnection _peerConnection;
        private WebRTCService _webrtcClient;
        private readonly string _hubUrl = AppSettings.BaseApiUri + "/remotecontrolhub";
        private bool _connectionEstablished = false;

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

        public event PropertyChangedEventHandler PropertyChanged;
        private void OnPropertyChanged([CallerMemberName] string name = null) =>
            PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(name));

        public SignalRService()
        {
            _token = TokenStorage.LoadToken();
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
                        options.Transports = Microsoft.AspNetCore.Http.Connections.HttpTransportType.WebSockets;
                        options.SkipNegotiation = false;
                        options.Headers = new Dictionary<string, string>
                        {
                            { "Authorization", $"Bearer {_token}" }
                        };
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
                        Console.WriteLine("[DEBUG] Connection is fully established and synchronized");
                        break;
                    }

                    Console.WriteLine($"[DEBUG] Waiting for connection to be fully established... Attempt {retryCount + 1}/{maxRetries}");
                    Console.WriteLine($"[DEBUG] Current state: {_connection.State}, Connection established: {_connectionEstablished}");
                    await Task.Delay(checkInterval);
                    retryCount++;
                }

                if (!_connectionEstablished || _connection.State != HubConnectionState.Connected)
                {
                    throw new Exception($"Failed to establish connection after {maxRetries} attempts. Current state: {_connection.State}, Connection established: {_connectionEstablished}");
                }

                // Send a test message to verify connection
                var testAction = new
                {
                    type = "test",
                    action = "test",
                    message = "Testing connection..."
                };

                Console.WriteLine("[DEBUG] Sending test message...");
                await _connection.InvokeAsync("SendInputAction", sessionId, JsonConvert.SerializeObject(testAction));
                Console.WriteLine("[DEBUG] Test message sent successfully");

                IsConnected = true;
                ConnectionStatus = "Connected";
                ConnectionId = _connection.ConnectionId;
            }
            catch (Exception ex)
            {
                IsConnected = false;
                ConnectionStatus = $"Connection failed: {ex.Message}";
                Console.WriteLine($"[ERROR] Hub connection error: {ex}");
                throw;
            }
        }

        private void RegisterEventHandlers()
        {
            _connection.On<string>("ConnectionEstablished", (connectionId) =>
            {
                Console.WriteLine($"[DEBUG] Connection established with ID: {connectionId}");
                _connectionEstablished = true;
                ConnectionId = connectionId;
                IsConnected = true;
                ConnectionStatus = "Connected";
            });

            _connection.On<string>("Error", (message) =>
            {
                Console.WriteLine($"[ERROR] Received error from server: {message}");
                ConnectionStatus = $"Error: {message}";
            });

            _connection.On<string>("PeerConnected", (peerId) =>
            {
                Console.WriteLine($"[DEBUG] Peer connected: {peerId}");
            });

            _connection.On<string>("PeerDisconnected", (peerId) =>
            {
                Console.WriteLine($"[DEBUG] Peer disconnected: {peerId}");
            });

            _connection.On<string>("ReceiveInput", async (serializedAction) =>
            {
                try
                {
                    Console.WriteLine($"[DEBUG] Received input action: {serializedAction}");
                    var action = JsonConvert.DeserializeObject<InputAction>(serializedAction);

                    if (action == null || string.IsNullOrWhiteSpace(action.Type) || string.IsNullOrWhiteSpace(action.Action))
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
                Console.WriteLine($"[DEBUG] Received screen update:\n" +
                    $"Data length: {imageBase64.Length}\n" +
                    $"First 100 chars: {imageBase64.Substring(0, Math.Min(100, imageBase64.Length))}...");
            });

            _connection.On<string>("TestMessage", (message) =>
            {
                Console.WriteLine($"[DEBUG] Received test message:\n" +
                    $"Message: {message}\n" +
                    $"Type: {message.GetType()}\n" +
                    $"Timestamp: {DateTime.UtcNow:O}");
            });

            _connection.On<string>("Echo", (message) =>
            {
                Console.WriteLine($"[DEBUG] Received echo:\n" +
                    $"Message: {message}\n" +
                    $"Type: {message.GetType()}\n" +
                    $"Timestamp: {DateTime.UtcNow:O}");
            });

            _connection.Reconnecting += error =>
            {
                Console.WriteLine($"[DEBUG] Connection lost. Reconnecting...\n" +
                    $"Error: {error}\n" +
                    $"State: {_connection.State}\n" +
                    $"Timestamp: {DateTime.UtcNow:O}");
                IsConnected = false;
                ConnectionStatus = "Reconnecting...";
                return Task.CompletedTask;
            };

            _connection.Reconnected += connectionId =>
            {
                Console.WriteLine($"[DEBUG] Reconnected successfully:\n" +
                    $"Connection ID: {connectionId}\n" +
                    $"State: {_connection.State}\n" +
                    $"Timestamp: {DateTime.UtcNow:O}");
                IsConnected = true;
                ConnectionStatus = "Reconnected";
                ConnectionId = connectionId;
                return Task.CompletedTask;
            };

            _connection.Closed += error =>
            {
                Console.WriteLine($"[DEBUG] Connection closed:\n" +
                    $"Error: {error}\n" +
                    $"State: {_connection.State}\n" +
                    $"Timestamp: {DateTime.UtcNow:O}");
                IsConnected = false;
                ConnectionStatus = "Disconnected";
                return Task.CompletedTask;
            };

            // WebRTC signal handlers
            _connection.On<object>("ReceiveWebRTCSignal", async (signal) =>
            {
                try
                {
                    var signalData = JsonConvert.DeserializeObject<WebRTCSignal>(JsonConvert.SerializeObject(signal));
                    Console.WriteLine($"Received WebRTC {signalData.SignalType} signal from {signalData.ConnectionId}");

                    // Handle WebRTC signaling
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
                    Console.WriteLine($"[ERROR] Error processing WebRTC signal: {ex.Message}");
                }
            });
        }

        public async Task HandleIceCandidateAsync(string connectionId, string candidate, string sdpMid, int sdpMlineIndex)
        {
            var pc = await GetOrCreatePeerAsync(connectionId);
            if (pc == null)
            {
                Console.WriteLine($"[{connectionId}] Failed to get or create PeerConnection.");
                return;
            }

            try
            {
                Console.WriteLine($"[{connectionId}] Adding ICE candidate...");
                pc.AddIceCandidate(new IceCandidate
                {
                    Content = candidate,
                    SdpMid = sdpMid,
                    SdpMlineIndex = sdpMlineIndex
                });

                Console.WriteLine($"[{connectionId}] ICE candidate added successfully.");
            }
            catch (Exception ex)
            {
                Console.WriteLine(ex.Message, $"[{connectionId}] Error adding ICE candidate.");
            }

            await Task.CompletedTask;
        }
        public async Task HandleSdpAsync(string connectionId, string sdp, string type)
        {
            var pc = await GetOrCreatePeerAsync(connectionId);
            if (pc == null)
            {
                Console.WriteLine($"[{connectionId}] Failed to get or create PeerConnection.");
                return;
            }

            Console.WriteLine($"[{connectionId}] Received SDP of type: {type}");

            try
            {
                var sdpMessage = new SdpMessage
                {
                    Type = type.ToLower() == "offer" ? SdpMessageType.Offer : SdpMessageType.Answer,
                    Content = sdp
                };

                await pc.SetRemoteDescriptionAsync(sdpMessage);

                if (type.ToLower() == "offer")
                {
                    Console.WriteLine($"[{connectionId}] Creating and sending SDP answer...");
                    pc.CreateAnswer();
                }

                Console.WriteLine($"[{connectionId}] Handled SDP successfully.");
            }
            catch (Exception ex)
            {
                Console.WriteLine(ex.Message, $"[{connectionId}] Error handling SDP message.");
            }
        }

        private async Task<PeerConnection> GetOrCreatePeerAsync(string connectionId)
        {
            if (_peerConnection != null)
            {
                return _peerConnection;
            }

            _peerConnection = new PeerConnection();
            var config = new PeerConnectionConfiguration
            {
                IceServers = new List<IceServer>
                {
                    new IceServer { Urls = { "stun:stun.l.google.com:19302" } }
                }
            };
            
            try
            {
                await _peerConnection.InitializeAsync(config);

                _peerConnection.LocalSdpReadytoSend += async (SdpMessage msg) =>
                {
                    await _connection.InvokeAsync("SendWebRTCState", JsonConvert.SerializeObject(new
                    {
                        Type = msg.Type.ToString().ToLower(),
                        Sdp = msg.Content,
                        FromConnectionId = connectionId
                    }));
                };

                _peerConnection.IceCandidateReadytoSend += async (IceCandidate candidate) =>
                {
                    await _connection.InvokeAsync("SendWebRTCState", JsonConvert.SerializeObject(new
                    {
                        Type = "ice-candidate",
                        Candidate = candidate.Content,
                        SdpMid = candidate.SdpMid,
                        SdpMLineIndex = candidate.SdpMlineIndex,
                        FromConnectionId = connectionId
                    }));
                };

                _peerConnection.VideoTrackAdded += track =>
                {
                    Console.WriteLine($"Video track added: {track.Name}");
                };

                return _peerConnection;
            }
            catch (Exception ex)
            {
                Console.WriteLine(ex.Message, "Error initializing PeerConnection");
                _peerConnection = null;
                return null;
            }
        }
     
        public async Task DisconnectAsync() 
        {
            if (_connection != null)
            {
                await _connection.StopAsync();
                await _connection.DisposeAsync();
                IsConnected = false;
                ConnectionStatus = "Disconnected";
            }
        }
    }
}