using System;
using System.Collections.Generic;
using System.Text.RegularExpressions;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;
using System.Windows.Threading;
using ExamForge.Models; // Add this line if missing

namespace ExamForge
{
    /// <summary>
    /// Interaction logic for structure_builder_usercontrol.xaml
    /// </summary>
    public partial class structure_builder_usercontrol : UserControl
    {
        private DispatcherTimer itemNumberTimer;
        private int currentItemCount = 0;

        // Auto-save storage for each tab
        private object savedDataGridState = null;
        private object savedTimingState = null;
        private object savedAntiCheatState = null;
        private object savedLoginConfigState = null;

        // Current active tab
        private string currentTab = "";

        public structure_builder_usercontrol()
        {
            InitializeComponent();
            InitializeTimer();
        }

        private void InitializeTimer()
        {
            itemNumberTimer = new DispatcherTimer
            {
                Interval = TimeSpan.FromMilliseconds(1500)
            };
            itemNumberTimer.Tick += ItemNumberTimer_Tick;
        }

        private void ItemNumber_PreviewTextInput(object sender, TextCompositionEventArgs e)
        {
            // Only allow numeric input
            Regex regex = new Regex("[^0-9]+");
            e.Handled = regex.IsMatch(e.Text);
        }

        private void ItemNumber_TextChanged(object sender, TextChangedEventArgs e)
        {
            itemNumberTimer.Stop();

            if (!string.IsNullOrWhiteSpace(item_number_textbox.Text))
            {
                itemNumberTimer.Start();
            }
        }

        private void ItemNumberTimer_Tick(object? sender, EventArgs e)
        {
            itemNumberTimer.Stop();

            if (int.TryParse(item_number_textbox.Text, out int newItemCount))
            {
                if (newItemCount == 0)
                {
                    currentItemCount = 0;
                    if (ContentHost.Content is datagrid_usercontrol dg)
                        dg.ClearDataGrid();
                }
                else
                {
                    currentItemCount = newItemCount;

                    // NEW: if grid is open, immediately push total items so Start/End lists populate/filter
                    if (ContentHost.Content is datagrid_usercontrol dg)
                        dg.SetTotalItems(currentItemCount);
                }
            }
        }

        private void ExamStructure_Button_Click(object sender, RoutedEventArgs e)
        {
            // Auto-save current tab before switching
            SaveCurrentTabState();

            // Set datagrid usercontrol as content
            var datagridControl = new datagrid_usercontrol();
            
            // Set total items if available
            if (currentItemCount > 0)
            {
                datagridControl.SetTotalItems(currentItemCount);
            }

            // Restore saved state if exists
            if (savedDataGridState != null)
            {
                datagridControl.RestoreState(savedDataGridState);
            }

            ContentHost.Content = datagridControl;
            currentTab = "ExamStructure";
        }

        private void Timing_Button_Click(object sender, RoutedEventArgs e)
        {
            // Auto-save current tab before switching
            SaveCurrentTabState();

            // Set timing usercontrol as content
            var timingControl = new timing_usercontrol();

            // Restore saved state if exists
            if (savedTimingState != null)
            {
                timingControl.RestoreState(savedTimingState);
            }

            ContentHost.Content = timingControl;
            currentTab = "Timing";
        }

        private void AntiCheat_Button_Click(object sender, RoutedEventArgs e)
        {
            // Auto-save current tab before switching
            SaveCurrentTabState();

            // Set anti-cheat usercontrol as content
            var antiCheatControl = new anti_cheat_usercontrol();

            // Restore saved state if exists
            if (savedAntiCheatState != null)
            {
                antiCheatControl.RestoreState(savedAntiCheatState);
            }

            ContentHost.Content = antiCheatControl;
            currentTab = "AntiCheat";
        }

        private void LoginConfiguration_Button_Click(object sender, RoutedEventArgs e)
        {
            // Auto-save current tab before switching
            SaveCurrentTabState();

            // Set examinee login usercontrol as content
            var loginConfigControl = new examinee_login_usercontrol();

            // Restore saved state if exists
            if (savedLoginConfigState != null)
            {
                loginConfigControl.RestoreState(savedLoginConfigState);
            }

            ContentHost.Content = loginConfigControl;
            currentTab = "LoginConfig";
        }

