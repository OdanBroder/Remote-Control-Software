using System;
using System.Windows;
using Client.Src.Utils;  

namespace Client
{
    public partial class MainWindow : Window
    {
        public MainWindow()
        {
            InitializeComponent();
            //CollectSystemInfo();
        }

        private void CollectSystemInfo()
        {
            string macAddress = GetID.GetMacAddress();
            string biosSerial = GetID.GetBiosSerial();
            string hddSerial = GetID.GetHddSerial();
            string osInstallationId = GetID.GetOsInstallationId();
            string cpuIdentifier = GetID.GetCpuIdentifier();

            string systemInfo = $"{macAddress}-{biosSerial}-{hddSerial}-{osInstallationId}-{cpuIdentifier}";

            MessageBox.Show($"Collected System Information:\n{systemInfo}");
        }

        private void Button_Click(object sender, RoutedEventArgs e)
        {
            CollectSystemInfo();
        }
    }
}
