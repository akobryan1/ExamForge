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
        }

        private void Button_Click(object sender, RoutedEventArgs e)
        {
            // Dashboard button clicked
            // TODO: Navigate to dashboard
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
    }
}
