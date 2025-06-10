using System;
using System.Collections.Generic;
using System.Configuration;
using System.Data;
using System.Linq;
using System.Threading.Tasks;
using System.Windows;
using Client.Views;
using Serilog;
using System.Diagnostics;

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

            // Subscribe to application exit event
            this.Exit += App_Exit;
        }
        protected void ApplicationStart(object sender, StartupEventArgs e)
        {
                var loginView = new LoginView();
                loginView.Show();

            // {
            //     Title = "Test View",
            //     Content = new TestView(),
            //     Width = 800,
            //     Height = 450,
            //     WindowStartupLocation = WindowStartupLocation.CenterScreen
            // };
            // window.Show();
        }

        private void App_Exit(object sender, ExitEventArgs e)
        {
            try
            {
                Log.Information("Application shutting down...");

                // Clean up any remaining windows
                foreach (Window window in Windows)
                {
                    try
                    {
                        if (window.DataContext is IDisposable disposable)
                        {
                            disposable.Dispose();
                        }
                        window.Close();
                    }
                    catch (Exception ex)
                    {
                        Log.Error(ex, "Error closing window");
                    }
                }

                // Force cleanup of any remaining processes
                var currentProcess = Process.GetCurrentProcess();
                foreach (ProcessThread thread in currentProcess.Threads)
                {
                    try
                    {
                        thread.Dispose();
                    }
                    catch { }
                }

                // Flush and close Serilog
                Log.CloseAndFlush();
            }
            catch (Exception ex)
            {
                Log.Error(ex, "Error during application shutdown");
            }
        }
    }
}
