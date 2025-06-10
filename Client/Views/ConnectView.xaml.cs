using System.Windows.Controls;
using Client.ViewModels;
using Client.Services;
using System.Windows.Media;
using System.Windows;

namespace Client.Views
{
    /// <summary>
    /// Interaction logic for ConnectView.xaml
    /// </summary>
    public partial class ConnectView : UserControl
    {
        private readonly SessionService _sessionService;
        private readonly SignalRService _signalRService;

        public ConnectView()
        {
            InitializeComponent();

            this.Loaded += (s, e) =>
            {
                UpdateClip();
                this.SizeChanged += (s2, e2) => UpdateClip();
            };

            _signalRService = new SignalRService();
            _sessionService = new SessionService(_signalRService);
            this.DataContext = new ConnectViewModel(_sessionService, _signalRService);
        }

        private void UpdateClip()
        {
            this.Clip = new RectangleGeometry()
            {
                Rect = new Rect(0, 0, this.ActualWidth, this.ActualHeight),
                RadiusX = 15,
                RadiusY = 15
            };
        }
    }
}
