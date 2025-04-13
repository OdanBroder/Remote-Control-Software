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
using ScreenCaptureLibrary;
using System.Text.RegularExpressions;



namespace Client.Views
{
    /// <summary>
    /// Interaction logic for MainView.xaml
    /// </summary>
    public partial class MainView : Window
    {
        private ScreenCapture screenCapture;
        public MainView()
        {
            InitializeComponent();
            screenCapture = new ScreenCapture();
        }

        private void StartButton_Click(object sender, RoutedEventArgs e)
        {
            screenCapture.StartStreaming(40);
        }

        private void StopButton_Click(object sender, RoutedEventArgs e)
        {
            screenCapture.StopStreaming();
            screenCapture.Dispose();
            
        }
    }
}
