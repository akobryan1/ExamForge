using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Linq;
using System.Threading.Tasks;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Media;
using System.Windows.Shapes;
using ExamForge.Models;
using ExamForge.Services;

namespace ExamForge
{
    public partial class student_analytics_usercontrol : UserControl
    {
        private readonly AnalyticsService _analyticsService;
        private List<StudentPerformanceData> _allStudents = new();
        private StudentPerformanceData? _selectedStudent;

        public student_analytics_usercontrol()
        {
            InitializeComponent();
            _analyticsService = new AnalyticsService();
            Loaded += StudentAnalytics_Loaded;
        }

        private async void StudentAnalytics_Loaded(object sender, RoutedEventArgs e)
        {
            await LoadAnalyticsDataAsync();
        }

        private async Task LoadAnalyticsDataAsync()
        {
            try
            {
                // Load sample analytics data
                await LoadClassOverviewDataAsync();
                await LoadItemAnalysisDataAsync();
                await LoadStudentDataAsync();
                await LoadIntegrityDataAsync();
                DrawScoreHistogram();
            }
            catch (Exception ex)
            {
                Debug.WriteLine($"? Error loading analytics data: {ex.Message}");
                ShowError($"Failed to load analytics: {ex.Message}");
            }
        }

        #region Tab Navigation

        private void AnalyticsTab_Changed(object sender, RoutedEventArgs e)
        {
            try
            {
                // Hide all panels safely
                if (ClassOverviewPanel != null) ClassOverviewPanel.Visibility = Visibility.Collapsed;
                if (ItemAnalysisPanel != null) ItemAnalysisPanel.Visibility = Visibility.Collapsed;
                if (StudentDrilldownPanel != null) StudentDrilldownPanel.Visibility = Visibility.Collapsed;
                if (IntegrityPanel != null) IntegrityPanel.Visibility = Visibility.Collapsed;
                if (TrendsPanel != null) TrendsPanel.Visibility = Visibility.Collapsed;

                // Show selected panel
                if (ClassOverviewTab?.IsChecked == true && ClassOverviewPanel != null)
                {
                    ClassOverviewPanel.Visibility = Visibility.Visible;
                }
                else if (ItemAnalysisTab?.IsChecked == true && ItemAnalysisPanel != null)
                {
                    ItemAnalysisPanel.Visibility = Visibility.Visible;
                    LoadItemAnalysisDataAsync();
                }
                else if (StudentDrilldownTab?.IsChecked == true && StudentDrilldownPanel != null)
                {
                    StudentDrilldownPanel.Visibility = Visibility.Visible;
                    LoadStudentListAsync();
                }
                else if (IntegrityTab?.IsChecked == true && IntegrityPanel != null)
                {
                    IntegrityPanel.Visibility = Visibility.Visible;
                    LoadIntegrityDataAsync();
                }
                else if (TrendsTab?.IsChecked == true && TrendsPanel != null)
                {
                    TrendsPanel.Visibility = Visibility.Visible;
                    DrawTrendChart();
                }
            }
            catch (Exception ex)
            {
                Debug.WriteLine($"Error in AnalyticsTab_Changed: {ex.Message}");
            }
        }

        #endregion

        #region Class Overview

        private async Task LoadClassOverviewDataAsync()
        {
            try
            {
                // This would typically load from your analytics service
                // For now, using sample data as shown in the UI
                await Task.Delay(100); // Simulate async operation
            }
            catch (Exception ex)
            {
                Debug.WriteLine($"Error loading class overview: {ex.Message}");
            }
        }

