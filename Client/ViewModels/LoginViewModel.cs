using System.ComponentModel;
using System.Runtime.CompilerServices;
using System.Threading.Tasks;
using System.Windows;
using System.Windows.Input;
using Client.Helpers;
using Client.Services;
using Client.Models;
using System.Windows.Forms.Design;

namespace Client.ViewModels
{
    public class LoginViewModel : INotifyPropertyChanged
    {
        private string _username = "";
        private string _password = "";

        public string Username
        {
            get => _username;
            set { _username = value; OnPropertyChanged(); }
        }

        public string Password
        {
            get => _password;
            set { _password = value; OnPropertyChanged(); }
        }

        public ICommand LoginCommand { get; }

        public LoginViewModel()
        {
            LoginCommand = new RelayCommand(async (_) => await LoginAsync());
        }

        private async Task LoginAsync()
        {
            if (string.IsNullOrWhiteSpace(Username) || string.IsNullOrWhiteSpace(Password))
            {
                MessageBox.Show("Please enter both username and password.", "Missing Information", MessageBoxButton.OK, MessageBoxImage.Warning);
                return;
            }

            var result = await ApiService.Instance.LoginAsync(Username, Password);
            if (result != null)
            {
                // Store token and user info if needed
                //TokenStorage.AccessToken = result.Token;
                //TokenStorage.Username = result.Username;

                MessageBox.Show($"Login successful! Welcome, {result.Username}.", "Success", MessageBoxButton.OK, MessageBoxImage.Information);

                // TODO: Navigate to MainView or dashboard
            }
            else
            {
                MessageBox.Show("Login failed. Please check your credentials.", "Login Error", MessageBoxButton.OK, MessageBoxImage.Error);
            }
        }

        public event PropertyChangedEventHandler? PropertyChanged;
        private void OnPropertyChanged([CallerMemberName] string? propertyName = null)
        {
            PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(propertyName));
        }
    }
}
