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
using System.Windows.Navigation;
using System.Windows.Shapes;
using Client.ViewModels;
using Client.Services;
using System.Net.Http;
namespace Client.Views
{
    /// <summary>
    /// Interaction logic for ConnectView.xaml
    /// </summary>
    public partial class ConnectView : UserControl
    {
        private readonly SendInputServices _sendInput = new SendInputServices();
        private InputMonitor inputMonitor;
        public ConnectView()
        {
            InitializeComponent();
            this.DataContext = new ConnectViewModel();
        }

        private void Button_Click(object sender, RoutedEventArgs e)
        {
            try
            {
                Console.WriteLine("Input monitoring started...");
                inputMonitor = new InputMonitor(_sendInput);
                inputMonitor.Start();
            }
            catch (Exception ex)
            {
                Console.WriteLine($"SendInput failed: {ex.Message}");
             }
        }
        private void Button_Click_1(object sender, RoutedEventArgs e)
        {
            try
            {
                Console.WriteLine("Input monitoring stopping...");
                inputMonitor.Stop();
            }
            catch (Exception ex)
            {
                Console.WriteLine($"SendInput failed: {ex.Message}");
            }
        }
    }
}
