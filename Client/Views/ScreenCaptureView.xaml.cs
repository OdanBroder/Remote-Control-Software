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
using System.Windows.Threading;
using System.Diagnostics;
using Client.Services;

namespace Client.Views
{
    /// <summary>
    /// Interaction logic for ScreenCaptureView.xaml
    /// </summary>
    public partial class ScreenCaptureView : Window
    {
        private readonly DispatcherTimer _frameTimer;
        private BitmapSource _currentFrame;
        private readonly object _frameLock = new object();
        private bool _isUpdating;
        private readonly SignalRService _signalRService;

        public ScreenCaptureView(SignalRService signalRService)
        {
            InitializeComponent();
            _signalRService = signalRService;
            _frameTimer = new DispatcherTimer
            {
                Interval = TimeSpan.FromMilliseconds(16) // ~60 FPS
            };
            _frameTimer.Tick += FrameTimer_Tick;
            _frameTimer.Start();
        }

        private void FrameTimer_Tick(object sender, EventArgs e)
        {
            if (_isUpdating) return;

            try
            {
                _isUpdating = true;
                lock (_frameLock)
                {
                    if (_currentFrame != null)
                    {
                        CaptureImage.Source = _currentFrame;
                    }
                }
            }
            catch (Exception ex)
            {
                Console.WriteLine(ex.Message, "Error in frame timer tick");
            }
            finally
            {
                _isUpdating = false;
            }
        }

        protected override void OnClosed(EventArgs e)
        {
            _frameTimer.Stop();
            lock (_frameLock)
            {
                _currentFrame = null;
            }
            base.OnClosed(e);
        }
    }
}
