using System;
using System.Threading;
using System.Threading.Tasks;
using System.Windows;
using System.Windows.Threading;

namespace WebsiteAndAppBlocker
{
    public partial class ChallengeWindow : Window
    {
        private string challengeText;
        private int correctAnswer;
        private CancellationTokenSource cts;
        private int timeLimit = 60; // Time limit in seconds
        public bool IsChallengeCompleted { get; private set; }

        public ChallengeWindow()
        {
            InitializeComponent();
            GenerateChallenge();
        }

        private async void GenerateChallenge()
        {
            // Generate random string
            challengeText = GetRandomString(20);
            ChallengeTextBlock.Text = $"Memorize the following text (you have 10 seconds):\n\n{challengeText}";

            // Wait for 10 seconds
            await Task.Delay(10000);

            // Clear the text
            ChallengeTextBlock.Text = "Now, please type the text you just saw.";

            // Generate math problem
            GenerateMathProblem();

            // Start the countdown after the initial 10 seconds
            StartCountdown();
        }

        private void GenerateMathProblem()
        {
            Random random = new Random();
            int num1 = random.Next(10, 99);
            int num2 = random.Next(10, 99);
            int num3 = random.Next(10, 99);
            correctAnswer = (num1 * num2) - num3;
            MathProblemTextBlock.Text = $"Solve the following math problem: ({num1} × {num2}) - {num3} = ?";
        }

        private string GetRandomString(int length)
        {
            const string chars = "ABCDEFGHIJKLMNOPQRSTUVWXYZabcdefghijklmnopqrstuvwxyz0123456789!@#$%^&*()-_=+[]{}|;:',.<>?/";
            Random random = new Random();
            char[] result = new char[length];
            for (int i = 0; i < length; i++)
            {
                result[i] = chars[random.Next(chars.Length)];
            }
            return new string(result);
        }

        private void StartCountdown()
        {
            cts = new CancellationTokenSource();
            CancellationToken token = cts.Token;
            Task.Run(async () =>
            {
                for (int i = timeLimit; i >= 0; i--)
                {
                    Dispatcher.Invoke(() =>
                    {
                        TimerTextBlock.Text = $"Time remaining: {i} seconds";
                    });
                    await Task.Delay(1000);
                    if (token.IsCancellationRequested)
                        break;
                }
                if (!token.IsCancellationRequested)
                {
                    Dispatcher.Invoke(() =>
                    {
                        MessageBox.Show("Time is up! Challenge failed.");
                        this.Close();
                    });
                }
            }, token);
        }

        private void SubmitButton_Click(object sender, RoutedEventArgs e)
        {
            // Check typed text
            string typedText = TypedTextBox.Text.Trim();
            if (typedText != challengeText)
            {
                MessageBox.Show("Incorrect typed text. Please try again.");
                TypedTextBox.Clear();
                return;
            }

            // Check math problem answer
            if (!int.TryParse(MathAnswerTextBox.Text.Trim(), out int userAnswer) || userAnswer != correctAnswer)
            {
                MessageBox.Show("Incorrect math answer. Please try again.");
                MathAnswerTextBox.Clear();
                return;
            }

            IsChallengeCompleted = true;
            cts.Cancel(); // Stop the timer
            this.Close();
        }

        protected override void OnClosed(EventArgs e)
        {
            base.OnClosed(e);
            cts?.Cancel();
        }
    }
}
