using System;
using System.Windows;
using ExamForge.Services;
using System.Threading;

namespace ExamForge.Views
{
    public partial class LoginWindow : Window
    {
        private readonly BackendAuthService _authService;

        public LoginWindow()
        {
            InitializeComponent();
            _authService = new BackendAuthService();
        }

        private async void GoogleSignIn_Click(object sender, RoutedEventArgs e)
        {
            try
            {
                // Disable button and show loading
                GoogleSignInButton.IsEnabled = false;
                LoadingBar.Visibility = Visibility.Visible;
                StatusText.Visibility = Visibility.Visible;
                StatusText.Foreground = (System.Windows.Media.Brush)FindResource("TextSecondaryBrush");
                StatusText.Text = "Opening Google Sign-In...";

                // Attempt sign-in via backend OAuth middleman
                var success = await _authService.SignInWithGoogleAsync(CancellationToken.None);

                if (success && _authService.UserId != null)
                {
                    StatusText.Text = "Sign-in successful! Loading your workspace...";

                    // Store backend auth context globally
                    App.AuthService = null; // Legacy direct Google flow disabled when backend is used
                    App.BackendAuthService = _authService;
                    App.BackendJwt = _authService.JwtToken;
                    App.CurrentUserId = _authService.UserId;
                    App.CurrentUserEmail = _authService.Email;
                    App.CurrentUserName = _authService.DisplayName;

                    // Initialize Firestore with user's ID (kept for now; replace with backend-proxied data if needed)
                    App.FirestoreService = new FirestoreService(_authService.UserId);

                    // Small delay for UX
                    await System.Threading.Tasks.Task.Delay(800);

                    // Open main window
                    var mainWindow = new MainWindow();
                    mainWindow.Show();

                    // Close login window
                    this.Close();
                }
                else
                {
                    // Sign-in failed
                    StatusText.Foreground = (System.Windows.Media.Brush)FindResource("ErrorBrush");
                    StatusText.Text = "Sign-in failed. Please try again or check your internet connection.";
                    GoogleSignInButton.IsEnabled = true;
                    LoadingBar.Visibility = Visibility.Collapsed;
                }
            }
            catch (Exception ex)
            {
                StatusText.Visibility = Visibility.Visible;
                StatusText.Foreground = (System.Windows.Media.Brush)FindResource("ErrorBrush");
                StatusText.Text = $"Error: {ex.Message}";
                GoogleSignInButton.IsEnabled = true;
                LoadingBar.Visibility = Visibility.Collapsed;

                System.Diagnostics.Debug.WriteLine($"Login error: {ex}");
            }
        }
    }
}
