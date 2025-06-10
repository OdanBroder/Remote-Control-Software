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
using Client.Views;
using System.Windows.Interop;
using System.Linq;

namespace Client.ViewModels
{
    public class ConnectViewModel : INotifyPropertyChanged, IDisposable
    {
        private readonly SessionService _sessionService;
        private readonly SignalRService _signalRService;
        private string _session;
        private string _joinSessionId;
        private string _errorMessage;
        private bool _isDisposed;
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
        public ICommand LeaveSessionCommand { get; }
        public ICommand CopySessionCommand { get; }
        public ICommand ConnectCommand { get; }
        public ICommand DisconnectCommand { get; }
        public ICommand StartInputCommand { get; }
        public ICommand StopInputCommand { get; }
        public ICommand StartStreamingCommand { get; }
        public ICommand StopStreamingCommand { get; }
        public ICommand AcceptStreamingCommand { get; }
        public ICommand ShowFileTransferCommand { get; }
        public ICommand LogoutCommand { get; }

        private readonly SendInputServices _sendInput;
        private InputMonitor _inputMonitor;


        public ConnectViewModel(SessionService sessionService, SignalRService signalRService)
        {
            ErrorMessage = string.Empty;
            _sessionService = sessionService;
            _signalRService = signalRService;
            _signalRService.OnStopInput += OnStoppedStreaming;
            _sendInput = new SendInputServices(_signalRService);

            ReconnectCommand = new AsyncRelayCommand(async _ => await ExecuteStartSession());
            LeaveSessionCommand = new AsyncRelayCommand(async _ => await ExecuteLeaveSessionAsync());
            JoinSessionCommand = new AsyncRelayCommand(async _ => await ExecuteJoinSessionAsync());
            CopySessionCommand = new AsyncRelayCommand(async _ => await ExecuteCopySessionAsync());
            ConnectCommand = new AsyncRelayCommand(async _ => await ExecuteConnectSessionAsync());
            DisconnectCommand = new AsyncRelayCommand(async _ => await ExecuteDisconnectSessionAsync());

            StartInputCommand = new AsyncRelayCommand(async _ => await ExecuteStartInput());
            StopInputCommand = new AsyncRelayCommand(async _ => await ExecuteStopInput());
            StartStreamingCommand = new AsyncRelayCommand(async _ => await ExecuteStartStreaming());
            StopStreamingCommand = new AsyncRelayCommand(async _ => await ExecuteStopStreaming());
            AcceptStreamingCommand = new AsyncRelayCommand(async _ => await ExecuteAcceptStreaming());
            LogoutCommand = new AsyncRelayCommand(async _ => await ExcuteLogout());
            ShowFileTransferCommand = new RelayCommand(OpenFileTransferWindow);
            _ = ExecuteStartSession();
        }

