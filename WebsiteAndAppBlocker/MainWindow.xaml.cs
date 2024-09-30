using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Security.Cryptography;
using System.Security.Principal;
using System.Text;
using System.Timers;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Threading; // For DispatcherTimer
using Microsoft.Win32;
using System.Threading.Tasks; // For async/await
using Hardcodet.Wpf.TaskbarNotification; // Namespace for TaskbarIcon




namespace WebsiteAndAppBlocker
{
    public partial class MainWindow : Window
    {
        // Variables and collections
        private Timer processMonitorTimer;
        private List<string> blockedApps = new List<string>();
        private List<string> blockedWebsites = new List<string>();
        private DateTime lastCheckTime;
        private string hashedPassword;

        private string blockedWebsitesFilePath = Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData), "WebsiteAndAppBlocker", "blocked_websites.txt");
        private string blockedAppsFilePath = Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData), "WebsiteAndAppBlocker", "blocked_apps.txt");
        private Dictionary<DateTime, int> unblockAttempts = new Dictionary<DateTime, int>();
        private int maxAttemptsPerHour = 2; // Maximum attempts allowed per hour



        // Blocking period (8 AM to 6 PM)
        private TimeSpan blockingStartTime = new TimeSpan(8, 0, 0);
        private TimeSpan blockingEndTime = new TimeSpan(18, 0, 0);

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
            lastCheckTime = DateTime.Now;

            // Start UI update timer
            StartUIUpdateTimer();

            // Handle the StateChanged event
            this.StateChanged += MainWindow_StateChanged;

            // Handle the Closing event
            this.Closing += Window_Closing;

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

            if (IsWithinBlockingPeriod() && !AuthenticateUser())
                return;

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

            if (IsWithinBlockingPeriod() && !AuthenticateUser())
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

            if (IsWithinBlockingPeriod() && !AuthenticateUser())
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
            if (IsWithinBlockingPeriod() && !AuthenticateUser())
                return;

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
            if (IsWithinBlockingPeriod() && !AuthenticateUser())
                return;

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
            if (IsWithinBlockingPeriod() && !AuthenticateUser())
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
            string password = PasswordBox.Password;
            if (!string.IsNullOrEmpty(password))
            {
                hashedPassword = ComputeSha256Hash(password);
                MessageBox.Show("Password set");
            }
        }

        // Helper methods
        private bool IsWithinBlockingPeriod()
        {
            TimeSpan now = DateTime.Now.TimeOfDay;
            return (now >= blockingStartTime) && (now <= blockingEndTime);
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

        // Handle the Close event to minimize to tray instead of closing
        private void Window_Closing(object sender, System.ComponentModel.CancelEventArgs e)
        {
            // Prevent closing and instead minimize to tray
            e.Cancel = true;
            this.Hide();
            ShowTrayIcon();
        }

        // Tray icon menu item: Open
        private void TrayMenu_Open_Click(object sender, RoutedEventArgs e)
        {
            this.Show();
            this.WindowState = WindowState.Normal;
            this.Activate();
            HideTrayIcon();
        }

        // Tray icon menu item: Exit
        private void TrayMenu_Exit_Click(object sender, RoutedEventArgs e)
        {
            // Optionally, prompt for password before exiting
            if (IsWithinBlockingPeriod())
            {
                MessageBox.Show("The application cannot be closed during study hours.");
                return;
            }

            // Clean up tray icon
            TrayIcon.Dispose();

            // Close the application
            Application.Current.Shutdown();
        }

        private void TrayIcon_TrayMouseDoubleClick(object sender, RoutedEventArgs e)
        {
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



        // Modify the UIUpdateTimer_Tick method
        private void UIUpdateTimer_Tick(object sender, EventArgs e)
        {
            if (IsWithinBlockingPeriod())
            {
                if (this.IsVisible)
                {
                    this.Hide();
                    ShowTrayIcon();
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

        // Remember to dispose of resources
        protected override void OnClosed(EventArgs e)
        {
            base.OnClosed(e);
            TrayIcon.Dispose();
        }

        // Authenticate user
        private bool AuthenticateUser()
        {
            if (!string.IsNullOrEmpty(hashedPassword))
            {
                string inputPassword = Microsoft.VisualBasic.Interaction.InputBox("Enter Password:", "Password Required", "", -1, -1);
                if (ComputeSha256Hash(inputPassword) == hashedPassword)
                {
                    return true;
                }
                else
                {
                    MessageBox.Show("Incorrect Password");
                    return false;
                }
            }
            else
            {
                // No password set
                return true;
            }
        }

        // Password hashing method
        private string ComputeSha256Hash(string rawData)
        {
            using (SHA256 sha256Hash = SHA256.Create())
            {
                // Compute the hash
                byte[] bytes = sha256Hash.ComputeHash(Encoding.UTF8.GetBytes(rawData));

                // Convert byte array to a string
                StringBuilder builder = new StringBuilder();
                foreach (var b in bytes)
                {
                    builder.Append(b.ToString("x2"));
                }

                return builder.ToString();
            }
        }

        // Prevent closing without password
        protected override void OnClosing(System.ComponentModel.CancelEventArgs e)
        {
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
            }
        }
    }
}
