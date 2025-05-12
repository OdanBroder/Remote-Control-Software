using System;
using System.ComponentModel;
using System.Linq;
using System.Net.Http;
using System.Runtime.CompilerServices;
using System.Threading.Tasks;
using System.Windows;
using System.Windows.Input;
using Client.Helpers;
using Client.Models;
using Client.Services;
using Client.Views;

namespace Client.ViewModels
{
    public class RegisterViewModel : INotifyPropertyChanged
    {
        private readonly ApiService _apiService = new ApiService();

        private string _name = string.Empty;
        private string _username = string.Empty;
        private string _password = string.Empty;
        private string _errorMessage;
        private bool _isRegistering;
        private bool _isViewVisible = true;

        public string Name
        {
            get => _name;
            set { _name = value; OnPropertyChanged(); RaiseCanExecuteChanged(); }
        }

        public string Username
        {
            get => _username;
            set { _username = value; OnPropertyChanged(); RaiseCanExecuteChanged(); }
        }

        public string Password
        {
            get => _password;
            set { _password = value; OnPropertyChanged(); RaiseCanExecuteChanged(); }
        }

        public string ErrorMessage
        {
            get => _errorMessage;
            set { _errorMessage = value; OnPropertyChanged(); }
        }

        public bool IsRegistering
        {
            get => _isRegistering;
            set { _isRegistering = value; OnPropertyChanged(); RaiseCanExecuteChanged(); }
        }

        public bool IsViewVisible
        {
            get => _isViewVisible;
            set { _isViewVisible = value; OnPropertyChanged(); }
        }

        public ICommand RegisterCommand { get; }

        public RegisterViewModel()
        {
            RegisterCommand = new AsyncRelayCommand(
                async _ => await ExecuteRegisterAsync(),
                _ => CanExecuteRegister());
        }

        private bool CanExecuteRegister()
            => !IsRegistering
               && !string.IsNullOrWhiteSpace(Name)
               && !string.IsNullOrWhiteSpace(Username)
               && !string.IsNullOrWhiteSpace(Password);

        private async Task ExecuteRegisterAsync()
        {
            IsRegistering = true;
            ErrorMessage = string.Empty;

            try
            {
                var request = new RegisterRequest
                {
                    Username = Username,
                    Password = Password
                };

                var response = await _apiService.RegisterAsync(request);

                MessageBox.Show("Đăng ký thành công!", "Thành công", MessageBoxButton.OK, MessageBoxImage.Information);

                // Mở lại LoginView
                var loginView = new LoginView();
                loginView.Show();

                // Đóng RegisterView hiện tại
                Application.Current.Windows
                    .OfType<Window>()
                    .FirstOrDefault(w => w.DataContext == this)
                    ?.Close();
            }
            catch (HttpRequestException ex)
            {
                ErrorMessage = ex.Message;
            }
            catch (Exception ex)
            {
                ErrorMessage = $"Lỗi không xác định: {ex.Message}";
            }
            finally
            {
                IsRegistering = false;
            }
        }

        private void RaiseCanExecuteChanged()
        {
            if (RegisterCommand is AsyncRelayCommand cmd)
                cmd.RaiseCanExecuteChanged();
        }

        public event PropertyChangedEventHandler PropertyChanged;
        private void OnPropertyChanged([CallerMemberName] string propertyName = null)
            => PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(propertyName));
    }
}
