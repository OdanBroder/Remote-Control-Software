using System;
using System.ComponentModel;
using System.Threading.Tasks;
using System.Windows.Input;
using Client.Models;
using Client.Helpers;
using Client.Services;
using Newtonsoft.Json.Linq;
using Serilog;
using System.Windows;
using System.Windows.Media.Animation;
using System.Text;
using System.Diagnostics;
using CommunityToolkit.Mvvm.Input;

namespace Client.ViewModels
{
    public class ConnectViewModel : INotifyPropertyChanged
    {
        private readonly SessionService _sessionService;
        private readonly SignalRService _signalRService;
        private string _session;
        private string _joinSessionId;
        private string _errorMessage;
        public string Session
        {
            get => _session;
            set
            {
                if (_session != value)
                {
                    _session = value;
                    OnPropertyChanged(nameof(Session));
                }
            }
        }

        public string JoinSessionId
        {
            get => _joinSessionId;
            set { _joinSessionId = value; OnPropertyChanged(); }
        }

        public string ErrorMessage
        {
            get => _errorMessage;
            set { _errorMessage = value; OnPropertyChanged(); }
        }

        public string ConnectionStatus
        {
            get => _sessionService.ConnectionStatus;
        }

        public string ConnectionId
        {
            get => _sessionService.ConnectionId;
        }

        public DateTime? ConnectedSince
        {
            get => _sessionService.ConnectedSince;
        }

        public ICommand ReconnectCommand { get; }
        public ICommand JoinSessionCommand { get; }
        public ICommand CopySessionCommand { get; }
        public ICommand ConnectCommand { get; }
        public ICommand DisconnectCommand { get; }
        public ICommand StartInputCommand { get; }
        public ICommand StopInputCommand { get; }
        public ICommand StartStreamingCommand { get; }
        public ICommand StopStreamingCommand { get; }
        public ICommand AcceptStreamingCommand { get; }

        private readonly SendInputServices _sendInput;
        private InputMonitor _inputMonitor;
        private SendWebRTCSignal _streamScreen;

        public ConnectViewModel(SessionService sessionService, SignalRService signalRService)
        {
            ErrorMessage = string.Empty;
            _sessionService = sessionService;
            _signalRService = signalRService;
            _sendInput = new SendInputServices();
            _streamScreen = new SendWebRTCSignal(_signalRService);

            ReconnectCommand = new AsyncRelayCommand(async _ => await ExecuteStartSession());
            JoinSessionCommand = new AsyncRelayCommand(async _ => await ExecuteJoinSessionAsync());
            CopySessionCommand = new AsyncRelayCommand(async _ => await ExecuteCopySessionAsync());
            ConnectCommand = new AsyncRelayCommand(async _ => await ExecuteConnectSessionAsync());
            DisconnectCommand = new AsyncRelayCommand(async _ => await ExecuteDisconnectSessionAsync());

            StartInputCommand = new AsyncRelayCommand(async _ => await ExecuteStartInput());
            StopInputCommand = new AsyncRelayCommand(async _ => await ExecuteStopInput());
            StartStreamingCommand = new AsyncRelayCommand(async _ => await ExecuteStartStreaming());
            StopStreamingCommand = new AsyncRelayCommand(async _ => await ExecuteStopStreaming());
            AcceptStreamingCommand = new AsyncRelayCommand(async _ => await ExecuteAcceptStreaming());

            _ = ExecuteStartSession();
        }

        private async Task ExecuteStartSession()
        {
            ErrorMessage = string.Empty;
            try
            {
                var response = await _sessionService.GetActiveSessionAsync();

                if (response.Code == "SESSIONS_FOUND")
                {
                    foreach (var session in response.Data)
                    {
                        await _sessionService.LeaveSessionAsync(session.SessionId);
                    }
                }
                var sessionResponse = await _sessionService.StartSessionAsync();
                Session = SessionStorage.LoadSession();
            }
            catch (Exception ex)
            {
                Console.WriteLine(ex.Message, "Failed to reconnect session");
            }
        }
        private async Task ExecuteConnectSessionAsync()
        {
            ErrorMessage = string.Empty;
            try
            {
                string sessionId = SessionStorage.LoadSession();
                string connectId = ConnectionStorage.LoadConnectionId();
                Log.Information($"Connection Id {connectId}");
                await _sessionService.ConnectToSessionAsync(sessionId, connectId);
            }
            catch (Exception ex)
            {
                Console.WriteLine(ex.Message, "Failed to reconnect session");
            }
        }