        private void DrawScoreHistogram()
        {
            try
            {
                if (ScoreHistogram == null) return;

                ScoreHistogram.Children.Clear();

                // Sample score distribution data
                var scoreBins = new[]
                {
                    new { Range = "0-10", Count = 1, Color = "#ef4444" },
                    new { Range = "10-20", Count = 0, Color = "#ef4444" },
                    new { Range = "20-30", Count = 0, Color = "#f97316" },
                    new { Range = "30-40", Count = 1, Color = "#f97316" },
                    new { Range = "40-50", Count = 2, Color = "#fbbf24" },
                    new { Range = "50-60", Count = 3, Color = "#fbbf24" },
                    new { Range = "60-70", Count = 4, Color = "#84cc16" },
                    new { Range = "70-80", Count = 8, Color = "#22c55e" },
                    new { Range = "80-90", Count = 7, Color = "#10b981" },
                    new { Range = "90-100", Count = 3, Color = "#059669" }
                };

                var maxCount = scoreBins.Max(b => b.Count);
                var barWidth = ScoreHistogram.Width / scoreBins.Length - 4;
                var maxHeight = ScoreHistogram.Height - 20;

                for (int i = 0; i < scoreBins.Length; i++)
                {
                    var bin = scoreBins[i];
                    var barHeight = maxCount > 0 ? (bin.Count * maxHeight) / maxCount : 0;
                    
                    var bar = new Rectangle
                    {
                        Width = barWidth,
                        Height = barHeight,
                        Fill = new SolidColorBrush((Color)ColorConverter.ConvertFromString(bin.Color))
                    };

                    Canvas.SetLeft(bar, i * (barWidth + 4));
                    Canvas.SetTop(bar, maxHeight - barHeight);
                    ScoreHistogram.Children.Add(bar);

                    // Add count labels
                    var label = new TextBlock
                    {
                        Text = bin.Count.ToString(),
                        FontSize = 10,
                        Foreground = new SolidColorBrush(Colors.White),
                        HorizontalAlignment = HorizontalAlignment.Center
                    };

                    Canvas.SetLeft(label, i * (barWidth + 4) + barWidth / 2 - 5);
                    Canvas.SetTop(label, maxHeight - barHeight - 15);
                    ScoreHistogram.Children.Add(label);
                }
            }
            catch (Exception ex)
            {
                Debug.WriteLine($"Error drawing histogram: {ex.Message}");
            }
        }

        private void ApplyFilters_Click(object sender, RoutedEventArgs e)
        {
            // Apply selected filters and reload data
            LoadAnalyticsDataAsync();
        }

        #endregion

        #region Item Analysis

        private async Task LoadItemAnalysisDataAsync()
        {
            try
            {
                var sampleItemData = new List<ItemAnalysisData>
                {
                    new ItemAnalysisData { QuestionId = "Q1", QuestionNumber = 1, QuestionType = "MCQ", DifficultyPercent = 96, Discrimination = 0.35, PointBiserial = 0.42, NoResponsePercent = 0, AverageTime = 32, QualityIndicator = "Good" },
                    new ItemAnalysisData { QuestionId = "Q2", QuestionNumber = 2, QuestionType = "MCQ", DifficultyPercent = 87, Discrimination = 0.48, PointBiserial = 0.51, NoResponsePercent = 2, AverageTime = 45, QualityIndicator = "Excellent" },
                    new ItemAnalysisData { QuestionId = "Q3", QuestionNumber = 3, QuestionType = "MCQ", DifficultyPercent = 73, Discrimination = 0.52, PointBiserial = 0.48, NoResponsePercent = 1, AverageTime = 67, QualityIndicator = "Excellent" },
                    new ItemAnalysisData { QuestionId = "Q15", QuestionNumber = 15, QuestionType = "MCQ", DifficultyPercent = 38, Discrimination = 0.23, PointBiserial = 0.21, NoResponsePercent = 8, AverageTime = 125, QualityIndicator = "Review" },
                    new ItemAnalysisData { QuestionId = "Q22", QuestionNumber = 22, QuestionType = "Essay", DifficultyPercent = 42, Discrimination = 0.31, PointBiserial = 0.28, NoResponsePercent = 12, AverageTime = 245, QualityIndicator = "Review" },
                };

                if (ItemAnalysisGrid != null)
                    ItemAnalysisGrid.ItemsSource = sampleItemData;

                await Task.Delay(100); // Simulate async operation
            }
            catch (Exception ex)
            {
                Debug.WriteLine($"Error loading item analysis: {ex.Message}");
            }
        }

        private void ExportItemAnalysis_Click(object sender, RoutedEventArgs e)
        {
            try
            {
                MessageBox.Show("Item Analysis export functionality coming soon!", "Info", 
                    MessageBoxButton.OK, MessageBoxImage.Information);
            }
            catch (Exception ex)
            {
                ShowError($"Failed to export item analysis: {ex.Message}");
            }
        }

