using System.Windows;

namespace WebsiteAndAppBlocker
{
    public partial class App : Application
    {
        protected override void OnStartup(StartupEventArgs e)
        {
            base.OnStartup(e);

            // Show the login window
            LoginWindow loginWindow = new LoginWindow();
            loginWindow.ShowDialog();

            if (loginWindow.IsAuthenticated)
            {
                //MessageBox.Show("User authenticated. Showing MainWindow.");
                // User authenticated, show the main window
                MainWindow mainWindow = new MainWindow();
                mainWindow.Show();
            }
            else
            {
                //MessageBox.Show("Authentication failed. Shutting down application.");
                // Authentication failed, shut down the application
                Application.Current.Shutdown();
            }
        }

    }
}
    