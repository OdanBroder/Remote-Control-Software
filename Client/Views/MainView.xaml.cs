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
using Client.Src.Utils;
using Client.Src.Services;
using Client.Views;

namespace Client.Views
{
    /// <summary>
    /// Interaction logic for MainView.xaml
    /// </summary>
    public partial class MainView : Window
    {
        private ContentControl _connectView;
        private ContentControl FileTransferView; // co the xoa
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

        private void btnClose_Click(object sender, RoutedEventArgs e)
        {
            Application.Current.Shutdown();
        }
        private void RadioButton_Checked(object sender, RoutedEventArgs e)
        {
            if (sender is RadioButton radio && radio.Tag != null)
            {
                string tag = radio.Tag.ToString();
                switch (tag)
                {
                    case "Connect":
                        MainContentPresenter.Content = _connectView;
                        break;
                    case "File":
                        MainContentPresenter.Content = new FileTransferView();
                        break;
                    case "History":
                        MainContentPresenter.Content = new TextBlock { Text = "HistoryView is not implemented yet." };
                        break;
                    default:
                        MainContentPresenter.Content = new TextBlock { Text = $"Unknown view: {tag}" };
                        break;
                }
            }
            else
            {
                MainContentPresenter.Content = new TextBlock { Text = "Invalid radio button or tag." };
            }
        }
    }
}
