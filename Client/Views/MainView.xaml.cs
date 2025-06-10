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
    /// <summary>
    /// Interaction logic for MainView.xaml
    /// </summary>
    public partial class MainView : Window
    {
        private ContentControl _connectView;
        private bool _isClosing = false;

        public MainView()
        {
            InitializeComponent();
            _connectView = new ConnectView();
            MainContentPresenter.Content = _connectView;
        }

        private void TitleBar_MouseDown(object sender, MouseButtonEventArgs e)
        {
            if (e.ChangedButton == MouseButton.Left)
            {
                this.DragMove();
            }
        }

        private void btnMinimize_Click(object sender, RoutedEventArgs e)
        {
            WindowState = WindowState.Minimized;
        }
        private void btnMaximize_Click(object sender, RoutedEventArgs e)
        {
            if (WindowState == WindowState.Maximized)
            {
                WindowState = WindowState.Normal;
            }
            else
            {
                WindowState = WindowState.Maximized;
            }
        }

        private void btnClose_Click(object sender, RoutedEventArgs e)
        {
            Close();
        }

        protected override void OnClosing(System.ComponentModel.CancelEventArgs e)
        {
            if (!_isClosing)
            {
                _isClosing = true;
                try
                {
                    Log.Information("Main window closing...");

                    // Clean up ConnectView resources
                    if (_connectView?.DataContext is IDisposable disposable)
                    {
                        disposable.Dispose();
                    }

                    // Force cleanup of any remaining resources
                    GC.Collect();
                    GC.WaitForPendingFinalizers();
                }
                catch (Exception ex)
                {
                    Log.Error(ex, "Error during window closing");
                }
            }
            base.OnClosing(e);
        }
    }
}