        private void ItemDetails_Click(object sender, RoutedEventArgs e)
        {
            if (sender is Button btn && btn.Tag is string questionId)
            {
                MessageBox.Show($"Detailed analysis for {questionId} coming soon!", "Item Details", 
                    MessageBoxButton.OK, MessageBoxImage.Information);
            }
        }

        #endregion

        #region Student Drilldown

        private async Task LoadStudentDataAsync()
        {
            try
            {
                _allStudents = new List<StudentPerformanceData>
                {
                    new StudentPerformanceData { Id = "1", Name = "Emma Johnson", Score = 92, Percentage = 92, Rank = 3, TimeTaken = TimeSpan.FromMinutes(42), FlagCount = 0 },
                    new StudentPerformanceData { Id = "2", Name = "Michael Chen", Score = 88, Percentage = 88, Rank = 5, TimeTaken = TimeSpan.FromMinutes(38), FlagCount = 1 },
                    new StudentPerformanceData { Id = "3", Name = "Sarah Williams", Score = 85, Percentage = 85, Rank = 7, TimeTaken = TimeSpan.FromMinutes(45), FlagCount = 0 },
                    new StudentPerformanceData { Id = "4", Name = "David Rodriguez", Score = 78, Percentage = 78, Rank = 12, TimeTaken = TimeSpan.FromMinutes(52), FlagCount = 2 },
                    new StudentPerformanceData { Id = "5", Name = "Lisa Thompson", Score = 52, Percentage = 52, Rank = 28, TimeTaken = TimeSpan.FromMinutes(35), FlagCount = 3 }
                };

                await Task.Delay(100); // Simulate async operation
            }
            catch (Exception ex)
            {
                Debug.WriteLine($"Error loading student data: {ex.Message}");
            }
        }

        private void LoadStudentListAsync()
        {
            try
            {
                if (StudentListBox != null && _allStudents.Any())
                {
                    var studentDisplayData = _allStudents.Select(s => new
                    {
                        Student = s,
                        Name = s.Name,
                        ScoreText = $"{s.Percentage}% (Rank: {s.Rank})"
                    }).ToList();

                    StudentListBox.ItemsSource = studentDisplayData;
                }
            }
            catch (Exception ex)
            {
                Debug.WriteLine($"Error loading student list: {ex.Message}");
            }
        }

        private void StudentSearchBox_GotFocus(object sender, RoutedEventArgs e)
        {
            if (StudentSearchBox.Text == "Search students...")
            {
                StudentSearchBox.Text = "";
                StudentSearchBox.Foreground = new SolidColorBrush(Color.FromRgb(232, 234, 237)); // TextPrimary
            }
        }

        private void StudentSearchBox_LostFocus(object sender, RoutedEventArgs e)
        {
            if (string.IsNullOrWhiteSpace(StudentSearchBox.Text))
            {
                StudentSearchBox.Text = "Search students...";
                StudentSearchBox.Foreground = new SolidColorBrush(Color.FromRgb(156, 163, 175)); // TextSecondary
            }
        }

        private void StudentListBox_SelectionChanged(object sender, SelectionChangedEventArgs e)
        {
            try
            {
                if (StudentListBox.SelectedItem != null)
                {
                    dynamic selectedItem = StudentListBox.SelectedItem;
                    _selectedStudent = selectedItem.Student;
                    LoadStudentDetails(_selectedStudent);
                }
            }
            catch (Exception ex)
            {
                Debug.WriteLine($"Error in student selection: {ex.Message}");
            }
        }