        private void SaveCurrentTabState()
        {
            if (ContentHost.Content == null) return;

            switch (currentTab)
            {
                case "ExamStructure":
                    if (ContentHost.Content is datagrid_usercontrol datagridControl)
                    {
                        savedDataGridState = datagridControl.SaveState();
                    }
                    break;
                case "Timing":
                    if (ContentHost.Content is timing_usercontrol timingControl)
                    {
                        savedTimingState = timingControl.SaveState();
                    }
                    break;
                case "AntiCheat":
                    if (ContentHost.Content is anti_cheat_usercontrol antiCheatControl)
                    {
                        savedAntiCheatState = antiCheatControl.SaveState();
                    }
                    break;
                case "LoginConfig":
                    if (ContentHost.Content is examinee_login_usercontrol loginConfigControl)
                    {
                        savedLoginConfigState = loginConfigControl.SaveState();
                    }
                    break;
            }
        }

        private void Reset_Button_Click(object sender, RoutedEventArgs e)
        {
            // Show confirmation dialog
            MessageBoxResult result = MessageBox.Show(
                "Are you sure you want to reset all configuration? This will clear all fields and cannot be undone.",
                "Confirm Reset",
                MessageBoxButton.YesNo,
                MessageBoxImage.Warning);

            if (result == MessageBoxResult.Yes)
            {
                // Clear all textboxes
                exam_name_textbox.Clear();
                subject_name_textbox.Clear();
                item_number_textbox.Clear();

                // Reset item count
                currentItemCount = 0;

                // Clear all saved states
                savedDataGridState = null;
                savedTimingState = null;
                savedAntiCheatState = null;
                savedLoginConfigState = null;

                // Clear content host
                ContentHost.Content = null;
                currentTab = "";

                MessageBox.Show("All configuration has been reset.", "Reset Complete", MessageBoxButton.OK, MessageBoxImage.Information);
            }
        }

        private void Proceed_Button_Click(object sender, RoutedEventArgs e)
        {
            // Validate required fields
            if (string.IsNullOrWhiteSpace(exam_name_textbox.Text))
            {
                MessageBox.Show("Please enter an Exam Name before proceeding.", "Validation Error", MessageBoxButton.OK, MessageBoxImage.Warning);
                exam_name_textbox.Focus();
                return;
            }

            if (string.IsNullOrWhiteSpace(subject_name_textbox.Text))
            {
                MessageBox.Show("Please enter a Subject Name before proceeding.", "Validation Error", MessageBoxButton.OK, MessageBoxImage.Warning);
                subject_name_textbox.Focus();
                return;
            }

            if (string.IsNullOrWhiteSpace(item_number_textbox.Text) || currentItemCount == 0)
            {
                MessageBox.Show("Please enter a valid Number of Items (greater than 0) before proceeding.", "Validation Error", MessageBoxButton.OK, MessageBoxImage.Warning);
                item_number_textbox.Focus();
                return;
            }

            // Save current tab state before proceeding
            SaveCurrentTabState();

            // Validate DataGrid structure if configured
            if (savedDataGridState is DataGridState dataGridState)
            {
                var tempDataGrid = new datagrid_usercontrol();
                tempDataGrid.SetTotalItems(currentItemCount);
                tempDataGrid.RestoreState(savedDataGridState);
                
                if (!tempDataGrid.ValidateStructure(out string errorMessage))
                {
                    MessageBox.Show($"Exam Structure validation failed:\n\n{errorMessage}", 
                        "Validation Error", 
                        MessageBoxButton.OK, 
                        MessageBoxImage.Warning);
                    
                    // Switch to Exam Structure tab to show the error
                    ExamStructure_Button_Click(sender, e);
                    return;
                }
            }

            // Create configuration object
            var configuration = new ExamConfiguration
            {
                ExamName = exam_name_textbox.Text,
                SubjectName = subject_name_textbox.Text,
                NumberOfItems = currentItemCount,
                DataGridState = savedDataGridState,
                TimingState = savedTimingState,
                AntiCheatState = savedAntiCheatState,
                LoginConfigState = savedLoginConfigState
            };

            // Show configuration summary window
            var summaryWindow = new ConfigurationSummaryWindow(configuration)
            {
                Owner = Window.GetWindow(this)
            };
            summaryWindow.ShowDialog();

            // Only proceed if user confirmed
            if (summaryWindow.ProceedConfirmed)
            {
                LoadContentBuilder(configuration);
            }
        }

        private void LoadContentBuilder(ExamConfiguration configuration)
        {
            // Find the main window and load content builder
            Window mainWindow = Window.GetWindow(this);
            if (mainWindow is MainWindow main)
            {
                // ✅ IMPORTANT: Store reference to structure_builder BEFORE navigating
                main.SetStructureBuilder(this);
                
                var contentBuilder = new content_builder();
                contentBuilder.LoadConfiguration(configuration);
                
                // Replace current content with content builder
                // Assuming MainWindow has a ContentControl named MainContentHost
                if (main.FindName("MainContentHost") is ContentControl contentHost)
                {
                    contentHost.Content = contentBuilder;
                }
            }
        }

