using System;
using System.ComponentModel;
using System.Threading.Tasks;
using System.Windows.Input;
using Client.Models;
using Client.Helpers;
using Client.Services;

namespace Client.ViewModels
{
    public class ConnectViewModel : INotifyPropertyChanged
    {
        #region Session Management Properties and Methods
        private readonly SessionService _sessionService = new SessionService();
        private string _session;
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
        public ICommand ReconnectCommand { get; }
        public ConnectViewModel()
        {
            ReconnectCommand = new AsyncRelayCommand(async _ => await ExecuteReconnectAsync());
            JoinSessionCommand = new AsyncRelayCommand(async _ => await ExecuteJoinSessionAsync(), _ => !IsJoining);
            _authService = new AuthService();
            _signalRService = new SignalRService();
            SubscribeToSignalREvents();
            _ = ExecuteStartSession();
        }
        private async Task ExecuteReconnectAsync()
        {
            try
            {
                var response = await _sessionService.GetActiveSessionAsync();

                if (response?.Data != null)
                {
                    foreach (var session in response.Data)
                    {
                        await _sessionService.LeaveSessionAsync(session.SessionId);
                    }
                }

                await ExecuteStartSession();
            }
            catch (Exception ex)
            {
                // Optional: handle/log exception
                Console.WriteLine(ex.Message, "Failed to reconnect session");
            }
        }

        public async Task ExecuteStartSession()
        {
            try
            {
                var response = await _sessionService.GetActiveSessionAsync();

                if (response.Code == "SESSIONS_FOUND")
                {
                    string sessionId = response.Data[0].SessionId;
                    await _sessionService.LeaveSessionAsync(JoinSessionId);
                }

                var startResponse = await _sessionService.StartSessionAsync();
                if(startResponse.Code == "SESSION_EXISTS")
                {
                    Session = SessionStorage.LoadSession();
                }
                else if (startResponse.Success)
                {
                    Session = startResponse.Data.SessionId;
                }
                else
                {
                    Console.WriteLine($"Error response session {startResponse.Message}");
                }
            }
            catch (Exception ex)
            {
                Console.WriteLine($"Error starting session: {ex.Message}");
            }
        }
        #endregion

        #region Join Session Properties and Methods
        // JoinSessionViewModel logic
        private readonly AuthService _authService;
        private readonly SignalRService _signalRService;
        private string _sessionId;
        private string _joinSessionId;
        private string _errorMessage;
        private bool _isJoining;
        private string _connectionStatus;
        private string _connectionId;
        private DateTime? _connectedSince;
        private const int MaxConnectionRetries = 5;
        private const int ConnectionCheckIntervalMs = 1000;

        
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
        public bool IsJoining
        {
            get => _isJoining;
            set { _isJoining = value; OnPropertyChanged(); }
        }
        public string ConnectionStatus
        {
            get => _connectionStatus;
            set { _connectionStatus = value; OnPropertyChanged(); }
        }
        public string ConnectionId
        {
            get => _connectionId;
            set { _connectionId = value; OnPropertyChanged(); }
        }
        public DateTime? ConnectedSince
        {
            get => _connectedSince;
            set { _connectedSince = value; OnPropertyChanged(); }
        }
        // Command to join a session
        public ICommand JoinSessionCommand { get; }

        private void SubscribeToSignalREvents()
        {
            _signalRService.PropertyChanged += (s, e) =>
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
        }

        private async Task ExecuteJoinSessionAsync()
        {
            IsJoining = true;
            ErrorMessage = string.Empty;

            try
            {
                await ValidateAndJoinSessionAsync();
            }
            catch (Exception ex)
            {
                HandleJoinSessionError(ex);
            }
            finally
            {
                IsJoining = false;
            }
        }

        private async Task ValidateAndJoinSessionAsync()
        {
            var token = TokenStorage.LoadToken();
            if (string.IsNullOrEmpty(token))
            {
                ErrorMessage = "Please login first.";
                return;
            }

            if (string.IsNullOrWhiteSpace(JoinSessionId))
            {
                ErrorMessage = "Please enter a session ID.";
                return;
            }
            JoinSessionId = JoinSessionId.Trim();
            Console.WriteLine("Join sessionId available...");
            var response = await _authService.JoinSessionAsync(JoinSessionId);
            if (!response.Success && response.Code != "SESSION_EXISTS")
            {
                ErrorMessage = response.Message ?? "Failed to join session.";
                return;
            }
            Console.WriteLine("Connect to signal signal async...");
            await ConnectToSignalRAsync();
        }

        private async Task ConnectToSignalRAsync()
        {
            await _signalRService.ConnectToHubAsync(JoinSessionId);

            // Wait for connection to be fully established
            int retryCount = 0;
            while (retryCount < MaxConnectionRetries)
            {
                if (_signalRService.IsConnected && !string.IsNullOrEmpty(_signalRService.ConnectionId))
                {
                    Console.WriteLine("[DEBUG] Connection is fully established and synchronized");
                    break;
                }

                Console.WriteLine($"[DEBUG] Waiting for connection to be fully established... Attempt {retryCount + 1}/{MaxConnectionRetries}");
                await Task.Delay(ConnectionCheckIntervalMs);
                retryCount++;
            }

            if (!_signalRService.IsConnected || string.IsNullOrEmpty(_signalRService.ConnectionId))
            {
                throw new Exception("Failed to establish SignalR connection");
            }

            // Store session ID for future use
            SessionStorage.SaveSession(JoinSessionId);
            Console.WriteLine($"[DEBUG] Successfully joined session: {JoinSessionId}");
        }

        private void HandleJoinSessionError(Exception ex)
        {
            ErrorMessage = $"Error joining session: {ex.Message}";
            Console.WriteLine($"[ERROR] Error joining session: {ex}");
        }
        #endregion

        public event PropertyChangedEventHandler PropertyChanged;
        protected void OnPropertyChanged(string propertyName = null)
        {
            PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(propertyName));
        }
    }
}
