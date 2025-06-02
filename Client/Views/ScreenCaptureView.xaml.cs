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

namespace Client.Views
{
    /// <summary>
    /// Interaction logic for ScreenCaptureView.xaml
    /// </summary>
    public partial class ScreenCaptureView : Window
    {
        public ScreenCaptureView()
        {
            InitializeComponent();
            UpdateFrame(LoadTestImage());
        }

        public void UpdateFrame(BitmapImage frame)
        {
            CaptureImage.Source = frame;
        }

        private BitmapImage LoadTestImage()
        {
            var image = new BitmapImage();
            image.BeginInit();
            image.UriSource = new Uri("pack://application:,,,/Images/key-icon.png");
            image.EndInit();
            return image;
        }
    }
}
