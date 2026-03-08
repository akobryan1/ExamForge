using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.ComponentModel;
using System.Linq;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Controls.Primitives; 

namespace ExamForge
{
    /// <summary>
    /// Interaction logic for content_builder.xaml
    /// </summary>
    public partial class content_builder : UserControl
    {
        private ExamConfiguration? currentConfiguration;
        private ObservableCollection<ExamItem> allItems = new ObservableCollection<ExamItem>();
        private ExamItem? selectedItem;
        private string currentFilter = "All";

        public content_builder()
        {
            InitializeComponent();
            ItemsListBox.ItemsSource = allItems;
        }

        public void LoadConfiguration(ExamConfiguration configuration)
        {
            currentConfiguration = configuration;
            
            // Restore saved content if exists
            if (configuration.ContentBuilderState != null && configuration.ContentBuilderState is List<ExamItem> savedItems)
            {
                allItems.Clear();
                foreach (var item in savedItems)
                {
                    allItems.Add(item);
                }
            }
            else
            {
                GenerateItems();
            }
            
            UpdateGroupsCount();
            ApplyFilter();
            
            // Auto-select first item
            if (allItems.Count > 0)
            {
                ItemsListBox.SelectedIndex = 0;
            }
        }

        private void GenerateItems()
        {
            allItems.Clear();

            if (currentConfiguration?.DataGridState is not DataGridState dataGridState)
                return;

            foreach (var row in dataGridState.Rows)
            {
                if (string.IsNullOrWhiteSpace(row.Start) || string.IsNullOrWhiteSpace(row.End))
                    continue;

                if (!int.TryParse(row.Start, out int start) || !int.TryParse(row.End, out int end))
                    continue;

                var isEssay = string.Equals(row.TestType, "Essay", StringComparison.OrdinalIgnoreCase);

                if (isEssay)
                {
                    var essayCount = int.TryParse(row.EssayCount, out var parsedEssayCount) && parsedEssayCount > 0
                        ? parsedEssayCount
                        : 1;

                    var totalEssayPoints = int.TryParse(row.Points, out var parsedPoints) && parsedPoints > 0
                        ? parsedPoints
                        : essayCount;

                    var basePoints = totalEssayPoints / essayCount;
                    var remainder = totalEssayPoints % essayCount;

                    for (int i = 0; i < essayCount; i++)
                    {
                        var distributedPoints = basePoints + (i < remainder ? 1 : 0);
                        if (distributedPoints <= 0) distributedPoints = 1;

                        var item = new ExamItem
                        {
                            Number = start + i,
                            Title = $"Item {start + i}",
                            TestGroup = row.TestGroup,
                            TestType = row.TestType,
                            Points = distributedPoints.ToString(),
                            Status = "Incomplete"
                        };
                        allItems.Add(item);
                    }

                    continue;
                }

                for (int i = start; i <= end; i++)
                {
                    var item = new ExamItem
                    {
                        Number = i,
                        Title = $"Item {i}",
                        TestGroup = row.TestGroup,
                        TestType = row.TestType,
                        Points = row.Points,
                        Status = "Incomplete"
                    };
                    allItems.Add(item);
                }
            }
        }

        private void UpdateGroupsCount()
        {
            if (currentConfiguration?.DataGridState is DataGridState dataGridState)
            {
                GroupsCount.Text = dataGridState.Rows.Count.ToString();
            }
        }

        private void SearchBox_TextChanged(object sender, TextChangedEventArgs e)
        {
            ApplyFilter();
        }

        private void FilterButton_Click(object sender, RoutedEventArgs e)
        {
            if (sender is ToggleButton button && button.Content is string filter)
            {
                // Uncheck all other filter buttons
                AllFilter.IsChecked = false;
                IncompleteFilter.IsChecked = false;
                CompleteFilter.IsChecked = false;
                ReviewFilter.IsChecked = false;

                // Check the clicked button
                button.IsChecked = true;
                currentFilter = filter;
                ApplyFilter();
            }
        }

        private void ApplyFilter()
        {
            var searchText = SearchBox.Text?.ToLower() ?? "";
            var filtered = allItems.AsEnumerable();

            // Apply status filter
            if (currentFilter != "All")
            {
                filtered = filtered.Where(i => i.Status == currentFilter);
            }

            // Apply search filter
            if (!string.IsNullOrWhiteSpace(searchText))
            {
                filtered = filtered.Where(i =>
                    i.Number.ToString().Contains(searchText) ||
                    i.Question.ToLower().Contains(searchText) ||
                    i.TestGroup.ToLower().Contains(searchText));
            }

            ItemsListBox.ItemsSource = new ObservableCollection<ExamItem>(filtered);
        }

