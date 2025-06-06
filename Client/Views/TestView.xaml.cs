using System.Windows.Controls;
using Client.ViewModels;
using Client.Services;

namespace Client.Views
{
    /// <summary>
    /// Interaction logic for TestView.xaml
    /// </summary>
    public partial class TestView : UserControl
    {
        private readonly SessionService _sessionService;
        private readonly SignalRService _signalRService;

        public TestView()
        {
            InitializeComponent();
            _signalRService = new SignalRService();
            _sessionService = new SessionService(_signalRService);
            this.DataContext = new ConnectViewModel(_sessionService, _signalRService);
        }
    }
}
