using System;
using System.Windows;
using System.Windows.Controls;
using ExamForge.Services;

namespace ExamForge.Views
{
    public partial class LoginUserControl : UserControl
    {
        public event EventHandler? NavigateToSignUp;
        public event EventHandler? NavigateToForgotPassword;
        public event EventHandler? LoginSuccessful;

        private readonly SupabaseAuthService _authService;

        public LoginUserControl()
        {
            InitializeComponent();
            _authService = new SupabaseAuthService();
            Loaded += LoginUserControl_Loaded;
        }

        private async void LoginUserControl_Loaded(object sender, RoutedEventArgs e)
        {
            try
            {
                await _authService.InitializeAsync();
            }
            catch (Exception ex)
            {
                ShowError($"Failed to initialize authentication: {ex.Message}");
            }
        }

        private async void Login_Click(object sender, RoutedEventArgs e)
        {
            var username = UsernameTextBox.Text.Trim();
            var password = PasswordBox.Password;

            // Validation
            if (string.IsNullOrWhiteSpace(username))
            {
                ShowError("Please enter your username or email");
                UsernameTextBox.Focus();
                return;
            }

            if (string.IsNullOrWhiteSpace(password))
            {
                ShowError("Please enter your password");
                PasswordBox.Focus();
                return;
            }

            // Show loading
            SetLoading(true);
            ErrorText.Visibility = Visibility.Collapsed;

            try
            {
                var (success, message) = await _authService.SignInAsync(username, password);

                if (success)
                {
                    // Store auth service globally
                    App.SupabaseAuth = _authService;

                    // Initialize Firestore with user ID if available
                    if (!string.IsNullOrEmpty(_authService.UserId))
                    {
                        App.FirestoreService = new FirestoreService(_authService.UserId);
                        // Configure publishing service if present
                        try
                        {
                            App.ConfigurePublishingService(App.FirestoreService);
                        }
                        catch { }
                    }

                    // Trigger success event to open main window
                    LoginSuccessful?.Invoke(this, EventArgs.Empty);
                }
                else
                {
                    ShowError(message);
                }
            }
            catch (Exception ex)
            {
                ShowError($"Login failed: {ex.Message}");
            }
            finally
            {
                SetLoading(false);
            }
        }

        private void SignUp_Click(object sender, RoutedEventArgs e)
        {
            NavigateToSignUp?.Invoke(this, EventArgs.Empty);
        }

        private void ForgotPassword_Click(object sender, RoutedEventArgs e)
        {
            NavigateToForgotPassword?.Invoke(this, EventArgs.Empty);
        }

        private void ShowError(string message)
        {
            ErrorText.Text = message;
            ErrorText.Visibility = Visibility.Visible;
        }

        private void SetLoading(bool isLoading)
        {
            LoginButton.IsEnabled = !isLoading;
            UsernameTextBox.IsEnabled = !isLoading;
            PasswordBox.IsEnabled = !isLoading;
            LoadingBar.Visibility = isLoading ? Visibility.Visible : Visibility.Collapsed;
        }
    }
}
