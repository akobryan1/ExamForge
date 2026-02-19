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

        private void Sidebar_Loaded(object sender, RoutedEventArgs e)
        {
            // Update username greeting
            if (App.SupabaseAuth != null && !string.IsNullOrEmpty(App.SupabaseAuth.Email))
            {
                var username = App.SupabaseAuth.Email.Split('@')[0];
                UsernameGreeting.Text = $"Hello, {username}";
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
