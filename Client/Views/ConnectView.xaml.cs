using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using System.Windows;
using System.Windows.Controls;
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
        private SessionService _sessionService;
        private SendWebRTCSignal _streamScreen;
        private readonly SignalRService _signalRService;
        public ConnectView()
        {
            InitializeComponent();
            _signalRService = new SignalRService();
            _sessionService = new SessionService(_signalRService);
            _streamScreen = new SendWebRTCSignal(_signalRService);
            this.DataContext = new ConnectViewModel(_sessionService, _signalRService);
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
                inputMonitor.Dispose();
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

        private async void Button_Click_2(object sender, RoutedEventArgs e)
        {
            try
            {
                // Connect to SignalR first
                SessionResponse sessionResponse = await _sessionService.GetActiveSessionAsync();
                await _signalRService.ConnectToHubAsync(sessionResponse.Data[0].SessionId);

                // Wait for connection (with retry if needed)
                await WaitForSignalRConnection();
                var webRTCSignal = new SendWebRTCSignal(_signalRService);
                var response = await webRTCSignal.StartStreaming(isStreamer: true);
            }
            catch (Exception ex)
            {
                _streamScreen.Dispose();
                await _signalRService.DisconnectAsync();
                Console.WriteLine($"Error when streaming: {ex.Message}");
            }
        }

        private async void Button_Click_3(object sender, RoutedEventArgs e)
        {
            try
            {
                _streamScreen.Dispose();
                await _signalRService.DisconnectAsync();
            }
            catch (Exception ex)
            {
                Console.WriteLine($"Error when streaming: {ex.Message}");
            }
        }

        private async void Button_Click_4(object sender, RoutedEventArgs e)
        {
            try
            {
                SessionResponse sessionResponse = await _sessionService.GetActiveSessionAsync();
                await _signalRService.ConnectToHubAsync(sessionResponse.Data[0].SessionId);
                await WaitForSignalRConnection();
                var webRTCSignal = new SendWebRTCSignal(_signalRService);
                var videoProcessor = new VideoProcessor();
                var response = await webRTCSignal.StartStreaming(isStreamer: false);
             
            }
            catch (Exception ex)
            {
                Console.WriteLine($"Error when accept streaming: {ex.Message}");
            }
        }
        private async Task WaitForSignalRConnection()
        {
            int retries = 0;
            while (!_signalRService.IsConnected && retries++ < 10)
            {
                await Task.Delay(500);
            }

            if (!_signalRService.IsConnected)
            {
                throw new Exception("Failed to establish SignalR connection");
            }
        }

    }
}