        private void LoadStudentDetails(StudentPerformanceData student)
        {
            try
            {
                if (SelectedStudentName != null)
                    SelectedStudentName.Text = $"?? {student.Name} - Performance Analysis";

                if (StudentPerformanceGrid != null)
                    StudentPerformanceGrid.Visibility = Visibility.Visible;

                // Update performance metrics
                if (StudentScore != null) StudentScore.Text = $"{student.Percentage}%";
                if (StudentRank != null) StudentRank.Text = $"{student.Rank}{GetOrdinalSuffix(student.Rank)}";
                if (StudentTime != null) StudentTime.Text = student.TimeTaken.ToString(@"mm\:ss");
                if (StudentFlags != null) 
                {
                    StudentFlags.Text = student.FlagCount.ToString();
                    StudentFlags.Foreground = student.FlagCount == 0 ? 
                        new SolidColorBrush(Color.FromRgb(16, 185, 129)) : // SuccessGreen
                        new SolidColorBrush(Color.FromRgb(239, 68, 68));   // ErrorRed
                }

                LoadMasteryBreakdown(student);
                LoadMissedItems(student);
            }
            catch (Exception ex)
            {
                Debug.WriteLine($"Error loading student details: {ex.Message}");
            }
        }

        private string GetOrdinalSuffix(int number)
        {
            if (number <= 0) return "";
            
            return (number % 100) switch
            {
                11 or 12 or 13 => "th",
                _ => (number % 10) switch
                {
                    1 => "st",
                    2 => "nd",
                    3 => "rd",
                    _ => "th"
                }
            };
        }

        private void LoadMasteryBreakdown(StudentPerformanceData student)
        {
            try
            {
                if (MasteryGrid == null) return;

                MasteryGrid.Children.Clear();
                MasteryGrid.RowDefinitions.Clear();

                var masteryAreas = new[]
                {
                    new { Topic = "Basic Algebra", Score = 85, Total = 100, Color = "#22c55e" },
                    new { Topic = "Quadratic Equations", Score = 60, Total = 100, Color = "#fbbf24" },
                    new { Topic = "Probability", Score = 45, Total = 100, Color = "#ef4444" },
                    new { Topic = "Geometry", Score = 92, Total = 100, Color = "#10b981" }
                };

                for (int i = 0; i < masteryAreas.Length; i++)
                {
                    var area = masteryAreas[i];
                    MasteryGrid.RowDefinitions.Add(new RowDefinition { Height = GridLength.Auto });

                    var stackPanel = new StackPanel { Orientation = Orientation.Horizontal, Margin = new Thickness(0, 4, 0, 4) };
                    
                    var topicText = new TextBlock 
                    { 
                        Text = area.Topic, 
                        Width = 150, 
                        FontSize = 12, 
                        Foreground = new SolidColorBrush(Color.FromRgb(232, 234, 237)) 
                    };
                    
                    var progressBar = new Border
                    {
                        Width = 100,
                        Height = 8,
                        Background = new SolidColorBrush(Color.FromRgb(51, 47, 68)),
                        CornerRadius = new CornerRadius(4),
                        Margin = new Thickness(8, 0, 8, 0)
                    };

                    var progress = new Border
                    {
                        Width = area.Score,
                        Height = 8,
                        Background = new SolidColorBrush((Color)ColorConverter.ConvertFromString(area.Color)),
                        CornerRadius = new CornerRadius(4),
                        HorizontalAlignment = HorizontalAlignment.Left
                    };

                    progressBar.Child = progress;

                    var scoreText = new TextBlock
                    {
                        Text = $"{area.Score}%",
                        FontSize = 12,
                        Foreground = new SolidColorBrush((Color)ColorConverter.ConvertFromString(area.Color))
                    };

                    stackPanel.Children.Add(topicText);
                    stackPanel.Children.Add(progressBar);
                    stackPanel.Children.Add(scoreText);

                    Grid.SetRow(stackPanel, i);
                    MasteryGrid.Children.Add(stackPanel);
                }
            }
            catch (Exception ex)
            {
                Debug.WriteLine($"Error loading mastery breakdown: {ex.Message}");
            }
        }

        private void LoadMissedItems(StudentPerformanceData student)
        {
            try
            {
                var sampleMissedItems = new List<MissedItemData>
                {
                    new MissedItemData { QuestionNumber = 15, Topic = "Quadratic Equations", StudentAnswer = "x = 2", CorrectAnswer = "x = ±3", PointsLost = 3 },
                    new MissedItemData { QuestionNumber = 22, Topic = "Probability", StudentAnswer = "0.25", CorrectAnswer = "0.375", PointsLost = 2 },
                    new MissedItemData { QuestionNumber = 18, Topic = "Factoring", StudentAnswer = "x(x+2)", CorrectAnswer = "(x+3)(x-1)", PointsLost = 2 }
                };

                if (MissedItemsGrid != null)
                    MissedItemsGrid.ItemsSource = sampleMissedItems;
            }
            catch (Exception ex)
            {
                Debug.WriteLine($"Error loading missed items: {ex.Message}");
            }
        }

