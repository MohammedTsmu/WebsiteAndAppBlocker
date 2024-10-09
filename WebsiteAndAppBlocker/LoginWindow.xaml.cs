using System;
using System.Windows;
using System.Windows.Input;

namespace WebsiteAndAppBlocker
{
    public partial class LoginWindow : Window
    {
        public bool IsAuthenticated { get; private set; }

        public LoginWindow()
        {
            InitializeComponent();
            PasswordBox.Focus();
        }


        private void LoginButton_Click(object sender, RoutedEventArgs e)
        {
            string enteredPassword = PasswordBox.Password;

            if (AuthenticateUser(enteredPassword))
            {
                IsAuthenticated = true;
                this.Close();
            }
            else
            {
                MessageBox.Show("Incorrect password. Access denied.");
                PasswordBox.Clear();
                PasswordBox.Focus();
            }
        }

        private bool AuthenticateUser(string password)
        {
            string passwordFilePath = GetPasswordFilePath();

            if (!System.IO.File.Exists(passwordFilePath))
            {
                // No password is set; allow access
                //MessageBox.Show("No password file found. Allowing access.");
                return true;
            }

            //MessageBox.Show("Password file found.");

            if (string.IsNullOrEmpty(password))
            {
                //MessageBox.Show("Password is empty. Denying access.");
                return false;
            }

            string storedHashedPassword = System.IO.File.ReadAllText(passwordFilePath);
            string enteredHashedPassword = HashPassword(password);

            if (storedHashedPassword == enteredHashedPassword)
            {
                //MessageBox.Show("Passwords match. Access granted.");
                return true;
            }
            else
            {
                //MessageBox.Show("Passwords do not match. Access denied.");
                return false;
            }
        }


        // Optional: Allow pressing Enter to submit
        private void PasswordBox_KeyDown(object sender, KeyEventArgs e)
        {
            if (e.Key == Key.Enter)
            {
                LoginButton_Click(this, new RoutedEventArgs());
            }
        }

        private string GetPasswordFilePath()
        {
            string appDataPath = Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData);
            string appFolder = System.IO.Path.Combine(appDataPath, "WebsiteAndAppBlocker");
            if (!System.IO.Directory.Exists(appFolder))
            {
                System.IO.Directory.CreateDirectory(appFolder);
            }
            return System.IO.Path.Combine(appFolder, "password.txt");
        }

        private string HashPassword(string password)
        {
            using (var sha256 = System.Security.Cryptography.SHA256.Create())
            {
                byte[] bytes = sha256.ComputeHash(System.Text.Encoding.UTF8.GetBytes(password));
                return Convert.ToBase64String(bytes);
            }
        }
    }
}
