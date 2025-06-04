using System;
using System.ComponentModel;
using System.Linq;
using System.Net.Http;
using System.Runtime.CompilerServices;
using System.Threading.Tasks;
using System.Windows.Input;
using Client.Helpers;  // Giả sử AsyncRelayCommand ở đây
using Client.Models;
using Client.Services; // ApiService đã có HttpClient config sẵn

namespace Client.ViewModels
{
    public class StartSessionViewModel : INotifyPropertyChanged
    {
        private readonly ApiService _apiService = new ApiService();

        private string _sessionId;
        private string _errorMessage;
        private bool _isStartingSession;

        public string SessionId
        {
            get => _sessionId;
            set { _sessionId = value; OnPropertyChanged(); }
        }

        public string ErrorMessage
        {
            get => _errorMessage;
            set { _errorMessage = value; OnPropertyChanged(); }
        }

        public bool IsStartingSession
        {
            get => _isStartingSession;
            set { _isStartingSession = value; OnPropertyChanged(); }
        }

        public ICommand StartSessionCommand { get; }

        public StartSessionViewModel()
        {
            StartSessionCommand = new AsyncRelayCommand(
                async _ => await ExecuteStartSessionAsync(),
                _ => !IsStartingSession);
        }

        private async Task ExecuteStartSessionAsync()
        {
            IsStartingSession = true;
            ErrorMessage = string.Empty;
            SessionId = string.Empty;

            try
            {

                var response = await _apiService.StartSessionAsync();

                if (response.Success)
                {
                    SessionId = response.Data.SessionId;
                    SessionStorage.SaveSession(SessionId);             
                }
                else
                {
                    ErrorMessage = response.Message ?? "Không thể khởi động session";
                }

            }
            catch (HttpRequestException ex)
            {
                ErrorMessage = $"Lỗi kết nối: {ex.Message}";
            }
            catch (Exception ex)
            {
                ErrorMessage = $"Lỗi không xác định: {ex.Message}";
            }
            finally
            {
                IsStartingSession = false;
            }
        }

        public event PropertyChangedEventHandler PropertyChanged;
        private void OnPropertyChanged([CallerMemberName] string propertyName = null)
            => PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(propertyName));
    }
}