        private Task ExcuteLogout()
        {
            Application.Current.Dispatcher.Invoke(() =>
            {
                TokenStorage.ClearToken();

                var loginView = new LoginView();
                loginView.Show();

                foreach (Window window in Application.Current.Windows)
                {
                    if (window is MainView)
                    {
                        window.Close();
                        break;
                    }
                }
            });

            return Task.CompletedTask;
        }
        private void OpenFileTransferWindow()
        {
            var vm = new FileTransferViewModel();
            var win = new FileTransferView(vm);
            win.Owner = Application.Current.MainWindow;
            win.Show();
        }
        private void OnStoppedStreaming(bool isSender)
        {
            if (isSender && _inputMonitor != null)
            {
                _inputMonitor.Stop();
            }
            else if (!isSender && _inputMonitor != null)
            {
                _inputMonitor.DisableLocalInput();
            }
        }
        private async Task ExecuteStartSession()
        {
            ErrorMessage = string.Empty;
            Session = string.Empty;
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
                SessionStorage.SaveSession(Session);
                string connectId = ConnectionStorage.LoadConnectionId();
                Log.Information($"Connection Id {connectId}");
                await _sessionService.ConnectToSessionAsync(Session, connectId);
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
                    await ExecuteLeaveSessionAsync();
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

        private async Task ExecuteLeaveSessionAsync()
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
                    Session = null;
                    Log.Information("Successfully left all active sessions");
                }
                else if (response.Code == "NO_SESSIONS")
                {
                    Log.Information("No active sessions to leave");
                }
                else
                {
                    ErrorMessage = response.Message;
                }
            }
            catch (Exception ex)
            {
                ErrorMessage = $"Error leaving session: {ex.Message}";
                Log.Error(ex, "Failed to leave session");
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
                // Get the main window handle
                IntPtr windowHandle = new WindowInteropHelper(Application.Current.MainWindow).Handle;
                _inputMonitor = new InputMonitor(_sendInput, windowHandle);
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
                // Check if already streaming
                if (_signalRService.IsStreaming)
                {
                    ErrorMessage = "Streaming is already active";
                    return;
                }

                ErrorMessage = "Starting streaming...";

                // Get active session
                SessionResponse sessionResponse = await _sessionService.GetActiveSessionAsync();
                if (sessionResponse?.Data == null || !sessionResponse.Data.Any())
                {
                    ErrorMessage = "No active session found";
                    return;
                }

                // Connect to SignalR hub
                await _signalRService.ConnectToHubAsync(sessionResponse.Data[0].SessionId);
                await WaitForSignalRConnection();

                // Start streaming
                var response = await _signalRService.StartStreaming(isStreamer: true);
                if (!response.Success)
                {
                    ErrorMessage = response.Message;
                    await _signalRService.DisconnectToHubAsync();
                    return;
                }

                ErrorMessage = "Streaming started successfully";
            }
            catch (Exception ex)
            {
                ErrorMessage = $"Error starting streaming: {ex.Message}";
                Log.Error(ex, "Failed to start streaming");
                await _signalRService.DisconnectToHubAsync();
            }
        }

        private async Task ExecuteStopStreaming()
        {
            try
            {
                if (!_signalRService.IsStreaming)
                {
                    ErrorMessage = "No active streaming session";
                    return;
                }
                Log.Information("Stopping streaming...");
                ErrorMessage = "Stopping streaming...";

                // First stop input monitoring
                await ExecuteStopInput();
                await _signalRService.SignalStopStreaming();
                await _signalRService.StopStreaming(keepResources: false);
                ErrorMessage = "Streaming stopped successfully";
            }
            catch (Exception ex)
            {
                ErrorMessage = $"Error stopping streaming: {ex.Message}";
                Log.Error(ex, "Failed to stop streaming");

                // Force cleanup on error
                try
                {
                    _signalRService.StopStreaming(keepResources: false);
                }
                catch (Exception cleanupEx)
                {
                    Log.Error(cleanupEx, "Failed to cleanup streaming resources after error");
                }
            }
        }

        private async Task ExecuteAcceptStreaming()
        {
            try
            {
                // Check if already streaming
                if (_signalRService.IsStreaming)
                {
                    ErrorMessage = "Streaming is already active";
                    return;
                }

                ErrorMessage = "Waiting for sender...";

                // Get active session
                SessionResponse sessionResponse = await _sessionService.GetActiveSessionAsync();
                if (sessionResponse?.Data == null || !sessionResponse.Data.Any())
                {
                    ErrorMessage = "No active session found";
                    return;
                }

                // Connect to SignalR hub
                await _signalRService.ConnectToHubAsync(sessionResponse.Data[0].SessionId);
                await WaitForSignalRConnection();

                // Start streaming as viewer
                var response = await _signalRService.StartStreaming(isStreamer: false);
                if (!response.Success)
                {
                    ErrorMessage = response.Message;
                    await _signalRService.DisconnectToHubAsync();
                    return;
                }

                ErrorMessage = "Streaming accepted successfully";
            }
            catch (Exception ex)
            {
                ErrorMessage = $"Error accepting streaming: {ex.Message}";
                Log.Error(ex, "Failed to accept streaming");
                await _signalRService.DisconnectToHubAsync();
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
                    try
                    {
                        // Clean up input monitor
                        if (_inputMonitor != null)
                        {
                            _inputMonitor.Dispose();
                            _inputMonitor = null;
                        }

                        // Clean up SignalR service
                        if (_signalRService != null)
                        {
                            _signalRService.OnStopInput -= OnStoppedStreaming;
                            _signalRService.Dispose();
                        }

                        // Clean up send input service
                        if (_sendInput != null)
                        {
                            _sendInput.Dispose();
                        }
                    }
                    catch (Exception ex)
                    {
                        Log.Error(ex, "Error during ConnectViewModel disposal");
                    }
                }
                _isDisposed = true;
            }
        }

        ~ConnectViewModel()
        {
            Dispose(false);
        }
    }
}
