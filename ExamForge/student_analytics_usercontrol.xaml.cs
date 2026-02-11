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
        private readonly FirestoreService _firestoreService;
        private List<StudentPerformanceData> _allStudents = new();
        private StudentPerformanceData? _selectedStudent;
        private List<PublishedExam> _availableExams = new();
        private string _currentExamId = "";

        public student_analytics_usercontrol()
        {
            InitializeComponent();
            _firestoreService = App.FirestoreService ?? throw new InvalidOperationException("Firestore service not initialized");
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
                // Load available exams first
                await LoadAvailableExamsAsync();
                
                // Load data for the first available exam
                if (_availableExams.Any())
                {
                    _currentExamId = _availableExams.First().Id;
                    await LoadClassOverviewDataAsync();
                    await LoadItemAnalysisDataAsync();
                    await LoadStudentDataAsync();
                    await LoadIntegrityDataAsync();
                    DrawScoreHistogram();
                }
                else
                {
                    ShowInfo("No published exams found. Please publish some exams first to see analytics.");
                }
            }
            catch (Exception ex)
            {
                Debug.WriteLine($"❌ Error loading analytics data: {ex.Message}");
                ShowError($"Failed to load analytics: {ex.Message}");
            }
        }

        private async Task LoadAvailableExamsAsync()
        {
            try
            {
                _availableExams = await _firestoreService.GetAllPublishedExamsAsync();
                
                // Update filter dropdown with real exams
                if (ExamAnalyticsFilter != null)
                {
                    ExamAnalyticsFilter.Items.Clear();
                    ExamAnalyticsFilter.Items.Add(new ComboBoxItem { Content = "All Exams" });
                    
                    foreach (var exam in _availableExams)
                    {
                        ExamAnalyticsFilter.Items.Add(new ComboBoxItem 
                        { 
                            Content = exam.Title,
                            Tag = exam.Id
                        });
                    }
                    
                    if (_availableExams.Any())
                    {
                        ExamAnalyticsFilter.SelectedIndex = 1; // Select first real exam
                    }
                }
            }
            catch (Exception ex)
            {
                Debug.WriteLine($"Error loading available exams: {ex.Message}");
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
                if (string.IsNullOrEmpty(_currentExamId))
                    return;

                // Get real class metrics from analytics service
                var metrics = await _analyticsService.CalculateClassOverviewAsync(_currentExamId);
                
                // Update UI with real data
                await UpdateClassMetricsDisplay(metrics);
            }
            catch (Exception ex)
            {
                Debug.WriteLine($"Error loading class overview: {ex.Message}");
                // Fall back to default values if data unavailable
                await UpdateClassMetricsDisplay(new ClassOverviewMetrics());
            }
        }

        private async Task UpdateClassMetricsDisplay(ClassOverviewMetrics metrics)
        {
            try
            {
                // Update metric cards with real data
                var metricCards = FindVisualChildren<TextBlock>(this)
                    .Where(tb => tb.FontSize == 32 && tb.FontWeight == FontWeights.Bold)
                    .ToList();

                if (metricCards.Count >= 4)
                {
                    metricCards[0].Text = $"{metrics.ClassAverage:F1}%";
                    metricCards[1].Text = $"{metrics.PassRate:F0}%";
                    metricCards[2].Text = $"{metrics.CompletionRate:F0}%";
                    
                    // Get integrity incidents for flagged rate
                    var incidents = await _firestoreService.GetIntegrityIncidentsAsync(_currentExamId);
                    var flaggedRate = metrics.TotalStudents > 0 ? 
                        (incidents.Count / (double)metrics.TotalStudents) * 100 : 0;
                    metricCards[3].Text = $"{flaggedRate:F1}%";
                }

                // Update sub-text with real data
                var subTexts = FindVisualChildren<TextBlock>(this)
                    .Where(tb => tb.FontSize == 10 && tb.Margin.Top == 4)
                    .ToList();

                if (subTexts.Count >= 3)
                {
                    // Calculate trend comparison (placeholder for now)
                    subTexts[0].Text = "Based on current data";
                    subTexts[1].Text = $"{metrics.PassingStudents} of {metrics.TotalStudents} students";
                    subTexts[2].Text = $"{metrics.TotalStudents} students submitted";
                }
            }
            catch (Exception ex)
            {
                Debug.WriteLine($"Error updating metrics display: {ex.Message}");
            }
        }

        private void DrawScoreHistogram()
        {
            try
            {
                if (ScoreHistogram == null || string.IsNullOrEmpty(_currentExamId)) return;

                ScoreHistogram.Children.Clear();

                // Get real score data
                Task.Run(async () =>
                {
                    try
                    {
                        var submissions = await _firestoreService.GetExamSubmissionsAsync(_currentExamId);
                        if (!submissions.Any()) return;

                        var scores = submissions.Select(s => s.TotalPossiblePoints > 0 ? 
                            (s.TotalScore / s.TotalPossiblePoints) * 100 : 0).ToList();

                        // Create score distribution bins
                        var scoreBins = new List<ScoreBin>();
                        for (int i = 0; i < 10; i++)
                        {
                            var min = i * 10;
                            var max = (i + 1) * 10;
                            var count = scores.Count(s => s >= min && s < max);
                            
                            var color = i switch
                            {
                                < 3 => "#ef4444", // Red for low scores
                                < 5 => "#f97316", // Orange 
                                < 6 => "#fbbf24", // Yellow
                                < 7 => "#84cc16", // Light green
                                _ => "#10b981"     // Green for high scores
                            };

                            scoreBins.Add(new ScoreBin 
                            { 
                                Range = $"{min}-{max}", 
                                Count = count, 
                                Color = color 
                            });
                        }

                        // Update UI on main thread
                        Dispatcher.Invoke(() => DrawHistogramBars(scoreBins));
                    }
                    catch (Exception ex)
                    {
                        Debug.WriteLine($"Error loading score data: {ex.Message}");
                    }
                });
            }
            catch (Exception ex)
            {
                Debug.WriteLine($"Error drawing histogram: {ex.Message}");
            }
        }

        private void DrawHistogramBars(List<ScoreBin> scoreBins)
        {
            try
            {
                if (ScoreHistogram == null) return;

                var maxCount = scoreBins.Max(b => b.Count);
                if (maxCount == 0) return;

                var barWidth = ScoreHistogram.ActualWidth > 0 ? 
                    ScoreHistogram.ActualWidth / scoreBins.Count - 4 : 50;
                var maxHeight = ScoreHistogram.ActualHeight > 0 ? 
                    ScoreHistogram.ActualHeight - 20 : 180;

                for (int i = 0; i < scoreBins.Count; i++)
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
                Debug.WriteLine($"Error drawing histogram bars: {ex.Message}");
            }
        }

        private void ApplyFilters_Click(object sender, RoutedEventArgs e)
        {
            try
            {
                // Get selected exam from filter
                if (ExamAnalyticsFilter?.SelectedItem is ComboBoxItem selectedItem && 
                    selectedItem.Tag is string examId)
                {
                    _currentExamId = examId;
                    LoadAnalyticsDataAsync();
                }
                else if (ExamAnalyticsFilter?.SelectedIndex == 0) // "All Exams" selected
                {
                    // Use first available exam
                    _currentExamId = _availableExams.FirstOrDefault()?.Id ?? "";
                    LoadAnalyticsDataAsync();
                }
            }
            catch (Exception ex)
            {
                Debug.WriteLine($"Error applying filters: {ex.Message}");
            }
        }

        #endregion

        #region Item Analysis

        private async Task LoadItemAnalysisDataAsync()
        {
            try
            {
                if (string.IsNullOrEmpty(_currentExamId))
                    return;

                // Get real item analysis data from analytics service
                var itemAnalysis = await _analyticsService.AnalyzeExamItemsAsync(_currentExamId);

                // Convert to UI model
                var itemData = itemAnalysis.Select(item => new ItemAnalysisData
                {
                    QuestionId = item.QuestionId,
                    QuestionNumber = int.TryParse(item.QuestionNumber, out int qNum) ? qNum : 1,
                    QuestionType = item.QuestionType,
                    DifficultyPercent = Math.Round(item.DifficultyPercent, 1),
                    Discrimination = Math.Round(item.Discrimination, 2),
                    PointBiserial = Math.Round(item.PointBiserial, 2),
                    NoResponsePercent = Math.Round(item.NoResponsePercent, 1),
                    AverageTime = 0, // Would need timing data from submissions
                    QualityIndicator = item.QualityIndicator
                }).OrderBy(x => x.QuestionNumber).ToList();

                if (ItemAnalysisGrid != null)
                    ItemAnalysisGrid.ItemsSource = itemData;
            }
            catch (Exception ex)
            {
                Debug.WriteLine($"Error loading item analysis: {ex.Message}");
                // Fall back to empty data
                if (ItemAnalysisGrid != null)
                    ItemAnalysisGrid.ItemsSource = new List<ItemAnalysisData>();
            }
        }

        private void ExportItemAnalysis_Click(object sender, RoutedEventArgs e)
        {
            try
            {
                if (string.IsNullOrEmpty(_currentExamId))
                {
                    ShowInfo("Please select an exam first.");
                    return;
                }

                // Use existing export service for item analysis
                Task.Run(async () =>
                {
                    try
                    {
                        var exam = _availableExams.FirstOrDefault(e => e.Id == _currentExamId);
                        if (exam != null)
                        {
                            // For now, show info that feature is in development
                            // In a full implementation, you'd create an ItemAnalysisExport method
                            Dispatcher.Invoke(() =>
                            {
                                MessageBox.Show("Item Analysis export is being prepared. This feature will generate detailed question analysis reports.", 
                                    "Export in Progress", MessageBoxButton.OK, MessageBoxImage.Information);
                            });
                        }
                    }
                    catch (Exception ex)
                    {
                        Dispatcher.Invoke(() => ShowError($"Export failed: {ex.Message}"));
                    }
                });
            }
            catch (Exception ex)
            {
                ShowError($"Failed to start export: {ex.Message}");
            }
        }

        private void ItemDetails_Click(object sender, RoutedEventArgs e)
        {
            if (sender is Button btn && btn.Tag is string questionId)
            {
                // Show detailed item analysis
                var exam = _availableExams.FirstOrDefault(e => e.Id == _currentExamId);
                var question = exam?.Contents.FirstOrDefault(q => q.Id == questionId);
                
                if (question != null)
                {
                    var details = $"Question: {question.QuestionText}\n" +
                                 $"Type: {question.QuestionType}\n" +
                                 $"Points: {question.Points}\n" +
                                 $"Correct Answer: {question.CorrectAnswer}";
                    
                    MessageBox.Show(details, "Question Details", MessageBoxButton.OK, MessageBoxImage.Information);
                }
            }
        }

        #endregion

        #region Student Drilldown

        private async Task LoadStudentDataAsync()
        {
            try
            {
                if (string.IsNullOrEmpty(_currentExamId))
                    return;

                var submissions = await _firestoreService.GetExamSubmissionsAsync(_currentExamId);
                if (!submissions.Any())
                {
                    _allStudents = new List<StudentPerformanceData>();
                    return;
                }

                // Convert submissions to student performance data
                var allScores = submissions
                    .Select(s => s.TotalPossiblePoints > 0 ? (s.TotalScore / s.TotalPossiblePoints) * 100 : 0)
                    .OrderByDescending(s => s)
                    .ToList();

                _allStudents = submissions.Select(submission =>
                {
                    var percentage = submission.TotalPossiblePoints > 0 ? 
                        (submission.TotalScore / submission.TotalPossiblePoints) * 100 : 0;
                    var rank = allScores.IndexOf(percentage) + 1;

                    var timeTaken = submission.EndTime.HasValue && submission.StartTime.HasValue ?
                        submission.EndTime.Value - submission.StartTime.Value :
                        TimeSpan.Zero;

                    // Count integrity incidents for this student
                    var flagCount = 0;
                    Task.Run(async () =>
                    {
                        try
                        {
                            var incidents = await _firestoreService.GetIntegrityIncidentsAsync(_currentExamId);
                            flagCount = incidents.Count(i => i.StudentId == submission.StudentId);
                        }
                        catch { /* Ignore if incidents can't be loaded */ }
                    });

                    return new StudentPerformanceData
                    {
                        Id = submission.StudentId,
                        Name = submission.StudentName,
                        Score = (int)submission.TotalScore,
                        Percentage = percentage,
                        Rank = rank,
                        TimeTaken = timeTaken,
                        FlagCount = flagCount
                    };
                }).OrderBy(s => s.Rank).ToList();
            }
            catch (Exception ex)
            {
                Debug.WriteLine($"Error loading student data: {ex.Message}");
                _allStudents = new List<StudentPerformanceData>();
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
                        ScoreText = $"{s.Percentage:F1}% (Rank: {s.Rank})"
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

        private async void LoadStudentDetails(StudentPerformanceData student)
        {
            try
            {
                if (SelectedStudentName != null)
                    SelectedStudentName.Text = $"📊 {student.Name} - Performance Analysis";

                if (StudentPerformanceGrid != null)
                    StudentPerformanceGrid.Visibility = Visibility.Visible;

                // Update performance metrics
                if (StudentScore != null) StudentScore.Text = $"{student.Percentage:F1}%";
                if (StudentRank != null) StudentRank.Text = $"{student.Rank}{GetOrdinalSuffix(student.Rank)}";
                if (StudentTime != null) StudentTime.Text = student.TimeTaken.ToString(@"mm\:ss");
                
                // Get real flag count from database
                try
                {
                    var incidents = await _firestoreService.GetIntegrityIncidentsAsync(_currentExamId);
                    var realFlagCount = incidents.Count(i => i.StudentId == student.Id);
                    
                    if (StudentFlags != null) 
                    {
                        StudentFlags.Text = realFlagCount.ToString();
                        StudentFlags.Foreground = realFlagCount == 0 ? 
                            new SolidColorBrush(Color.FromRgb(16, 185, 129)) : // SuccessGreen
                            new SolidColorBrush(Color.FromRgb(239, 68, 68));   // ErrorRed
                    }
                }
                catch (Exception ex)
                {
                    Debug.WriteLine($"Error loading flag count: {ex.Message}");
                }

                await LoadMasteryBreakdown(student);
                await LoadMissedItems(student);
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

        private async Task LoadMasteryBreakdown(StudentPerformanceData student)
        {
            try
            {
                if (MasteryGrid == null) return;

                MasteryGrid.Children.Clear();
                MasteryGrid.RowDefinitions.Clear();

                // Get real mastery data from student's submission
                var submissions = await _firestoreService.GetExamSubmissionsAsync(_currentExamId);
                var studentSubmission = submissions.FirstOrDefault(s => s.StudentId == student.Id);
                
                if (studentSubmission?.Responses == null || !studentSubmission.Responses.Any())
                {
                    // Show message if no response data
                    var noDataText = new TextBlock
                    {
                        Text = "No detailed response data available",
                        FontSize = 12,
                        Foreground = new SolidColorBrush(Color.FromRgb(156, 163, 175))
                    };
                    MasteryGrid.Children.Add(noDataText);
                    return;
                }

                // Group responses by topic (simplified - using question type as proxy)
                var topicGroups = studentSubmission.Responses
                    .GroupBy(r => GetTopicFromQuestionId(r.QuestionId))
                    .Where(g => !string.IsNullOrEmpty(g.Key))
                    .ToList();

                for (int i = 0; i < topicGroups.Count; i++)
                {
                    var group = topicGroups[i];
                    var correctCount = group.Count(r => r.IsCorrect);
                    var totalCount = group.Count();
                    var percentage = totalCount > 0 ? (correctCount * 100.0 / totalCount) : 0;

                    MasteryGrid.RowDefinitions.Add(new RowDefinition { Height = GridLength.Auto });

                    var stackPanel = new StackPanel 
                    { 
                        Orientation = Orientation.Horizontal, 
                        Margin = new Thickness(0, 4, 0, 4) 
                    };
                    
                    var topicText = new TextBlock 
                    { 
                        Text = group.Key, 
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
                        Width = percentage,
                        Height = 8,
                        Background = new SolidColorBrush(GetColorForPercentage(percentage)),
                        CornerRadius = new CornerRadius(4),
                        HorizontalAlignment = HorizontalAlignment.Left
                    };

                    progressBar.Child = progress;

                    var scoreText = new TextBlock
                    {
                        Text = $"{percentage:F0}%",
                        FontSize = 12,
                        Foreground = new SolidColorBrush(GetColorForPercentage(percentage))
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

        private async Task LoadMissedItems(StudentPerformanceData student)
        {
            try
            {
                var submissions = await _firestoreService.GetExamSubmissionsAsync(_currentExamId);
                var studentSubmission = submissions.FirstOrDefault(s => s.StudentId == student.Id);
                
                if (studentSubmission?.Responses == null)
                {
                    if (MissedItemsGrid != null)
                        MissedItemsGrid.ItemsSource = new List<MissedItemData>();
                    return;
                }

                var exam = _availableExams.FirstOrDefault(e => e.Id == _currentExamId);
                
                var missedItems = studentSubmission.Responses
                    .Where(r => !r.IsCorrect)
                    .Select(r =>
                    {
                        var question = exam?.Contents.FirstOrDefault(q => q.Id == r.QuestionId);
                        return new MissedItemData
                        {
                            QuestionNumber = r.QuestionNumber,
                            Topic = GetTopicFromQuestionId(r.QuestionId),
                            StudentAnswer = r.Answer ?? "No Answer",
                            CorrectAnswer = question?.CorrectAnswer ?? "Unknown",
                            PointsLost = (int)(r.PointsPossible - r.PointsEarned)
                        };
                    })
                    .OrderBy(m => m.QuestionNumber)
                    .ToList();

                if (MissedItemsGrid != null)
                    MissedItemsGrid.ItemsSource = missedItems;
            }
            catch (Exception ex)
            {
                Debug.WriteLine($"Error loading missed items: {ex.Message}");
                if (MissedItemsGrid != null)
                    MissedItemsGrid.ItemsSource = new List<MissedItemData>();
            }
        }

        private string GetTopicFromQuestionId(string questionId)
        {
            // Simplified topic extraction - in a real implementation, 
            // questions would have topic tags or categories
            if (questionId.Contains("math", StringComparison.OrdinalIgnoreCase))
                return "Mathematics";
            if (questionId.Contains("sci", StringComparison.OrdinalIgnoreCase))
                return "Science";
            if (questionId.Contains("hist", StringComparison.OrdinalIgnoreCase))
                return "History";
            
            // Default topic based on question ID pattern or exam content
            return "General";
        }

        private Color GetColorForPercentage(double percentage)
        {
            return percentage switch
            {
                >= 80 => Color.FromRgb(16, 185, 129),  // Green
                >= 60 => Color.FromRgb(251, 191, 36),  // Yellow
                _ => Color.FromRgb(239, 68, 68)        // Red
            };
        }

        #endregion

        #region Integrity & Incidents

        private async Task LoadIntegrityDataAsync()
        {
            try
            {
                if (string.IsNullOrEmpty(_currentExamId))
                    return;

                var incidents = await _firestoreService.GetIntegrityIncidentsAsync(_currentExamId);
                
                // Convert to UI model with real data
                var incidentData = incidents.Select(incident => new IntegrityIncidentData
                {
                    Id = incident.Id,
                    Timestamp = incident.Timestamp,
                    StudentName = incident.StudentName,
                    EventType = incident.IncidentType,
                    Severity = incident.Severity,
                    Details = incident.Details ?? "",
                    IpAddress = "192.168.1.xxx" // Masked for privacy
                }).OrderByDescending(i => i.Timestamp).ToList();

                if (IntegrityIncidentsGrid != null)
                    IntegrityIncidentsGrid.ItemsSource = incidentData;

                // Update summary metrics with real data
                UpdateIntegritySummary(incidents);
            }
            catch (Exception ex)
            {
                Debug.WriteLine($"Error loading integrity data: {ex.Message}");
                // Fall back to empty data
                if (IntegrityIncidentsGrid != null)
                    IntegrityIncidentsGrid.ItemsSource = new List<IntegrityIncidentData>();
            }
        }

        private void UpdateIntegritySummary(List<IntegrityIncident> incidents)
        {
            try
            {
                // Find and update the metric cards in the Integrity panel
                var metricTexts = FindVisualChildren<TextBlock>(IntegrityPanel)
                    .Where(tb => tb.FontSize == 32 && tb.FontWeight == FontWeights.Bold)
                    .ToList();

                if (metricTexts.Count >= 4)
                {
                    metricTexts[0].Text = incidents.Count.ToString();
                    metricTexts[1].Text = incidents.Count(i => i.IncidentType.Contains("tab", StringComparison.OrdinalIgnoreCase)).ToString();
                    metricTexts[2].Text = incidents.Count(i => i.IncidentType.Contains("focus", StringComparison.OrdinalIgnoreCase)).ToString();
                    metricTexts[3].Text = incidents.Count(i => i.IncidentType.Contains("login", StringComparison.OrdinalIgnoreCase)).ToString();
                }
            }
            catch (Exception ex)
            {
                Debug.WriteLine($"Error updating integrity summary: {ex.Message}");
            }
        }

        private void ExportIntegrityTimeline_Click(object sender, RoutedEventArgs e)
        {
            try
            {
                if (string.IsNullOrEmpty(_currentExamId))
                {
                    ShowInfo("Please select an exam first.");
                    return;
                }

                Task.Run(async () =>
                {
                    try
                    {
                        var exam = _availableExams.FirstOrDefault(e => e.Id == _currentExamId);
                        if (exam != null)
                        {
                            var exportService = new ExportService();
                            var filePath = await exportService.ExportIntegrityReportAsync(_currentExamId, exam.Title);
                            
                            Dispatcher.Invoke(() =>
                            {
                                MessageBox.Show($"Integrity report exported successfully!\nSaved to: {filePath}", 
                                    "Export Complete", MessageBoxButton.OK, MessageBoxImage.Information);
                            });
                        }
                    }
                    catch (Exception ex)
                    {
                        Dispatcher.Invoke(() => ShowError($"Export failed: {ex.Message}"));
                    }
                });
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
                // Show detailed incident information
                var incident = IntegrityIncidentsGrid.ItemsSource?.Cast<IntegrityIncidentData>()
                    .FirstOrDefault(i => i.Id == incidentId);
                
                if (incident != null)
                {
                    var details = $"Student: {incident.StudentName}\n" +
                                 $"Event Type: {incident.EventType}\n" +
                                 $"Severity: {incident.Severity}\n" +
                                 $"Time: {incident.Timestamp:yyyy-MM-dd HH:mm:ss}\n" +
                                 $"Details: {incident.Details}\n" +
                                 $"IP Address: {incident.IpAddress}";
                    
                    MessageBox.Show(details, "Incident Details", MessageBoxButton.OK, MessageBoxImage.Warning);
                }
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

        private void ShowInfo(string message)
        {
            MessageBox.Show(message, "Information", MessageBoxButton.OK, MessageBoxImage.Information);
        }

        /// <summary>
        /// Find all visual children of a specific type
        /// </summary>
        private static IEnumerable<T> FindVisualChildren<T>(DependencyObject depObj) where T : DependencyObject
        {
            if (depObj != null)
            {
                for (int i = 0; i < VisualTreeHelper.GetChildrenCount(depObj); i++)
                {
                    DependencyObject child = VisualTreeHelper.GetChild(depObj, i);
                    if (child != null && child is T)
                    {
                        yield return (T)child;
                    }

                    foreach (T childOfChild in FindVisualChildren<T>(child))
                    {
                        yield return childOfChild;
                    }
                }
            }
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

    public class ScoreBin
    {
        public string Range { get; set; } = "";
        public int Count { get; set; }
        public string Color { get; set; } = "";
    }

    #endregion
}