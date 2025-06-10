using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Data;
using System.Windows.Documents;
using System.Windows.Input;
using System.Windows.Media;
using System.Windows.Media.Imaging;
using System.Windows.Shapes;
using Client.Views;
using Serilog;

namespace Client.Views
{
    public partial class LoginView : Window
    {
        private bool _isClosing = false;

        public LoginView()
        {
            InitializeComponent();
        }

        private void Window_MouseDown(object sender, MouseButtonEventArgs e)
        {
            if (e.LeftButton == MouseButtonState.Pressed)
                DragMove();
        }

        private void btnMinimize_Click(object sender, RoutedEventArgs e)
        {
            WindowState = WindowState.Minimized;
        }

        private void btnClose_Click(object sender, RoutedEventArgs e)
        {
            Close();
        }

        private void OnGoToRegister(object sender, RoutedEventArgs e)
        {
            var register = new RegisterView();
            register.Show();
            this.Close();
        }

        protected override void OnClosing(System.ComponentModel.CancelEventArgs e)
        {
            if (!_isClosing)
            {
                _isClosing = true;
                try
                {
                    Log.Information("Login window closing...");

                    // Clean up ViewModel resources
                    if (DataContext is IDisposable disposable)
                    {
                        disposable.Dispose();
                    }

                    // Force cleanup of any remaining resources
                    GC.Collect();
                    GC.WaitForPendingFinalizers();
                }
                catch (Exception ex)
                {
                    Log.Error(ex, "Error during login window closing");
                }
            }
            base.OnClosing(e);
        }
    }
}