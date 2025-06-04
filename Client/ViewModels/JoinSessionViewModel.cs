using System;
using System.ComponentModel;
using System.Net.Http;
using System.Runtime.CompilerServices;
using System.Threading.Tasks;
using System.Windows.Input;
using Client.Helpers;
using Client.Models;
using Client.Services;

namespace Client.ViewModels
{
    /// <summary>
    /// ViewModel for handling session joining functionality
    /// </summary>
    public class JoinSessionViewModel : INotifyPropertyChanged
    {
        private readonly AuthService _authService;
        private readonly SignalRService _signalRService;
        private string _sessionId;
        private string _errorMessage;
        private bool _isJoining;
        private string _connectionStatus;
        private string _connectionId;
        private DateTime? _connectedSince;

        private const int MaxConnectionRetries = 5;
        private const int ConnectionCheckIntervalMs = 1000;

        /// <summary>
        /// Gets or sets the session identifier
        /// </summary>
        public string SessionId
        {
            get => _sessionId;
            set { _sessionId = value; OnPropertyChanged(); }
        }

        /// <summary>
        /// Gets or sets the error message to display
        /// </summary>
        public string ErrorMessage
        {
            get => _errorMessage;
            set { _errorMessage = value; OnPropertyChanged(); }
        }

        /// <summary>
        /// Gets or sets whether a join operation is in progress
        /// </summary>
        public bool IsJoining
        {
            get => _isJoining;
            set { _isJoining = value; OnPropertyChanged(); }
        }

        /// <summary>
        /// Gets or sets the current connection status
        /// </summary>
        public string ConnectionStatus
        {
            get => _connectionStatus;
            set { _connectionStatus = value; OnPropertyChanged(); }
        }

        /// <summary>
        /// Gets or sets the current connection identifier
        /// </summary>
        public string ConnectionId
        {
            get => _connectionId;
            set { _connectionId = value; OnPropertyChanged(); }
        }

        /// <summary>
        /// Gets or sets when the connection was established
        /// </summary>
        public DateTime? ConnectedSince
        {
            get => _connectedSince;
            set { _connectedSince = value; OnPropertyChanged(); }
        }

        // Command to join a session
        public ICommand JoinSessionCommand { get; } 

        /// <summary>
        /// Initializes a new instance of the JoinSessionViewModel class
        /// </summary>
        public JoinSessionViewModel()
        {
            _authService = new AuthService();
            _signalRService = new SignalRService();

            JoinSessionCommand = new AsyncRelayCommand(
                async _ => await ExecuteJoinSessionAsync(),
                _ => !IsJoining);

            SubscribeToSignalREvents();
        }

        /// <summary>
        /// Subscribes to SignalR service property change events
        /// </summary>
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

        /// <summary>
        /// Executes the join session operation
        /// </summary>
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

        /// <summary>
        /// Validates prerequisites and joins the session
        /// </summary>
        private async Task ValidateAndJoinSessionAsync()
        {
            var token = TokenStorage.LoadToken();
            if (string.IsNullOrEmpty(token))
            {
                ErrorMessage = "Please login first.";
                return;
            }

            if (string.IsNullOrWhiteSpace(SessionId))
            {
                ErrorMessage = "Please enter a session ID.";
                return;
            }

            var response = await _authService.JoinSessionAsync(SessionId);
            if (!response.Success)
            {
                ErrorMessage = response.Message ?? "Failed to join session.";
                return;
            }
            Console.WriteLine("[Debug] Connect to signalR Async...");
            await ConnectToSignalRAsync();
        }

        /// <summary>
        /// Establishes SignalR connection and waits for it to be ready
        /// </summary>
        private async Task ConnectToSignalRAsync()
        {
            await _signalRService.ConnectToHubAsync(SessionId);

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
            SessionStorage.SaveSession(SessionId);
            Console.WriteLine($"[DEBUG] Successfully joined session: {SessionId}");
        }

        /// <summary>
        /// Handles errors that occur during session joining
        /// </summary>
        private void HandleJoinSessionError(Exception ex)
        {
            ErrorMessage = $"Error joining session: {ex.Message}";
            Console.WriteLine($"[ERROR] Error joining session: {ex}");
        }

        /// <summary>
        /// Event that is raised when a property value changes
        /// </summary>
        public event PropertyChangedEventHandler PropertyChanged;

        /// <summary>
        /// Raises the PropertyChanged event
        /// </summary>
        /// <param name="propertyName">Name of the property that changed</param>
        private void OnPropertyChanged([CallerMemberName] string propertyName = null)
            => PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(propertyName));
    }
}