        private void TextBox_GotFocus(object sender, RoutedEventArgs e)
        {
            // Placeholder visibility handled by binding
        }

        private void TextBox_LostFocus(object sender, RoutedEventArgs e)
        {
            // Placeholder visibility handled by binding
        }

        private void TextBox_TextChanged(object sender, TextChangedEventArgs e)
        {
            // Placeholder visibility handled by binding
        }

        // Add this public method to show exam structure by default
        public void ShowExamStructure()
        {
            // Simulate clicking the Exam Structure button
            ExamStructure_Button_Click(exam_structure_button, new RoutedEventArgs());
        }

        public DataGridState? GetStructureState()
        {
            SaveCurrentTabState();
            return savedDataGridState as DataGridState;
        }

        public TimingState? GetTimingState()
        {
            SaveCurrentTabState();
            return savedTimingState as TimingState;
        }

        public LoginConfigState? GetLoginConfigState()
        {
            SaveCurrentTabState();
            return savedLoginConfigState as LoginConfigState;
        }

        public string GetExamTitle()
        {
            return exam_name_textbox?.Text ?? "Untitled Exam";
        }

        /// <summary>
        /// Restore structure_builder state from configuration
        /// </summary>
        public void RestoreFromConfiguration(ExamConfiguration configuration)
        {
            if (configuration == null) return;

            // Restore text fields
            exam_name_textbox.Text = configuration.ExamName;
            subject_name_textbox.Text = configuration.SubjectName;
            item_number_textbox.Text = configuration.NumberOfItems.ToString();
            currentItemCount = configuration.NumberOfItems;

            // Restore saved states
            savedDataGridState = configuration.DataGridState;
            savedTimingState = configuration.TimingState;
            savedAntiCheatState = configuration.AntiCheatState;
            savedLoginConfigState = configuration.LoginConfigState;
        }

        private void Review_Button_Click(object sender, RoutedEventArgs e)
        {
            // Validate structure first
            SaveCurrentTabState();
            
            var datagridState = savedDataGridState as DataGridState;
            if (datagridState == null || datagridState.Rows.Count == 0)
            {
                MessageBox.Show("Please configure the exam structure before reviewing.", 
                    "Incomplete Configuration", MessageBoxButton.OK, MessageBoxImage.Warning);
                return;
            }

            // Validate timing
            var timingState = savedTimingState as TimingState;
            if (timingState == null || timingState.StartDateTime == null || timingState.EndDateTime == null)
            {
                MessageBox.Show("Please configure the exam timing before reviewing.", 
                    "Incomplete Configuration", MessageBoxButton.OK, MessageBoxImage.Warning);
                return;
            }

            // Check if exam has a title
            if (string.IsNullOrWhiteSpace(exam_name_textbox.Text))
            {
                MessageBox.Show("Please enter an exam name before reviewing.", 
                    "Incomplete Configuration", MessageBoxButton.OK, MessageBoxImage.Warning);
                return;
            }

            // Get MainWindow and show review
            var mainWindow = Application.Current.MainWindow as MainWindow;
            if (mainWindow != null)
            {
                var examData = mainWindow.GatherExamData();
                if (examData != null)
                {
                    mainWindow.ShowExamReview(examData);
                }
            }
        }

        private void Back_Button_Click(object sender, RoutedEventArgs e)
        {
            // Navigate back to dashboard or previous screen
            Window mainWindow = Window.GetWindow(this);
            if (mainWindow is MainWindow main)
            {
                // Go back to dashboard
                if (main.FindName("MainContentHost") is ContentControl contentHost)
                {
                    contentHost.Content = null; // Or load dashboard
                }
            }
        }

        private void Next_Button_Click(object sender, RoutedEventArgs e)
        {
            // This calls the same logic as Proceed
            Proceed_Button_Click(sender, e);
        }

        private void Exam_Structure_Button_Click(object sender, RoutedEventArgs e)
        {
            // This is an alias for the full method name
            ExamStructure_Button_Click(sender, e);
        }
    }

    // Configuration data class
    public class ExamConfiguration
    {
        public string ExamName { get; set; } = "";
        public string SubjectName { get; set; } = "";
        public int NumberOfItems { get; set; }
        public object? DataGridState { get; set; }
        public object? TimingState { get; set; }
        public object? AntiCheatState { get; set; }
        public object? LoginConfigState { get; set; }
        public object? ContentBuilderState { get; set; } // NEW: Store content builder state
    }
}
