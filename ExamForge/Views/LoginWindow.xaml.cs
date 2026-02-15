using System;
using System.Windows;
using ExamForge.Services;

namespace ExamForge.Views
{
    public partial class LoginWindow : Window
    {
        private readonly FirebaseAuthService _authService;

        public LoginWindow()
        {
            InitializeComponent();
            _authService = new FirebaseAuthService();
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

                // Attempt sign-in
                var success = await _authService.SignInWithGoogleAsync();

                if (success && _authService.UserId != null)
                {
                    StatusText.Text = "Sign-in successful! Loading your workspace...";

                    // Store auth service globally
                    App.AuthService = _authService;

                    // Initialize Firestore with user's ID (data isolation)
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