        private void ItemsListBox_SelectionChanged(object sender, SelectionChangedEventArgs e)
        {
            if (ItemsListBox.SelectedItem is ExamItem item)
            {
                selectedItem = item;
                LoadItemEditor(item);
                EmptyState.Visibility = Visibility.Collapsed;
                EditorPanel.Visibility = Visibility.Visible;
            }
        }

        private void LoadItemEditor(ExamItem item)
        {
            ItemNumberLabel.Text = $"Item {item.Number}";
            
            // Access the TextBlock inside each Border badge
            if (TestGroupBadge.Child is TextBlock testGroupText)
                testGroupText.Text = item.TestGroup;
            
            if (TestTypeBadge.Child is TextBlock testTypeText)
                testTypeText.Text = item.TestType;
            
            if (PointsBadge.Child is TextBlock pointsText)
                pointsText.Text = $"{item.Points} pts";
            
            QuestionTextBox.Text = item.Question;

            // Load test type specific editor
            LoadTestTypeEditor(item);
        }

        private void LoadTestTypeEditor(ExamItem item)
        {
            // Clear previous editor
            AnswerEditorHost.Content = null;

            switch (item.TestType)
            {
                case "Multiple Choice":
                    AnswerEditorHost.Content = new MultipleChoiceEditor(item);
                    break;
                case "True or False":
                    AnswerEditorHost.Content = new TrueFalseEditor(item);
                    break;
                case "Modified True or False":
                    AnswerEditorHost.Content = new ModifiedTrueFalseEditor(item);
                    break;
                case "Identification":
                    AnswerEditorHost.Content = new IdentificationEditor(item);
                    break;
                case "Enumeration":
                    AnswerEditorHost.Content = new EnumerationEditor(item);
                    break;
                case "Essay":
                    AnswerEditorHost.Content = new EssayEditor(item);
                    break;
            }
        }

        private void QuestionTextBox_TextChanged(object sender, TextChangedEventArgs e)
        {
            if (selectedItem != null)
            {
                selectedItem.Question = QuestionTextBox.Text;
                UpdateItemStatus();
            }
        }

        private void UpdateItemStatus()
        {
            if (selectedItem == null) return;

            bool isComplete = !string.IsNullOrWhiteSpace(selectedItem.Question) &&
                            selectedItem.HasValidAnswer();

            selectedItem.Status = isComplete ? "Complete" : "Incomplete";
            ItemsListBox.Items.Refresh();
        }

        private void NextItem_Click(object sender, RoutedEventArgs e)
        {
            int currentIndex = ItemsListBox.SelectedIndex;
            if (currentIndex < allItems.Count - 1)
            {
                ItemsListBox.SelectedIndex = currentIndex + 1;
            }
        }

        private void Previous_Click(object sender, RoutedEventArgs e)
        {
            // Auto-save current content
            SaveContentState();

            // Navigate back to structure_builder
            var mainWindow = Application.Current.MainWindow as MainWindow;
            if (mainWindow == null)
            {
                MessageBox.Show("Unable to access main window.", "Error", MessageBoxButton.OK, MessageBoxImage.Error);
                return;
            }

            // Find structure_builder and reload it with saved data
            if (mainWindow.FindName("MainContentHost") is ContentControl contentHost)
            {
                var structureBuilder = new structure_builder_usercontrol();
                
                // Restore the configuration if available
                if (currentConfiguration != null)
                {
                    structureBuilder.RestoreFromConfiguration(currentConfiguration);
                }
                
                // ✅ Store reference to structure_builder
                mainWindow.SetStructureBuilder(structureBuilder);
                
                contentHost.Content = structureBuilder;
            }
        }

