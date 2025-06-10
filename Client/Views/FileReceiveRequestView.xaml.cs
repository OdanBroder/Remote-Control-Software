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
    /// Interaction logic for FileReceiveRequestView.xaml
    /// </summary>
    public partial class FileReceiveRequestView : Window
    {
        public FileReceiveRequestView(FileReceiveRequestViewModel viewModel)
        {
            InitializeComponent();

            DataContext = viewModel;

            Loaded += (s, e) =>
            {
                if (DataContext is FileReceiveRequestViewModel vm)
                {
                    vm.RequestClosed += result =>
                    {
                        DialogResult = result;
                        Dispatcher.Invoke(Close);
                    };
                }
            };
        }
        private void Border_MouseLeftButtonDown(object sender, MouseButtonEventArgs e)
        {
            this.DragMove();
        }

    }
}