        #endregion

        #region Integrity & Incidents

        private async Task LoadIntegrityDataAsync()
        {
            try
            {
                var sampleIncidents = new List<IntegrityIncidentData>
                {
                    new IntegrityIncidentData { Id = "1", Timestamp = DateTime.Now.AddHours(-2), StudentName = "Michael Chen", EventType = "tab_switch", Severity = "Warning", Details = "Switched to browser tab", IpAddress = "192.168.1.105" },
                    new IntegrityIncidentData { Id = "2", Timestamp = DateTime.Now.AddHours(-1.5), StudentName = "David Rodriguez", EventType = "focus_loss", Severity = "Warning", Details = "Application lost focus for 15 seconds", IpAddress = "192.168.1.112" },
                    new IntegrityIncidentData { Id = "3", Timestamp = DateTime.Now.AddHours(-1), StudentName = "Lisa Thompson", EventType = "multiple_login", Severity = "Critical", Details = "Multiple login attempts detected", IpAddress = "192.168.1.108" },
                    new IntegrityIncidentData { Id = "4", Timestamp = DateTime.Now.AddMinutes(-45), StudentName = "David Rodriguez", EventType = "tab_switch", Severity = "Warning", Details = "Switched to external application", IpAddress = "192.168.1.112" },
                    new IntegrityIncidentData { Id = "5", Timestamp = DateTime.Now.AddMinutes(-30), StudentName = "Lisa Thompson", EventType = "disconnect", Severity = "Info", Details = "Connection lost for 3 minutes", IpAddress = "192.168.1.108" }
                };

                if (IntegrityIncidentsGrid != null)
                    IntegrityIncidentsGrid.ItemsSource = sampleIncidents.OrderByDescending(i => i.Timestamp);

                await Task.Delay(100); // Simulate async operation
            }
            catch (Exception ex)
            {
                Debug.WriteLine($"Error loading integrity data: {ex.Message}");
            }
        }

        private void ExportIntegrityTimeline_Click(object sender, RoutedEventArgs e)
        {
            try
            {
                MessageBox.Show("Integrity timeline export functionality coming soon!", "Info", 
                    MessageBoxButton.OK, MessageBoxImage.Information);
            }
            catch (Exception ex)
            {
                ShowError($"Failed to export integrity timeline: {ex.Message}");
            }
        }

        private void ReviewIncident_Click(object sender, RoutedEventArgs e)
        {
            if (sender is Button btn && btn.Tag is string incidentId)
            {
                MessageBox.Show($"Review incident {incidentId} functionality coming soon!", "Review Incident", 
                    MessageBoxButton.OK, MessageBoxImage.Information);
            }
        }

        #endregion

        #region Trends Over Time

