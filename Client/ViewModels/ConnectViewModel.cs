using System;
using System.ComponentModel;
using System.Threading.Tasks;
using System.Windows.Input;
using Client.Models;
using Client.Helpers;
using Client.Services;
using Newtonsoft.Json.Linq;
using Serilog;

namespace Client.ViewModels
{
    public class ConnectViewModel : INotifyPropertyChanged
    {
        private readonly SessionService _sessionService;
        private readonly SignalRService _signalRService;
        private string _session;
        private string _joinSessionId;
        private string _errorMessage;
        private bool _isJoining;

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

        public bool IsJoining
        {
            get => _isJoining;
            set { _isJoining = value; OnPropertyChanged(); }
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

        public ConnectViewModel(SessionService sessionService, SignalRService signalRService)
        {
            ErrorMessage = string.Empty;
            _sessionService = sessionService;
            _signalRService = signalRService;

            ReconnectCommand = new AsyncRelayCommand(async _ => await ExecuteStartSession());
            JoinSessionCommand = new AsyncRelayCommand(async _ => await ExecuteJoinSessionAsync(), _ => !IsJoining);

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

        private async Task ExecuteJoinSessionAsync()
        {
            IsJoining = true;
            ErrorMessage = string.Empty;

            try
            {
                if (string.IsNullOrWhiteSpace(JoinSessionId))
                {
                    ErrorMessage = "Please enter a valid session ID.";
                    return;
                }
                JoinSessionId = JoinSessionId.Trim();
                var response = await _sessionService.GetActiveSessionAsync();
                if(response.Code == "SESSIONS_FOUND")
                {
                    if(response.Data[0].SessionId == JoinSessionId)
                    {
                        ErrorMessage = "You've already join this SessionId";
                        return;
                    }
                    else if (response.Data[0].SessionId!=JoinSessionId)
                    {
                        foreach (var session in response.Data)
                        {
                            await _sessionService.LeaveSessionAsync(session.SessionId);
                        }
                    }
                    else if(response.Code != "NO_SESSIONS")
                    {
                        ErrorMessage = response.Message;
                        return;
                    }
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
            }
            catch (Exception ex)
            {
                ErrorMessage = ex.Message;
            }
            finally
            {
                IsJoining = false;
            }
        }

        public event PropertyChangedEventHandler PropertyChanged;
        protected void OnPropertyChanged(string propertyName = null)
        {
            PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(propertyName));
        }
    }
}
