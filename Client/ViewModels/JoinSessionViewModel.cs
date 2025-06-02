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
    public class JoinSessionViewModel : INotifyPropertyChanged
    {
        private readonly ApiService _apiService = new ApiService();

        private string _sessionId;
        private string _errorMessage;
        private bool _isJoining;

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

        public bool IsJoining
        {
            get => _isJoining;
            set { _isJoining = value; OnPropertyChanged(); }
        }

        public ICommand JoinSessionCommand { get; }

        public JoinSessionViewModel()
        {
            JoinSessionCommand = new AsyncRelayCommand(
                async _ => await ExecuteJoinSessionAsync(),
                _ => !IsJoining);
        }

        private async Task ExecuteJoinSessionAsync()
        {
            IsJoining = true;
            ErrorMessage = string.Empty;

            try
            {
                var token = TokenStorage.LoadToken();
                if (string.IsNullOrEmpty(token))
                {
                    ErrorMessage = "Vui lòng đăng nhập trước.";
                    return;
                }

                if (string.IsNullOrWhiteSpace(SessionId))
                {
                    ErrorMessage = "Vui lòng nhập session ID.";
                    return;
                }

                var response = await _apiService.JoinSessionAsync(SessionId);

                if (response.Success)
                {
                    // TODO: Connect to SignalR Hub with SessionId
                    Console.WriteLine("Đã tham gia session thành công: " + SessionId);
                }
                else
                {
                    ErrorMessage = response.Message ?? "Tham gia session thất bại.";
                }
            }
            catch (Exception ex)
            {
                ErrorMessage = "Lỗi khi tham gia session: " + ex.Message;
            }
            finally
            {
                IsJoining = false;
            }
        }

        public event PropertyChangedEventHandler PropertyChanged;
        private void OnPropertyChanged([CallerMemberName] string propertyName = null)
            => PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(propertyName));
    }
}
