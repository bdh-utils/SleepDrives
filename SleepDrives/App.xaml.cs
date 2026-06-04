using System.Windows;

namespace SleepDrives
{
    /// <summary>
    /// Interaction logic for App.xaml.
    /// </summary>
    public partial class App : System.Windows.Application
    {
        private void App_Startup(object sender, StartupEventArgs e)
        {
            // Create the window ourselves (rather than via StartupUri) so it can
            // decide whether to show or start hidden in the tray. The tray icon
            // and OnExplicitShutdown keep the app alive even when hidden.
            var window = new MainWindow();
            MainWindow = window;
            window.Initialize();
        }
    }
}
