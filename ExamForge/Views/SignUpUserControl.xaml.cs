using System;
using System.Text.RegularExpressions;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Media;
using ExamForge.Services;

namespace ExamForge.Views
{
    public partial class SignUpUserControl : UserControl
    {
        public event EventHandler? NavigateToLogin;
        public event EventHandler? SignUpSuccessful;

        private readonly SupabaseAuthService _authService;

        public SignUpUserControl()
        {
            InitializeComponent();
            _authService = new SupabaseAuthService();
            Loaded += SignUpUserControl_Loaded;
        }

        private async void SignUpUserControl_Loaded(object sender, RoutedEventArgs e)
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

        private async void SignUp_Click(object sender, RoutedEventArgs e)
        {
            var username = UsernameTextBox.Text.Trim();
            var email = EmailTextBox.Text.Trim();
            var password = PasswordBox.Password;
            var confirmPassword = ConfirmPasswordBox.Password;

            // Validation
            if (string.IsNullOrWhiteSpace(username))
            {
                ShowMessage("Please enter a username", isError: true);
                UsernameTextBox.Focus();
                return;
            }

            if (username.Length < 3)
            {
                ShowMessage("Username must be at least 3 characters", isError: true);
                UsernameTextBox.Focus();
                return;
            }

            if (string.IsNullOrWhiteSpace(email))
            {
                ShowMessage("Please enter an email address", isError: true);
                EmailTextBox.Focus();
                return;
            }

            if (!IsValidEmail(email))
            {
                ShowMessage("Please enter a valid email address", isError: true);
                EmailTextBox.Focus();
                return;
            }

            if (string.IsNullOrWhiteSpace(password))
            {
                ShowMessage("Please enter a password", isError: true);
                PasswordBox.Focus();
                return;
            }

            if (password.Length < 6)
            {
                ShowMessage("Password must be at least 6 characters", isError: true);
                PasswordBox.Focus();
                return;
            }

            if (password != confirmPassword)
            {
                ShowMessage("Passwords do not match", isError: true);
                ConfirmPasswordBox.Focus();
                return;
            }

            // Show loading
            SetLoading(true);
            MessageText.Visibility = Visibility.Collapsed;

            try
            {
                var (success, message) = await _authService.SignUpAsync(email, password, username);

                if (success)
                {
                    ShowMessage("Account created successfully! Redirecting...", isError: false);
                    
                    // Store auth service globally
                    App.SupabaseAuth = _authService;
                    
                    // Initialize Firestore with user ID
                    App.FirestoreService = new FirestoreService(_authService.UserId!);

                    // Configure publishing service for this user
                    if (App.FirestoreService != null)
                    {
                        App.ConfigurePublishingService(App.FirestoreService);
                    }

                    // Wait a moment then trigger success
                    await System.Threading.Tasks.Task.Delay(1500);
                    SignUpSuccessful?.Invoke(this, EventArgs.Empty);
                }
                else
                {
                    ShowMessage(message, isError: true);
                }
            }
            catch (Exception ex)
            {
                ShowMessage($"Sign-up failed: {ex.Message}", isError: true);
            }
            finally
            {
                SetLoading(false);
            }
        }

        private void Login_Click(object sender, RoutedEventArgs e)
        {
            NavigateToLogin?.Invoke(this, EventArgs.Empty);
        }

        private bool IsValidEmail(string email)
        {
            try
            {
                var emailRegex = new Regex(@"^[^@\s]+@[^@\s]+\.[^@\s]+$");
                return emailRegex.IsMatch(email);
            }
            catch
            {
                return false;
            }
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
            SignUpButton.IsEnabled = !isLoading;
            UsernameTextBox.IsEnabled = !isLoading;
            EmailTextBox.IsEnabled = !isLoading;
            PasswordBox.IsEnabled = !isLoading;
            ConfirmPasswordBox.IsEnabled = !isLoading;
            LoadingBar.Visibility = isLoading ? Visibility.Visible : Visibility.Collapsed;
        }
    }
}
