using System;
using System.Collections.Generic;
using System.Configuration;
using System.Data;
using System.Linq;
using System.Threading.Tasks;
using System.Windows;
using Client.Views;
using Serilog;
namespace Client
{
    /// <summary>
    /// Interaction logic for App.xaml
    /// </summary>
    public partial class App : Application
    {
        public App()
        {
            // Configure Serilog as early as possible
            Log.Logger = new LoggerConfiguration()
                .MinimumLevel.Debug()
                .WriteTo.Console()
                .WriteTo.File("logs/log.txt", rollingInterval: RollingInterval.Day)
                .CreateLogger();

            Log.Information("Application initialized.");
        }
        protected void ApplicationStart(object sender, StartupEventArgs e)
        {
            var mainView = new MainView();
            mainView.Show();
            //var loginView = new LoginView();
            //loginView.Show();
        }
    }
}
