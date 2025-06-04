using Microsoft.AspNetCore.SignalR.Client;
using System;
using System.ComponentModel;
using System.Runtime.CompilerServices;
using System.Threading.Tasks;
using WindowsInput;
using Client.Models;
using Microsoft.MixedReality.WebRTC;
namespace Client.ViewModels
{
    public class SignalRConnectionViewModel: INotifyPropertyChanged
    {
        private HubConnection _connection;
        private string _connectionId;
        private bool _isConnected;
        private string _connectionStatus;
        private readonly string _hubUrl;
        private readonly string _token;

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

        public SignalRConnectionViewModel(string hubUrl, string token)
        {
            _hubUrl = hubUrl;
            _token = token;
            ConnectionStatus = "Disconnected";
        }

        public async Task ConnectToHubAsync(string sessionId)
        {
            if (_connection != null && _connection.State == HubConnectionState.Connected)
                return;

            _connection = new HubConnectionBuilder()
                .WithUrl($"{_hubUrl}?sessionId={sessionId}", options =>
                {
                    options.AccessTokenProvider = () => Task.FromResult(_token);
                    options.Transports = Microsoft.AspNetCore.Http.Connections.HttpTransportType.WebSockets;
                })
                .WithAutomaticReconnect()
                .Build();

            RegisterEventHandlers();

            try
            {
                ConnectionStatus = "Connecting...";
                await _connection.StartAsync();
                IsConnected = true;
                ConnectionStatus = "Connected";
                ConnectionId = _connection.ConnectionId;

                // Optionally send a test message
                var testAction = new
                {
                    type = "test",
                    action = "test",
                    message = "Hello from C# client"
                };

                await _connection.InvokeAsync("SendInputAction", sessionId, System.Text.Json.JsonSerializer.Serialize(testAction));
            }
            catch (Exception ex)
            {
                IsConnected = false;
                ConnectionStatus = $"Connection failed: {ex.Message}";
            }
        }

        private void RegisterEventHandlers()
        {
            _connection.On<string>("ConnectionEstablished", (connectionId) =>
            {
                ConnectionId = connectionId;
                IsConnected = true;
                ConnectionStatus = "Connected";
            });

            _connection.On<string>("Error", (message) =>
            {
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
                    Console.WriteLine($"[DEBUG] Received input: {serializedAction}");
                    var action = System.Text.Json.JsonSerializer.Deserialize<InputAction>(serializedAction);

                    if (action == null || string.IsNullOrWhiteSpace(action.Type) || string.IsNullOrWhiteSpace(action.Action))
                        throw new Exception("Invalid input action format");

                    await Task.Delay(100); // Simulate delay

                    if (action.Type == "keyboard")
                    {
                        Console.WriteLine($"[DEBUG] Simulate keyboard {action.Action} {action.Key}");
                        var sim = new InputSimulator();
                        sim.Keyboard.TextEntry(action.Key);
                    }
                    else if (action.Type == "mouse")
                    {
                        var sim = new InputSimulator();
                        if (action.Action == "move")
                        {
                            sim.Mouse.MoveMouseToPositionOnVirtualDesktop(action.X, action.Y);
                        }
                        else if (action.Action == "click")
                        {
                            switch (action.Button?.ToLower())
                            {
                                case "left":
                                    sim.Mouse.LeftButtonClick();
                                    break;
                                case "right":
                                    sim.Mouse.RightButtonClick();
                                    break;
                                case "middle":
                                    sim.Mouse.XButtonClick(2); // Simulate middle as XButton2
                                    break;
                            }
                        }
                    }
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
                Console.WriteLine($"[DEBUG] Received screen data: length={imageBase64.Length}");
                // Xử lý nếu cần decode ảnh base64 ở đây
            });

            _connection.On<string>("TestMessage", (msg) =>
            {
                Console.WriteLine($"[DEBUG] Test message received: {msg}");
            });

            _connection.On<string>("Echo", (msg) =>
            {
                Console.WriteLine($"[DEBUG] Echo received: {msg}");
            });

            _connection.Reconnecting += error =>
            {
                IsConnected = false;
                ConnectionStatus = "Reconnecting...";
                return Task.CompletedTask;
            };

            _connection.Reconnected += connectionId =>
            {
                IsConnected = true;
                ConnectionStatus = "Reconnected";
                ConnectionId = connectionId;
                return Task.CompletedTask;
            };

            _connection.Closed += error =>
            {
                IsConnected = false;
                ConnectionStatus = "Disconnected";
                return Task.CompletedTask;
            };
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