        private void DrawTrendChart()
        {
            try
            {
                if (TrendChart == null) return;

                TrendChart.Children.Clear();

                // Sample trend data points
                var trendData = new[]
                {
                    new { Month = "Sep", Score = 72.5 },
                    new { Month = "Oct", Score = 75.2 },
                    new { Month = "Nov", Score = 78.8 },
                    new { Month = "Dec", Score = 76.1 },
                    new { Month = "Jan", Score = 78.5 },
                    new { Month = "Feb", Score = 81.2 }
                };

                var chartWidth = TrendChart.Width - 40;
                var chartHeight = TrendChart.Height - 40;
                var pointSpacing = chartWidth / (trendData.Length - 1);

                var minScore = trendData.Min(d => d.Score);
                var maxScore = trendData.Max(d => d.Score);
                var scoreRange = maxScore - minScore;

                // Draw trend line
                for (int i = 0; i < trendData.Length - 1; i++)
                {
                    var point1 = trendData[i];
                    var point2 = trendData[i + 1];

                    var x1 = 20 + i * pointSpacing;
                    var y1 = 20 + chartHeight - ((point1.Score - minScore) / scoreRange * chartHeight);
                    var x2 = 20 + (i + 1) * pointSpacing;
                    var y2 = 20 + chartHeight - ((point2.Score - minScore) / scoreRange * chartHeight);

                    var line = new Line
                    {
                        X1 = x1,
                        Y1 = y1,
                        X2 = x2,
                        Y2 = y2,
                        Stroke = new SolidColorBrush(Color.FromRgb(212, 184, 150)), // AccentGold
                        StrokeThickness = 3
                    };

                    TrendChart.Children.Add(line);

                    // Add data points
                    var point = new Ellipse
                    {
                        Width = 8,
                        Height = 8,
                        Fill = new SolidColorBrush(Color.FromRgb(212, 184, 150)) // AccentGold
                    };

                    Canvas.SetLeft(point, x1 - 4);
                    Canvas.SetTop(point, y1 - 4);
                    TrendChart.Children.Add(point);

                    // Add month labels
                    var label = new TextBlock
                    {
                        Text = point1.Month,
                        FontSize = 10,
                        Foreground = new SolidColorBrush(Colors.White),
                        HorizontalAlignment = HorizontalAlignment.Center
                    };

                    Canvas.SetLeft(label, x1 - 10);
                    Canvas.SetTop(label, chartHeight + 25);
                    TrendChart.Children.Add(label);
                }

                // Add final point and label
                var lastIndex = trendData.Length - 1;
                var lastPoint = trendData[lastIndex];
                var lastX = 20 + lastIndex * pointSpacing;
                var lastY = 20 + chartHeight - ((lastPoint.Score - minScore) / scoreRange * chartHeight);

                var finalPoint = new Ellipse
                {
                    Width = 8,
                    Height = 8,
                    Fill = new SolidColorBrush(Color.FromRgb(212, 184, 150))
                };

                Canvas.SetLeft(finalPoint, lastX - 4);
                Canvas.SetTop(finalPoint, lastY - 4);
                TrendChart.Children.Add(finalPoint);

                var finalLabel = new TextBlock
                {
                    Text = lastPoint.Month,
                    FontSize = 10,
                    Foreground = new SolidColorBrush(Colors.White),
                    HorizontalAlignment = HorizontalAlignment.Center
                };

                Canvas.SetLeft(finalLabel, lastX - 10);
                Canvas.SetTop(finalLabel, chartHeight + 25);
                TrendChart.Children.Add(finalLabel);
            }
            catch (Exception ex)
            {
                Debug.WriteLine($"Error drawing trend chart: {ex.Message}");
            }
        }

        private void UpdateTrendChart_Click(object sender, RoutedEventArgs e)
        {
            DrawTrendChart();
        }

        #endregion

        private void ShowError(string message)
        {
            MessageBox.Show(message, "Error", MessageBoxButton.OK, MessageBoxImage.Error);
        }
    }

    #region Analytics Data Models

    public class ItemAnalysisData
    {
        public string QuestionId { get; set; } = "";
        public int QuestionNumber { get; set; }
        public string QuestionType { get; set; } = "";
        public double DifficultyPercent { get; set; }
        public double Discrimination { get; set; }
        public double PointBiserial { get; set; }
        public double NoResponsePercent { get; set; }
        public int AverageTime { get; set; }
        public string QualityIndicator { get; set; } = "";
    }

    public class StudentPerformanceData
    {
        public string Id { get; set; } = "";
        public string Name { get; set; } = "";
        public int Score { get; set; }
        public double Percentage { get; set; }
        public int Rank { get; set; }
        public TimeSpan TimeTaken { get; set; }
        public int FlagCount { get; set; }
    }

    public class MissedItemData
    {
        public int QuestionNumber { get; set; }
        public string Topic { get; set; } = "";
        public string StudentAnswer { get; set; } = "";
        public string CorrectAnswer { get; set; } = "";
        public int PointsLost { get; set; }
    }

    public class IntegrityIncidentData
    {
        public string Id { get; set; } = "";
        public DateTime Timestamp { get; set; }
        public string StudentName { get; set; } = "";
        public string EventType { get; set; } = "";
        public string Severity { get; set; } = "";
        public string Details { get; set; } = "";
        public string IpAddress { get; set; } = "";
    }

    #endregion
}