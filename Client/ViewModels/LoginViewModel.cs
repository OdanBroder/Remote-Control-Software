using System;
using System.ComponentModel;
using System.Net.Http;
using System.Runtime.CompilerServices;
using System.Threading.Tasks;
using System.Windows;
using System.Windows.Input;
using Client.Models;
using Client.Services;
using Client.Helpers;
using Client.Views;
using System.Linq;

namespace Client.ViewModels
{
    public class LoginViewModel : INotifyPropertyChanged
    {
        private readonly ApiService _apiService = new ApiService();

        private string _username = "";
        private string _password = "";
        private string _errorMessage = "";
        private bool _isLoggingIn;
        private bool _isViewVisible = true;

        public string Username
        {
            get => _username;
            set
            {
                _username = value;
                OnPropertyChanged();
                RaiseCanExecuteChanged();
            }
        }

        public string Password
        {
            get => _password;
            set
            {
                _password = value;
                OnPropertyChanged();
                RaiseCanExecuteChanged();
            }
        }

        public string ErrorMessage
        {
            get => _errorMessage;
            set { _errorMessage = value; OnPropertyChanged(); }
        }

        public bool IsLoggingIn
        {
            get => _isLoggingIn;
            set
            {
                _isLoggingIn = value;
                OnPropertyChanged();
                RaiseCanExecuteChanged();
            }
        }

        public bool IsViewVisible
        {
            get => _isViewVisible;
            set { _isViewVisible = value; OnPropertyChanged(); }
        }

        public ICommand LoginCommand { get; }

        public LoginViewModel()
        {
            LoginCommand = new AsyncRelayCommand(async obj => await ExecuteLogin(), obj => CanExecuteLogin());
        }

        private bool CanExecuteLogin()
        {
            return !IsLoggingIn && !string.IsNullOrWhiteSpace(Username) && !string.IsNullOrWhiteSpace(Password);
        }

        private async Task ExecuteLogin()
        {
            IsLoggingIn = true;
            ErrorMessage = "";
            OnPropertyChanged(nameof(ErrorMessage));

            try
            {
                var loginRequest = new LoginRequest
                {
                    Username = Username,
                    Password = Password
                };

                var result = await _apiService.LoginAsync(loginRequest);

                if (result.Success)
                {
                    TokenStorage.SaveToken(result.Data.Token);
                    _apiService.SetAuthToken(result.Data.Token);
                    var mainView = new MainView();
                    mainView.Show();
                    Application.Current.Windows
                        .OfType<Window>()
                        .FirstOrDefault(w => w.DataContext == this)
                        ?.Close();
                }
            }
            catch (HttpRequestException ex)
            {
                ErrorMessage = ex.Message;
            }
            catch (Exception ex)
            {
                ErrorMessage = "Lỗi không xác định: " + ex.Message;
            }
            finally
            {
                IsLoggingIn = false;
            }
        }

        private void RaiseCanExecuteChanged()
        {
            if (LoginCommand is AsyncRelayCommand asyncCmd)
            {
                asyncCmd.RaiseCanExecuteChanged();
            }
        }

        public event PropertyChangedEventHandler PropertyChanged;
        private void OnPropertyChanged([CallerMemberName] string name = null)
        {
            PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(name));
        }
    }
}