        private async Task ExecuteDisconnectSessionAsync()
        {
            ErrorMessage = string.Empty;
            try
            {
                var response = await _sessionService.GetActiveSessionAsync();
                if (response.Code == "SESSIONS_FOUND")
                {
                    string sessionId = SessionStorage.LoadSession();
                    string connectId = ConnectionStorage.LoadConnectionId();
                    Log.Information($"Connection Id {connectId}");
                    await _sessionService.DisconnectToSessionAsync(sessionId, connectId);
                }
                else
                {
                    ErrorMessage = "Connection Id not found please join first!";
                    Log.Information($"Connection Id not found please join first!");
                }
            }
            catch (Exception ex)
            {
                Console.WriteLine(ex.Message, "Failed to reconnect session");
            }
        }
        private async Task ExecuteJoinSessionAsync()
        {
            ErrorMessage = string.Empty;

            try
            {
                if (string.IsNullOrWhiteSpace(JoinSessionId))
                {
                    await Application.Current.Dispatcher.InvokeAsync(() =>
                    {
                        MessageBox.Show("Please enter a session ID.", "Invalid Input", MessageBoxButton.OK, MessageBoxImage.Warning);
                    });
                    return;
                }
                JoinSessionId = JoinSessionId.Trim();
                var response = await _sessionService.GetActiveSessionAsync();
                if (response.Code == "SESSIONS_FOUND")
                {
                    if (response.Data[0].SessionId == JoinSessionId)
                    {
                        ErrorMessage = "You've already join this SessionId";
                        return;
                    }
                    else if (response.Data[0].SessionId != JoinSessionId)
                    {
                        foreach (var session in response.Data)
                        {
                            await _sessionService.LeaveSessionAsync(session.SessionId);
                        }
                    }
                }
                else if (response.Code != "NO_SESSIONS")
                {
                    ErrorMessage = response.Message;
                    return;
                }

            }
            catch (Exception ex)
            {
                Console.WriteLine(ex.Message, "No active session found");
            }

            try
            {
                await _sessionService.JoinSessionAsync(JoinSessionId);
                Session = JoinSessionId;
                Log.Information($"SessionsId: {Session}");
                await ExecuteConnectSessionAsync();
            }
            catch (Exception ex)
            {
                ErrorMessage = ex.Message;
            }
        }

        private async Task ExecuteCopySessionAsync()
        {
            try
            {
                await Task.Run(() =>
                {
                    Application.Current.Dispatcher.Invoke(() =>
                    {
                        System.Windows.Clipboard.SetText(Session);
                    });
                });

            }
            catch (Exception ex)
            {
                Debug.WriteLine($"Copy failed: {ex.Message}");
            }
        }

        private async Task ExecuteStartInput()
        {
            try
            {
                Console.WriteLine("Input monitoring started...");
                _inputMonitor = new InputMonitor(_sendInput);
                _inputMonitor.Start();
            }
            catch (Exception ex)
            {
                _inputMonitor?.Dispose();
                Console.WriteLine($"SendInput failed: {ex.Message}");
            }
        }

        private async Task ExecuteStopInput()
        {
            try
            {
                Console.WriteLine("Input monitoring stopping...");
                _inputMonitor?.Stop();
            }
            catch (Exception ex)
            {
                Console.WriteLine($"SendInput failed: {ex.Message}");
            }
        }

        private async Task ExecuteStartStreaming()
        {
            try
            {
                SessionResponse sessionResponse = await _sessionService.GetActiveSessionAsync();
                await _signalRService.ConnectToHubAsync(sessionResponse.Data[0].SessionId);
                await WaitForSignalRConnection();
                var webRTCSignal = new SendWebRTCSignal(_signalRService);
                var response = await webRTCSignal.StartStreaming(isStreamer: true);
            }
            catch (Exception ex)
            {
                _streamScreen.Dispose();
                await _signalRService.DisconnectToHubAsync();
                Console.WriteLine($"Error when streaming: {ex.Message}");
            }
        }

        private async Task ExecuteStopStreaming()
        {
            try
            {
                _streamScreen.Dispose();
                await _signalRService.DisconnectToHubAsync();
            }
            catch (Exception ex)
            {
                Console.WriteLine($"Error when streaming: {ex.Message}");
            }
        }

        private async Task ExecuteAcceptStreaming()
        {
            try
            {
                SessionResponse sessionResponse = await _sessionService.GetActiveSessionAsync();
                await _signalRService.ConnectToHubAsync(sessionResponse.Data[0].SessionId);
                await WaitForSignalRConnection();
                var webRTCSignal = new SendWebRTCSignal(_signalRService);
                var videoProcessor = new VideoProcessor();
                var response = await webRTCSignal.StartStreaming(isStreamer: false);
            }
            catch (Exception ex)
            {
                Console.WriteLine($"Error when accept streaming: {ex.Message}");
            }
        }

        private async Task WaitForSignalRConnection()
        {
            int retries = 0;
            while (!_signalRService.IsConnected && retries++ < 10)
            {
                await Task.Delay(500);
            }

            if (!_signalRService.IsConnected)
            {
                throw new Exception("Failed to establish SignalR connection");
            }
        }

        public event PropertyChangedEventHandler PropertyChanged;
        protected void OnPropertyChanged(string propertyName = null)
        {
            PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(propertyName));
        }
    }
}