        private void Preview_Click(object sender, RoutedEventArgs e)
        {
            // Auto-save before preview
            SaveContentState();

            // Navigate to main window and trigger review
            var mainWindow = Application.Current.MainWindow as MainWindow;
            if (mainWindow == null)
            {
                MessageBox.Show("Unable to access main window.", "Error", MessageBoxButton.OK, MessageBoxImage.Error);
                return;
            }

            // Gather exam data
            var examData = mainWindow.GatherExamData();
            if (examData == null)
            {
                // GatherExamData() already shows appropriate error messages
                return;
            }

            // Validate that content is complete
            var incompleteItems = allItems.Where(i => i.Status == "Incomplete").ToList();
            if (incompleteItems.Any())
            {
                var result = MessageBox.Show(
                    $"You have {incompleteItems.Count} incomplete item(s).\n\n" +
                    $"Do you want to continue to review anyway?",
                    "Incomplete Content",
                    MessageBoxButton.YesNo,
                    MessageBoxImage.Warning);
                
                if (result == MessageBoxResult.No)
                    return;
            }

            // Show review screen
            mainWindow.ShowExamReview(examData);
        }

        /// <summary>
        /// Auto-save content builder state
        /// </summary>
        private void SaveContentState()
        {
            if (currentConfiguration != null)
            {
                currentConfiguration.ContentBuilderState = allItems.ToList();
            }
        }

        public List<ExamItem> GetAllItems()
        {
            return allItems.ToList();
        }

        /// <summary>
        /// Save state for restoration
        /// </summary>
        public object SaveState()
        {
            return allItems.ToList();
        }

        /// <summary>
        /// Restore state from saved data
        /// </summary>
        public void RestoreState(object state)
        {
            if (state is List<ExamItem> savedItems)
            {
                allItems.Clear();
                foreach (var item in savedItems)
                {
                    allItems.Add(item);
                }
                ApplyFilter();
                
                if (allItems.Count > 0)
                {
                    ItemsListBox.SelectedIndex = 0;
                }
            }
        }
    }

    // ==================== DATA MODELS ====================
    public class ExamItem : INotifyPropertyChanged
    {
        private string _status = "Incomplete";
        private string _question = "";

        public int Number { get; set; }
        public string Title { get; set; } = "";
        public string TestGroup { get; set; } = "";
        public string TestType { get; set; } = "";
        public string Points { get; set; } = "";
        public string CustomPoints { get; set; } = "";

        public string Status
        {
            get => _status;
            set
            {
                if (_status != value)
                {
                    _status = value;
                    OnPropertyChanged(nameof(Status));
                    OnPropertyChanged(nameof(StatusColor));
                }
            }
        }

        public string StatusColor => Status switch
        {
            "Complete" => "#4ade80",
            "Incomplete" => "#fbbf24",
            "Invalid" => "#ef4444",
            _ => "#9ca3af"
        };

        public string Question
        {
            get => _question;
            set
            {
                if (_question != value)
                {
                    _question = value;
                    OnPropertyChanged(nameof(Question));
                }
            }
        }

        // Multiple Choice
        public string OptionA { get; set; } = "";
        public string OptionB { get; set; } = "";
        public string OptionC { get; set; } = "";
        public string OptionD { get; set; } = "";
        public string CorrectAnswer { get; set; } = "";

        // True/False & Modified True/False
        public bool TrueFalseAnswer { get; set; }
        public string ModifiedAnswer { get; set; } = "";

        // Identification & Essay
        public string TextAnswer { get; set; } = "";

        // Essay infrastructure
        public string EssayRubric { get; set; } = "";
        public string EssayModelAnswer { get; set; } = "";
        public string EssayKeyPoints { get; set; } = "";
        public double EssayWeightThesis { get; set; } = 33;
        public double EssayWeightEvidence { get; set; } = 34;
        public double EssayWeightClarity { get; set; } = 33;

        // Enumeration
        public List<string> EnumerationAnswers { get; set; } = new List<string>();

        public bool HasValidAnswer()
        {
            return TestType switch
            {
                "Multiple Choice" => !string.IsNullOrWhiteSpace(OptionA) &&
                                   !string.IsNullOrWhiteSpace(OptionB) &&
                                   !string.IsNullOrWhiteSpace(CorrectAnswer),
                "True or False" => true, // Always has answer (bool)
                "Modified True or False" => !string.IsNullOrWhiteSpace(ModifiedAnswer),
                "Identification" => !string.IsNullOrWhiteSpace(TextAnswer),
                "Enumeration" => EnumerationAnswers.Count > 0 && EnumerationAnswers.All(a => !string.IsNullOrWhiteSpace(a)),
                "Essay" => !string.IsNullOrWhiteSpace(EssayRubric),
                _ => false
            };
        }

        public event PropertyChangedEventHandler? PropertyChanged;
        protected void OnPropertyChanged(string propertyName)
        {
            PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(propertyName));
        }
    }
}
