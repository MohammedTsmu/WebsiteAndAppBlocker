using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Security.Principal;
using System.Timers;
using System.Windows;
using System.Windows.Threading;
using Hardcodet.Wpf.TaskbarNotification;
using System.Windows.Input;


namespace WebsiteAndAppBlocker
{
    public partial class MainWindow : Window
    {
        // Variables and collections
        private Timer processMonitorTimer;
        private List<string> blockedApps = new List<string>();
        private List<string> blockedWebsites = new List<string>();
        private Dictionary<DateTime, int> unblockAttempts = new Dictionary<DateTime, int>();
        private int maxAttemptsPerHour = 2; // Maximum attempts allowed per hour

        private string blockedWebsitesFilePath = Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData), "WebsiteAndAppBlocker", "blocked_websites.txt");
        private string blockedAppsFilePath = Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData), "WebsiteAndAppBlocker", "blocked_apps.txt");

        // Blocking period from 4 AM to midnight (00:00 of the next day)
        private TimeSpan blockingStartTime = new TimeSpan(4, 0, 0);   // 4 AM
        private TimeSpan blockingEndTime = new TimeSpan(0, 0, 0);     // Midnight (next day)


        // DispatcherTimer for UI updates
        private DispatcherTimer uiUpdateTimer;

        public MainWindow()
        {
            InitializeComponent();

            // Load blocked websites and apps
            LoadBlockedWebsitesFromFile();
            LoadBlockedAppsFromFile();

            // Refresh UI lists
            RefreshBlockedWebsitesList();
            RefreshBlockedAppsList();

            // Start process monitoring
            StartProcessMonitoring();

            // Start UI update timer
            StartUIUpdateTimer();

            // Handle the StateChanged event
            this.StateChanged += MainWindow_StateChanged;

            // Hide window during blocking period
            if (IsWithinBlockingPeriod())
            {
                this.Hide();
                ShowTrayIcon();
            }
            else
            {
                // Optionally, start minimized even outside blocking period
                // this.WindowState = WindowState.Minimized;
            }
        }

        // Administrator check
        private bool IsAdministrator()
        {
            var identity = WindowsIdentity.GetCurrent();
            var principal = new WindowsPrincipal(identity);
            return principal.IsInRole(WindowsBuiltInRole.Administrator);
        }

        // Restart as admin
        private void RestartAsAdmin()
        {
            var exeName = Process.GetCurrentProcess().MainModule.FileName;
            var startInfo = new ProcessStartInfo(exeName)
            {
                Verb = "runas"
            };
            Process.Start(startInfo);
            Application.Current.Shutdown();
        }

        // Website blocking methods
        private void BlockWebsite(string website)
        {
            if (!blockedWebsites.Contains(website, StringComparer.OrdinalIgnoreCase))
            {
                blockedWebsites.Add(website);
                UpdateHostsFile();
                SaveBlockedWebsitesToFile();
                RefreshBlockedWebsitesList();
            }
        }

        private void UnblockWebsite(string website)
        {
            if (blockedWebsites.Contains(website, StringComparer.OrdinalIgnoreCase))
            {
                blockedWebsites.Remove(website);
                UpdateHostsFile();
                SaveBlockedWebsitesToFile();
                RefreshBlockedWebsitesList();
            }
        }

        private void UpdateHostsFile()
        {
            string hostsPath = Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.System), @"drivers\etc\hosts");
            string[] originalLines = File.ReadAllLines(hostsPath);
            List<string> newLines = new List<string>();

            // Remove any previous entries added by the application
            foreach (var line in originalLines)
            {
                if (!line.Contains("# Blocked by WebsiteAndAppBlocker"))
                {
                    newLines.Add(line);
                }
            }

            // Add blocked websites
            foreach (var website in blockedWebsites)
            {
                newLines.Add($"127.0.0.1 {website} # Blocked by WebsiteAndAppBlocker");
                newLines.Add($"127.0.0.1 www.{website} # Blocked by WebsiteAndAppBlocker");
            }

            File.WriteAllLines(hostsPath, newLines);

            // Flush DNS cache
            FlushDnsCache();
        }

        private void FlushDnsCache()
        {
            ProcessStartInfo psi = new ProcessStartInfo("ipconfig", "/flushdns");
            psi.CreateNoWindow = true;
            psi.WindowStyle = ProcessWindowStyle.Hidden;
            psi.UseShellExecute = true;
            psi.Verb = "runas"; // Ensure it runs as admin
            Process.Start(psi);
        }

        // Process monitoring
        private void StartProcessMonitoring()
        {
            processMonitorTimer = new Timer(2000); // Check every 2 seconds
            processMonitorTimer.Elapsed += ProcessMonitorTimer_Elapsed;
            processMonitorTimer.Start();
        }

        private void ProcessMonitorTimer_Elapsed(object sender, ElapsedEventArgs e)
        {
            if (!IsWithinBlockingPeriod())
                return;

            var processes = Process.GetProcesses();

            foreach (var process in processes)
            {
                string processName = process.ProcessName;

                // Check if the process is in the blocked apps list
                if (blockedApps.Contains(processName, StringComparer.OrdinalIgnoreCase))
                {
                    try
                    {
                        process.Kill();
                    }
                    catch
                    {
                        // Handle exceptions if necessary
                    }
                }
            }
        }

        // Event handlers for UI elements
        private void BlockWebsiteButton_Click(object sender, RoutedEventArgs e)
        {
            if (!IsAdministrator())
            {
                RestartAsAdmin();
                return;
            }

            //if (IsWithinBlockingPeriod() && !PromptForAuthentication())
            //    return;

            string website = WebsiteTextBox.Text.Trim();
            if (!string.IsNullOrEmpty(website))
            {
                BlockWebsite(website);
                MessageBox.Show($"Blocked {website}");
                WebsiteTextBox.Clear();
            }
        }

        private void UnblockWebsiteButton_Click(object sender, RoutedEventArgs e)
        {
            if (!IsAdministrator())
            {
                RestartAsAdmin();
                return;
            }

            // Always prompt for authentication before unblocking
            if (!PromptForAuthentication())
                return;

            if (!CanAttemptUnblock())
                return;

            // Show the challenge window
            ChallengeWindow challengeWindow = new ChallengeWindow();
            challengeWindow.Owner = this; // Set owner to block interaction with main window
            challengeWindow.ShowDialog();

            if (!challengeWindow.IsChallengeCompleted)
                return; // User failed the challenge or closed the window

            string website = WebsiteTextBox.Text.Trim();
            if (!string.IsNullOrEmpty(website))
            {
                UnblockWebsite(website);
                MessageBox.Show($"Unblocked {website}");
                WebsiteTextBox.Clear();
            }
        }


        private void UnblockSelectedWebsiteButton_Click(object sender, RoutedEventArgs e)
        {
            if (!IsAdministrator())
            {
                RestartAsAdmin();
                return;
            }

            // Always prompt for authentication before unblocking
            if (!PromptForAuthentication())
                return;

            if (!CanAttemptUnblock())
                return;

            if (BlockedWebsitesListBox.SelectedItem != null)
            {
                // Show the challenge window
                ChallengeWindow challengeWindow = new ChallengeWindow();
                challengeWindow.Owner = this;
                challengeWindow.ShowDialog();

                if (!challengeWindow.IsChallengeCompleted)
                    return; // User failed the challenge or closed the window

                string website = BlockedWebsitesListBox.SelectedItem.ToString();
                UnblockWebsite(website);
                MessageBox.Show($"Unblocked {website}");
            }
            else
            {
                MessageBox.Show("Please select a website to unblock.");
            }
        }


        private bool CanAttemptUnblock()
        {
            DateTime currentHour = DateTime.Now.Date.AddHours(DateTime.Now.Hour);

            // Remove attempts from previous hours
            var keysToRemove = unblockAttempts.Keys.Where(k => k < currentHour).ToList();
            foreach (var key in keysToRemove)
            {
                unblockAttempts.Remove(key);
            }

            if (!unblockAttempts.ContainsKey(currentHour))
            {
                unblockAttempts[currentHour] = 0;
            }

            if (unblockAttempts[currentHour] >= maxAttemptsPerHour)
            {
                MessageBox.Show("You have reached the maximum number of unblocking attempts for this hour. Please try again later.");
                return false;
            }

            unblockAttempts[currentHour]++;
            return true;
        }

        private void BlockSelectedAppButton_Click(object sender, RoutedEventArgs e)
        {
            //if (IsWithinBlockingPeriod() && !PromptForAuthentication())
            //    return;

            if (RunningAppsListBox.SelectedValue != null)
            {
                string appName = RunningAppsListBox.SelectedValue.ToString();
                if (!blockedApps.Contains(appName, StringComparer.OrdinalIgnoreCase))
                {
                    blockedApps.Add(appName);
                    SaveBlockedAppsToFile();
                    RefreshBlockedAppsList();
                    MessageBox.Show($"Blocked {appName}");
                }
                else
                {
                    MessageBox.Show($"{appName} is already blocked.");
                }
            }
            else
            {
                MessageBox.Show("Please select an app from the list.");
            }
        }

        private void BlockAppButton_Click(object sender, RoutedEventArgs e)
        {
            //if (IsWithinBlockingPeriod() && !PromptForAuthentication())
            //    return;

            string appName = AppTextBox.Text.Trim();
            if (!string.IsNullOrEmpty(appName))
            {
                blockedApps.Add(appName);
                SaveBlockedAppsToFile();
                RefreshBlockedAppsList();
                MessageBox.Show($"Blocked {appName}");
            }
        }

        private void UnblockSelectedAppButton_Click(object sender, RoutedEventArgs e)
        {
            // Always prompt for authentication before unblocking
            if (!PromptForAuthentication())
                return;

            if (!CanAttemptUnblock())
                return;

            if (BlockedAppsListBox.SelectedItem != null)
            {
                // Show the challenge window
                ChallengeWindow challengeWindow = new ChallengeWindow();
                challengeWindow.Owner = this;
                challengeWindow.ShowDialog();

                if (!challengeWindow.IsChallengeCompleted)
                    return; // User failed the challenge or closed the window

                string appName = BlockedAppsListBox.SelectedItem.ToString();
                blockedApps.Remove(appName);
                SaveBlockedAppsToFile();
                RefreshBlockedAppsList();
                MessageBox.Show($"Unblocked {appName}");
            }
            else
            {
                MessageBox.Show("Please select an app to unblock.");
            }
        }


        private void RefreshAppListButton_Click(object sender, RoutedEventArgs e)
        {
            var processes = Process.GetProcesses()
                                   .Where(p => !string.IsNullOrWhiteSpace(p.MainWindowTitle))
                                   .Select(p => new { Name = p.ProcessName, Title = $"{p.MainWindowTitle} ({p.ProcessName})" })
                                   .OrderBy(p => p.Title)
                                   .ToList();

            RunningAppsListBox.ItemsSource = processes;
            RunningAppsListBox.DisplayMemberPath = "Title";
            RunningAppsListBox.SelectedValuePath = "Name";
        }

        // Password setting
        private void SetPasswordButton_Click(object sender, RoutedEventArgs e)
        {
            string newPassword = PasswordBox.Password.Trim();

            if (string.IsNullOrEmpty(newPassword))
            {
                MessageBox.Show("Password cannot be empty.");
                return;
            }

            string hashedPassword = HashPassword(newPassword);

            try
            {
                string passwordFilePath = GetPasswordFilePath();
                File.WriteAllText(passwordFilePath, hashedPassword);
                MessageBox.Show("Password has been set successfully.");
                PasswordBox.Clear();
            }
            catch (Exception ex)
            {
                MessageBox.Show($"Failed to set password: {ex.Message}");
            }
        }


        private string HashPassword(string password)
        {
            using (var sha256 = System.Security.Cryptography.SHA256.Create())
            {
                byte[] bytes = sha256.ComputeHash(System.Text.Encoding.UTF8.GetBytes(password));
                return Convert.ToBase64String(bytes);
            }
        }

        private string GetPasswordFilePath()
        {
            string appDataPath = Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData);
            string appFolder = Path.Combine(appDataPath, "WebsiteAndAppBlocker");
            if (!Directory.Exists(appFolder))
            {
                Directory.CreateDirectory(appFolder);
            }
            return Path.Combine(appFolder, "password.txt");
        }

        private void ChangePasswordButton_Click(object sender, RoutedEventArgs e)
        {
            string currentPassword = CurrentPasswordBox.Password.Trim();
            string newPassword = NewPasswordBox.Password.Trim();
            string confirmNewPassword = ConfirmNewPasswordBox.Password.Trim();

            if (string.IsNullOrEmpty(currentPassword) || string.IsNullOrEmpty(newPassword) || string.IsNullOrEmpty(confirmNewPassword))
            {
                MessageBox.Show("All password fields are required.");
                return;
            }

            // Authenticate current password
            if (!AuthenticateUser(currentPassword))
            {
                MessageBox.Show("Current password is incorrect.");
                return;
            }

            if (newPassword != confirmNewPassword)
            {
                MessageBox.Show("New passwords do not match.");
                return;
            }

            // Set new password
            string hashedPassword = HashPassword(newPassword);
            string passwordFilePath = GetPasswordFilePath();
            File.WriteAllText(passwordFilePath, hashedPassword);

            MessageBox.Show("Password has been changed successfully.");
            CurrentPasswordBox.Clear();
            NewPasswordBox.Clear();
            ConfirmNewPasswordBox.Clear();
        }

        // Authentication methods
        private bool AuthenticateUser(string password)
        {
            string passwordFilePath = GetPasswordFilePath();

            if (!File.Exists(passwordFilePath))
            {
                // No password set
                return true;
            }

            if (string.IsNullOrEmpty(password))
                return false;

            string storedHashedPassword = File.ReadAllText(passwordFilePath);
            string enteredHashedPassword = HashPassword(password);

            return storedHashedPassword == enteredHashedPassword;
        }

        private bool PromptForAuthentication()
        {
            // Show the login window
            LoginWindow loginWindow = new LoginWindow();
            loginWindow.Owner = this;
            loginWindow.ShowDialog();

            return loginWindow.IsAuthenticated;
        }

        // Helper methods
        //private bool IsWithinBlockingPeriod()
        //{
        //    TimeSpan now = DateTime.Now.TimeOfDay;
        //    return (now >= blockingStartTime) && (now <= blockingEndTime);
        //}
        private bool IsWithinBlockingPeriod()
        {
            TimeSpan now = DateTime.Now.TimeOfDay;

            if (blockingStartTime <= blockingEndTime)
            {
                // For periods within the same day
                return now >= blockingStartTime && now <= blockingEndTime;
            }
            else
            {
                // For periods that span midnight
                return now >= blockingStartTime || now <= blockingEndTime;
            }
        }


        // Handle window state changes
        private void MainWindow_StateChanged(object sender, EventArgs e)
        {
            if (this.WindowState == WindowState.Minimized)
            {
                this.Hide();
                ShowTrayIcon();
            }
        }

        // Show the tray icon
        private void ShowTrayIcon()
        {
            TrayIcon.Visibility = Visibility.Visible;
        }

        // Hide the tray icon
        private void HideTrayIcon()
        {
            TrayIcon.Visibility = Visibility.Collapsed;
        }

        // Tray icon menu item: Open
        private void TrayMenu_Open_Click(object sender, RoutedEventArgs e)
        {
            if (!PromptForAuthentication())
                return;

            this.Show();
            this.WindowState = WindowState.Normal;
            this.Activate();
            HideTrayIcon();
        }

        // Tray icon menu item: Exit
        private void TrayMenu_Exit_Click(object sender, RoutedEventArgs e)
        {
            if (IsWithinBlockingPeriod())
            {
                MessageBox.Show("The application cannot be closed during study hours.");
                return;
            }

            // Clean up tray icon
            TrayIcon.Dispose();

            // Close the application
            canClose = false;
            Application.Current.Shutdown();
        }

        private void TrayIcon_TrayMouseDoubleClick(object sender, RoutedEventArgs e)
        {
            if (!PromptForAuthentication())
                return;

            this.Show();
            this.WindowState = WindowState.Normal;
            this.Activate();
            HideTrayIcon();
        }

        private void SaveBlockedWebsitesToFile()
        {
            string directoryPath = Path.GetDirectoryName(blockedWebsitesFilePath);
            if (!Directory.Exists(directoryPath))
            {
                Directory.CreateDirectory(directoryPath);
            }

            File.WriteAllLines(blockedWebsitesFilePath, blockedWebsites);
        }

        private void LoadBlockedWebsitesFromFile()
        {
            if (File.Exists(blockedWebsitesFilePath))
            {
                blockedWebsites = File.ReadAllLines(blockedWebsitesFilePath).ToList();
                UpdateHostsFile();
                RefreshBlockedWebsitesList();
            }
        }

        private void SaveBlockedAppsToFile()
        {
            string directoryPath = Path.GetDirectoryName(blockedAppsFilePath);
            if (!Directory.Exists(directoryPath))
            {
                Directory.CreateDirectory(directoryPath);
            }

            File.WriteAllLines(blockedAppsFilePath, blockedApps);
        }

        private void LoadBlockedAppsFromFile()
        {
            if (File.Exists(blockedAppsFilePath))
            {
                blockedApps = File.ReadAllLines(blockedAppsFilePath).ToList();
                RefreshBlockedAppsList();
            }
        }

        private void RefreshBlockedWebsitesList()
        {
            Dispatcher.Invoke(() =>
            {
                BlockedWebsitesListBox.ItemsSource = null;
                BlockedWebsitesListBox.ItemsSource = blockedWebsites;
            });
        }

        private void RefreshBlockedAppsList()
        {
            Dispatcher.Invoke(() =>
            {
                BlockedAppsListBox.ItemsSource = null;
                BlockedAppsListBox.ItemsSource = blockedApps;
            });
        }

        // UI Update Timer to check blocking period
        private void StartUIUpdateTimer()
        {
            uiUpdateTimer = new DispatcherTimer();
            uiUpdateTimer.Interval = TimeSpan.FromSeconds(30); // Check every 30 seconds
            uiUpdateTimer.Tick += UIUpdateTimer_Tick;
            uiUpdateTimer.Start();
        }

        //private void UIUpdateTimer_Tick(object sender, EventArgs e)
        //{
        //    if (IsWithinBlockingPeriod())
        //    {
        //        if (this.IsVisible && !this.IsActive)
        //        {
        //            this.Hide();
        //            ShowTrayIcon();
        //        }
        //    }
        //    else
        //    {
        //        if (!this.IsVisible)
        //        {
        //            this.Show();
        //            this.WindowState = WindowState.Normal;
        //            HideTrayIcon();
        //        }
        //    }
        //}
        private void UIUpdateTimer_Tick(object sender, EventArgs e)
        {
            if (IsWithinBlockingPeriod())
            {
                if (this.WindowState == WindowState.Minimized && this.IsVisible)
                {
                    this.Hide();
                    ShowTrayIcon();

                    // Show notification
                    ShowTrayNotification("The application has been minimized to the system tray. Click the tray icon to reopen it.");
                
                }
            }
            else
            {
                if (!this.IsVisible)
                {
                    this.Show();
                    this.WindowState = WindowState.Normal;
                    HideTrayIcon();
                }
            }
        }
        private void ShowTrayNotification(string message)
        {
            TrayIcon.ShowBalloonTip("Website and App Blocker", message, BalloonIcon.Info);
        }



        // Remember to dispose of resources
        protected override void OnClosed(EventArgs e)
        {
            base.OnClosed(e);
            TrayIcon.Dispose();
        }

        private void AboutMenuItem_Click(object sender, RoutedEventArgs e)
        {
            AboutWindow aboutWindow = new AboutWindow();
            aboutWindow.Owner = this;
            aboutWindow.ShowDialog();
        }

        // Flag to allow closing
        private bool canClose = false;

        // Handle the Close event to prevent closing during blocking period
        protected override void OnClosing(System.ComponentModel.CancelEventArgs e)
        {
            if (canClose)
            {
                base.OnClosing(e);
                return;
            }

            if (IsWithinBlockingPeriod())
            {
                e.Cancel = true; // Prevent closing
                MessageBox.Show("The application cannot be closed during study hours.");
            }
            else
            {
                MessageBoxResult result = MessageBox.Show("Are you sure you want to close the application?", "Confirmation", MessageBoxButton.YesNo);
                if (result != MessageBoxResult.Yes)
                {
                    e.Cancel = true;
                }
                else
                {
                    canClose = false;
                    Application.Current.Shutdown();
                }
            }
        }


    }
}
