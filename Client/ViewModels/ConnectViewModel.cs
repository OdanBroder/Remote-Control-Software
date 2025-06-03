using System.ComponentModel;
using Client.Models;
using Client.Services;
using System.Threading.Tasks;
using System;

namespace Client.ViewModels
{
    public class ConnectViewModel : INotifyPropertyChanged
    {
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

        public ConnectViewModel()
        {
            _ = ExecuteStartSession();
        }

        public async Task ExecuteStartSession()
        {
            try
            {
                var response = await _sessionService.GetActiveSessionAsync();

                if (response.Code == "SESSIONS_FOUND")
                {
                    string sessionId = response.Data[0].SessionId;
                    await _sessionService.LeaveSessionAsync(sessionId);
                }

                var startResponse = await _sessionService.StartSessionAsync();

                if (startResponse.Success)
                {
                    Session = startResponse.Data.SessionId;
                }
                else
                {
                    throw new Exception(startResponse.Message);
                }
            }
            catch (Exception ex)
            {
                Console.WriteLine($"Error starting session: {ex.Message}");
            }
        }
        public event PropertyChangedEventHandler PropertyChanged;

        protected void OnPropertyChanged(string propertyName)
        {
            PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(propertyName));
        }
    }
}
