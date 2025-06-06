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

        public ScreenCaptureView()
        {
            InitializeComponent();
            _frameTimer = new DispatcherTimer
            {
                Interval = TimeSpan.FromMilliseconds(16) // ~60 FPS
            };
            _frameTimer.Tick += FrameTimer_Tick;
            _frameTimer.Start();
        }

        public void UpdateFrame(BitmapSource frame)
        {
            lock (_frameLock)
            {
                _currentFrame = frame;
            }
        }

        private void FrameTimer_Tick(object sender, EventArgs e)
        {
            lock (_frameLock)
            {
                if (_currentFrame != null)
                {
                    CaptureImage.Source = _currentFrame;
                }
            }
        }

        protected override void OnClosed(EventArgs e)
        {
            _frameTimer.Stop();
            base.OnClosed(e);
        }
    }
}
