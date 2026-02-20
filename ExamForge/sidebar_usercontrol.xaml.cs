using System;
using System.Threading.Tasks;
using System.Windows;
using System.Windows.Controls;

namespace ExamForge
{
    /// <summary>
    /// Interaction logic for sidebar_usercontrol.xaml
    /// </summary>
    public partial class sidebar_usercontrol : UserControl
    {
        public sidebar_usercontrol()
        {
            InitializeComponent();
            Loaded += Sidebar_Loaded;
        }

        private async void Sidebar_Loaded(object sender, RoutedEventArgs e)
        {
            // Update username greeting from Supabase user data
            try
            {
                if (App.SupabaseAuth != null && App.SupabaseAuth.CurrentUser != null)
                {
                    // Try to get username from user metadata first
                    var username = App.SupabaseAuth.CurrentUser.UserMetadata?.TryGetValue("username", out var usernameObj) == true 
                        ? usernameObj?.ToString() 
                        : null;
                    
                    // If no username in metadata, try to get from examforge_users table
                    if (string.IsNullOrEmpty(username) && !string.IsNullOrEmpty(App.SupabaseAuth.UserId))
                    {
                        var authService = new ExamForge.Services.SupabaseAuthService();
                        await authService.InitializeAsync();
                        // Username will be in the examforge_users table - we'll need to add a method to get it
                        username = await GetUsernameFromSupabaseAsync(App.SupabaseAuth.UserId);
                    }
                    
                    // Fallback to email username if still not found
                    if (string.IsNullOrEmpty(username) && !string.IsNullOrEmpty(App.SupabaseAuth.Email))
                    {
                        username = App.SupabaseAuth.Email.Split('@')[0];
                    }
                    
                    if (!string.IsNullOrEmpty(username))
                    {
                        UsernameGreeting.Text = $"Hello, {username}";
                    }
                }
            }
            catch (Exception ex)
            {
                System.Diagnostics.Debug.WriteLine($"Error loading username: {ex.Message}");
                // Fallback to default
                UsernameGreeting.Text = "Hello, User";
            }
        }
        
        private async Task<string> GetUsernameFromSupabaseAsync(string userId)
        {
            try
            {
                var authService = new ExamForge.Services.SupabaseAuthService();
                await authService.InitializeAsync();
                // This will use the GetUsernameByUserId method we'll add to SupabaseAuthService
                return await authService.GetUsernameByUserIdAsync(userId);
            }
            catch
            {
                return string.Empty;
            }
        }

        private void Button_Click(object sender, RoutedEventArgs e)
        {
            // Dashboard button clicked
            Window mainWindow = Window.GetWindow(this);
            if (mainWindow is MainWindow main)
            {
                if (main.FindName("MainContentHost") is ContentControl contentHost)
                {
                    contentHost.Content = new dashboard_usercontrol();
                }
            }
        }

        private void Create_Button_Click(object sender, RoutedEventArgs e)
        {
            // Create button clicked - load structure builder with datagrid visible
            Window mainWindow = Window.GetWindow(this);
            if (mainWindow is MainWindow main)
            {
                var structureBuilder = new structure_builder_usercontrol();
                
                // ✅ FIX: Store the structure builder reference in MainWindow
                main.SetStructureBuilder(structureBuilder);
                
                if (main.FindName("MainContentHost") is ContentControl contentHost)
                {
                    contentHost.Content = structureBuilder;
                    
                    // ✅ FIX: Use Dispatcher to avoid freeze
                    // Automatically click the Exam Structure button to show datagrid
                    Dispatcher.BeginInvoke(new System.Action(() =>
                    {
                        structureBuilder.ShowExamStructure();
                    }), System.Windows.Threading.DispatcherPriority.Loaded);
                }
            }
        }

        private void Published_Exams_Button_Click(object sender, RoutedEventArgs e)
        {
            // Published Exams button clicked - load published exams usercontrol
            Window mainWindow = Window.GetWindow(this);
            if (mainWindow is MainWindow main)
            {
                if (main.FindName("MainContentHost") is ContentControl contentHost)
                {
                    contentHost.Content = new published_exams_usercontrol();
                }
            }
        }

        private void Analytics_Button_Click(object sender, RoutedEventArgs e)
        {
            // Analytics button clicked - load student analytics usercontrol
            Window mainWindow = Window.GetWindow(this);
            if (mainWindow is MainWindow main)
            {
                if (main.FindName("MainContentHost") is ContentControl contentHost)
                {
                    contentHost.Content = new student_analytics_usercontrol();
                }
            }
        }

        /// <summary>
        /// Public method to navigate to Published Exams tab from external code
        /// </summary>
        public void NavigateToPublishedExams()
        {
            Published_Exams_Button_Click(this, new RoutedEventArgs());
        }

        private async void logout_button_Click(object sender, RoutedEventArgs e)
        {
            var result = MessageBox.Show("Are you sure you want to log out?", "Logout", MessageBoxButton.YesNo, MessageBoxImage.Question);
            if (result != MessageBoxResult.Yes) return;

            try
            {
                if (App.SupabaseAuth != null)
                {
                    await App.SupabaseAuth.SignOutAsync();
                }
            }
            catch (Exception ex)
            {
                System.Diagnostics.Debug.WriteLine($"Logout error: {ex.Message}");
            }
            finally
            {
                App.ResetServices();

                var authWindow = new ExamForge.Views.AuthWindow();
                authWindow.Show();

                Window.GetWindow(this)?.Close();
            }
        }
    }
}
