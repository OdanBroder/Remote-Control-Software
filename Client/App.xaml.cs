using System;
using System.Collections.Generic;
using System.Configuration;
using System.Data;
using System.IO;
using System.Linq;
using System.Runtime.InteropServices;
using System.Threading.Tasks;
using System.Windows;
using Serilog;
namespace Client
{
    /// <summary>
    /// Interaction logic for App.xaml
    /// </summary>
    public partial class App : Application
    {

        protected override void OnStartup(StartupEventArgs e)
        {
            Directory.CreateDirectory("Logs");
            Log.Logger = new LoggerConfiguration()
                .MinimumLevel.Debug()
                .WriteTo.File("Logs/log.txt", rollingInterval: RollingInterval.Day)
                .WriteTo.Console()
                .CreateLogger();

            Log.Information("Ứng dụng khởi động");

            base.OnStartup(e);
            var mainView = new Views.MainView();
            mainView.Show();
        }

        protected override void OnExit(ExitEventArgs e)
        {
            Log.Information("Ứng dụng thoát");
            Log.CloseAndFlush();

            base.OnExit(e);
        }
    }
}
