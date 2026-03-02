using System;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Media;
using ExamForge.Services;

namespace ExamForge.Views
{
    public partial class AccountRecoveryUserControl : UserControl
    {
        public event EventHandler? NavigateToLogin;

        private readonly SupabaseAuthService _authService;

        public AccountRecoveryUserControl()
        {
            InitializeComponent();
            _authService = new SupabaseAuthService();
            Loaded += AccountRecoveryUserControl_Loaded;
        }

        private async void AccountRecoveryUserControl_Loaded(object sender, RoutedEventArgs e)
        {
            try
            {
                await _authService.InitializeAsync();
            }
            catch (Exception ex)
            {
                ShowMessage($"Failed to initialize authentication: {ex.Message}", isError: true);
            }
        }

        private async void ResetPassword_Click(object sender, RoutedEventArgs e)
        {
            var username = UsernameTextBox.Text.Trim();
            var email = EmailTextBox.Text.Trim();

            // Validation
            if (string.IsNullOrWhiteSpace(username))
            {
                ShowMessage("Please enter your username", isError: true);
                UsernameTextBox.Focus();
                return;
            }

            if (string.IsNullOrWhiteSpace(email))
            {
                ShowMessage("Please enter your email address", isError: true);
                EmailTextBox.Focus();
                return;
            }

            // Show loading
            SetLoading(true);
            MessageText.Visibility = Visibility.Collapsed;

            try
            {
                var (success, message) = await _authService.ResetPasswordAsync(username, email);

                if (success)
                {
                    ShowMessage(message, isError: false);
                    
                    // Clear form
                    UsernameTextBox.Clear();
                    EmailTextBox.Clear();

                    // Wait then navigate back
                    await System.Threading.Tasks.Task.Delay(3000);
                    NavigateToLogin?.Invoke(this, EventArgs.Empty);
                }
                else
                {
                    ShowMessage(message, isError: true);
                }
            }
            catch (Exception ex)
            {
                ShowMessage($"Password reset failed: {ex.Message}", isError: true);
            }
            finally
            {
                SetLoading(false);
            }
        }

        private void BackToLogin_Click(object sender, RoutedEventArgs e)
        {
            NavigateToLogin?.Invoke(this, EventArgs.Empty);
        }

        private void ShowMessage(string message, bool isError)
        {
            MessageText.Text = message;
            MessageText.Foreground = isError 
                ? (Brush)FindResource("ErrorRed") 
                : (Brush)FindResource("SuccessGreen");
            MessageText.Visibility = Visibility.Visible;
        }

        private void SetLoading(bool isLoading)
        {
            ResetButton.IsEnabled = !isLoading;
            UsernameTextBox.IsEnabled = !isLoading;
            EmailTextBox.IsEnabled = !isLoading;
            LoadingBar.Visibility = isLoading ? Visibility.Visible : Visibility.Collapsed;
        }
    }
}
