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
using System.Windows.Interop;

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
        private InputMonitor _inputMonitor;
        private SendInputServices _inputSender;

        public ScreenCaptureView(SignalRService signalRService)
        {
            InitializeComponent();
            _signalRService = signalRService;
            _inputSender = new SendInputServices(_signalRService);
            
            _frameTimer = new DispatcherTimer
            {
                Interval = TimeSpan.FromMilliseconds(16) // ~60 FPS
            };
            _frameTimer.Tick += FrameTimer_Tick;
            _frameTimer.Start();

            // Subscribe to window activation events
            Activated += ScreenCaptureView_Activated;
            Deactivated += ScreenCaptureView_Deactivated;
        }

        protected override void OnSourceInitialized(EventArgs e)
        {
            base.OnSourceInitialized(e);
            
            // Get the window handle
            IntPtr windowHandle = new WindowInteropHelper(this).Handle;
            
            // Initialize InputMonitor with the window handle
            _inputMonitor = new InputMonitor(_inputSender, windowHandle);
            
            // Start monitoring when the window is initialized
            _inputMonitor.Start();
        }

        private void ScreenCaptureView_Activated(object sender, EventArgs e)
        {
            // Start or re-enable monitoring when window gains focus
            if (_inputMonitor != null)
            {
                _inputMonitor.Start();
            }
        }

        private void ScreenCaptureView_Deactivated(object sender, EventArgs e)
        {
            // Stop monitoring when window loses focus
            if (_inputMonitor != null)
            {
                _inputMonitor.Stop();
            }
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
            // Clean up InputMonitor
            if (_inputMonitor != null)
            {
                _inputMonitor.Dispose();
                _inputMonitor = null;
            }

            _frameTimer.Stop();
            lock (_frameLock)
            {
                _currentFrame = null;
            }
            base.OnClosed(e);
        }
    }
}
