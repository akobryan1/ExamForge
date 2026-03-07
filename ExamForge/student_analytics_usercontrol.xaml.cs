using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Linq;
using System.Threading.Tasks;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Media;
using System.Windows.Shapes;
using System.Windows.Threading;
using ExamForge.Models;
using ExamForge.Services;

namespace ExamForge
{
    public partial class student_analytics_usercontrol : UserControl
    {
        private readonly AnalyticsService _analyticsService;
        private readonly FirestoreService _firestoreService;
        private List<PublishedExam> _availableExams = new();
        private string _currentExamId = "";

        // Real-time update timers
        private System.Windows.Threading.DispatcherTimer? _analyticsRefreshTimer;
        private const int ANALYTICS_REFRESH_INTERVAL = 15000; // 15 seconds for analytics
        private DateTime _lastDataRefresh = DateTime.MinValue;

        public student_analytics_usercontrol()
        {
            InitializeComponent();
            _firestoreService = App.FirestoreService ?? throw new InvalidOperationException("Firestore service not initialized");
            _analyticsService = new AnalyticsService();
            Loaded += StudentAnalytics_Loaded;
            Unloaded += StudentAnalytics_Unloaded;
        }

        private async void StudentAnalytics_Loaded(object sender, RoutedEventArgs e)
        {
            await LoadAnalyticsDataAsync();
            StartAnalyticsRefresh();
        }

        private void StudentAnalytics_Unloaded(object sender, RoutedEventArgs e)
        {
            StopAnalyticsRefresh();
        }

        private void StartAnalyticsRefresh()
        {
            StopAnalyticsRefresh(); // Ensure no duplicate timers

            _analyticsRefreshTimer = new System.Windows.Threading.DispatcherTimer
            {
                Interval = TimeSpan.FromMilliseconds(ANALYTICS_REFRESH_INTERVAL)
            };
            
            _analyticsRefreshTimer.Tick += async (s, e) =>
            {
                // Only refresh if visible and data is stale
                if (this.IsVisible && (DateTime.UtcNow - _lastDataRefresh).TotalSeconds > 10)
                {
                    await RefreshCurrentTabData();
                }
            };
            
            _analyticsRefreshTimer.Start();
            Debug.WriteLine($"? Analytics auto-refresh started (every {ANALYTICS_REFRESH_INTERVAL/1000}s)");
        }

        private void StopAnalyticsRefresh()
        {
            _analyticsRefreshTimer?.Stop();
            _analyticsRefreshTimer = null;
            Debug.WriteLine("?? Analytics auto-refresh stopped");
        }

        private async Task RefreshCurrentTabData()
        {
            try
            {
                if (string.IsNullOrEmpty(_currentExamId)) return;

                // Determine which tab is active and refresh accordingly
                if (ClassOverviewTab?.IsChecked == true)
                {
                    await LoadClassOverviewDataAsync();
                }
                else if (ItemAnalysisTab?.IsChecked == true)
                {
                    await LoadItemAnalysisDataAsync();
                }
                else if (IntegrityTab?.IsChecked == true)
                {
                    await LoadIntegrityDataAsync();
                }
                else if (ExamineeDataTab?.IsChecked == true)
                {
                    await LoadExamineeDataAsync();
                }

                _lastDataRefresh = DateTime.UtcNow;
                Debug.WriteLine($"✅ Analytics data refreshed at {_lastDataRefresh:HH:mm:ss}");
            }
            catch (Exception ex)
            {
                Debug.WriteLine($"Error refreshing analytics data: {ex.Message}");
            }
        }

        private async void ApplyFilters_Click(object sender, RoutedEventArgs e)
        {
            try
            {
                if (!_availableExams.Any())
                {
                    await ShowEmptyAnalyticsStateAsync();
                    return;
                }

                // Get filter selections
                var selectedYearText = (SchoolYearFilter?.SelectedItem as ComboBoxItem)?.Content?.ToString() ?? "All Years";
                var selectedSubject = (SubjectAnalyticsFilter?.SelectedItem as ComboBoxItem)?.Content?.ToString() ?? "All Subjects";
                var selectedExamItem = ExamAnalyticsFilter?.SelectedItem as ComboBoxItem;
                var selectedExamId = selectedExamItem?.Tag as string;

                // Filter exams by year and subject
                var filteredExams = _availableExams.AsEnumerable();

                if (selectedYearText != "All Years" && int.TryParse(selectedYearText.Substring(0, 4), out int startYear))
                {
                    filteredExams = filteredExams.Where(e => e.PublishedDate.Year == startYear);
                }

                if (selectedSubject != "All Subjects")
                {
                    filteredExams = filteredExams.Where(e => string.Equals(e.Subject, selectedSubject, StringComparison.OrdinalIgnoreCase));
                }

                var filteredList = filteredExams.OrderByDescending(e => e.PublishedDate).ToList();

                if (!filteredList.Any())
                {
                    ShowInfo("No exams match the selected filters.");
                    return;
                }

                // Determine selected exam id
                if (!string.IsNullOrEmpty(selectedExamId) && filteredList.Any(e => e.Id == selectedExamId))
                {
                    _currentExamId = selectedExamId;
                }
                else
                {
                    // Default to first filtered exam
                    _currentExamId = filteredList.First().Id;
                }

                // Refresh analytics for selected exam
                await LoadClassOverviewDataAsync();
                await LoadIntegrityDataAsync();
                DrawScoreHistogram();

                _lastDataRefresh = DateTime.UtcNow;
            }
            catch (Exception ex)
            {
                Debug.WriteLine($"Error applying analytics filters: {ex.Message}");
                ShowError($"Failed to apply filters: {ex.Message}");
            }
        }

        private async Task LoadAnalyticsDataAsync()
        {
            try
            {
                // Load available exams first
                await LoadAvailableExamsAsync();
                
                // Load data for the first available exam or show empty state
                if (_availableExams.Any())
                {
                    _currentExamId = _availableExams.First().Id;
                    await LoadClassOverviewDataAsync();
                    await LoadIntegrityDataAsync();
                    DrawScoreHistogram();
                    
                    _lastDataRefresh = DateTime.UtcNow;
                    Debug.WriteLine($"✅ Initial analytics data loaded at {_lastDataRefresh:HH:mm:ss}");
                }
                else
                {
                    await ShowEmptyAnalyticsStateAsync();
                }
            }
            catch (Exception ex)
            {
                Debug.WriteLine($"❌ Error loading analytics data: {ex.Message}");
                await ShowEmptyAnalyticsStateAsync();
            }
        }

        private async Task LoadAvailableExamsAsync()
        {
            try
            {
                _availableExams = await _firestoreService.GetAllPublishedExamsAsync();
                await PopulateFiltersAsync();
                Debug.WriteLine($"✅ Loaded {_availableExams.Count} exams for analytics");
            }
            catch (Exception ex)
            {
                Debug.WriteLine($"Error loading available exams: {ex.Message}");
                _availableExams = new List<PublishedExam>();
            }
        }

        private async Task PopulateFiltersAsync()
        {
            await Dispatcher.InvokeAsync(() =>
            {
                // Populate School Year filter
                if (SchoolYearFilter != null)
                {
                    SchoolYearFilter.Items.Clear();
                    SchoolYearFilter.Items.Add(new ComboBoxItem { Content = "All Years" });
                    
                    var years = _availableExams
                        .Select(e => e.PublishedDate.Year)
                        .Distinct()
                        .OrderByDescending(y => y)
                        .Select(y => $"{y}-{y + 1}")
                        .ToList();
                    
                    foreach (var year in years)
                    {
                        SchoolYearFilter.Items.Add(new ComboBoxItem { Content = year });
                    }
                    
                    SchoolYearFilter.SelectedIndex = 0;
                }

                // Populate Subject filter
                if (SubjectAnalyticsFilter != null)
                {
                    SubjectAnalyticsFilter.Items.Clear();
                    SubjectAnalyticsFilter.Items.Add(new ComboBoxItem { Content = "All Subjects" });
                    
                    var subjects = _availableExams
                        .Select(e => e.Subject)
                        .Where(s => !string.IsNullOrWhiteSpace(s))
                        .Distinct()
                        .OrderBy(s => s)
                        .ToList();
                    
                    foreach (var subject in subjects)
                    {
                        SubjectAnalyticsFilter.Items.Add(new ComboBoxItem { Content = subject });
                    }
                    
                    SubjectAnalyticsFilter.SelectedIndex = 0;
                }

                // Populate Exam filter
                if (ExamAnalyticsFilter != null)
                {
                    ExamAnalyticsFilter.Items.Clear();
                    ExamAnalyticsFilter.Items.Add(new ComboBoxItem { Content = "All Exams" });
                    
                    foreach (var exam in _availableExams.OrderByDescending(e => e.PublishedDate))
                    {
                        ExamAnalyticsFilter.Items.Add(new ComboBoxItem 
                        { 
                            Content = exam.Title,
                            Tag = exam.Id
                        });
                    }
                    
                    ExamAnalyticsFilter.SelectedIndex = _availableExams.Any() ? 1 : 0;
                }
            });
        }

        private async Task ShowEmptyAnalyticsStateAsync()
        {
            try
            {
                _currentExamId = string.Empty;
                _availableExams = new List<PublishedExam>();

                // Set filters to indicate no data
                await Dispatcher.InvokeAsync(() =>
                {
                    ExamAnalyticsFilter?.Items.Clear();
                    ExamAnalyticsFilter?.Items.Add(new ComboBoxItem { Content = "No data yet" });
                    if (ExamAnalyticsFilter != null) ExamAnalyticsFilter.SelectedIndex = 0;

                    SubjectAnalyticsFilter?.Items.Clear();
                    SubjectAnalyticsFilter?.Items.Add(new ComboBoxItem { Content = "No data yet" });
                    if (SubjectAnalyticsFilter != null) SubjectAnalyticsFilter.SelectedIndex = 0;
                });

                // Update metrics to zeros
                await UpdateClassMetricsDisplay(new ExamForge.Services.ClassOverviewMetrics());
                DrawScoreHistogram();
                await Dispatcher.InvokeAsync(() => UpdateScoreStatistics(new List<double>()));
            }
            catch (Exception ex)
            {
                Debug.WriteLine($"Error setting empty analytics state: {ex.Message}");
            }
        }

        private async Task LoadDemoClassOverview()
        {
            try
            {
                var demoMetrics = new ExamForge.Services.ClassOverviewMetrics
                {
                    ClassAverage = 82.5,
                    PassRate = 85.0,
                    CompletionRate = 96.0,
                    TotalStudents = 25,
                    PassingStudents = 21
                };

                await UpdateClassMetricsDisplay(demoMetrics);
            }
            catch (Exception ex)
            {
                Debug.WriteLine($"Error loading demo class overview: {ex.Message}");
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
                if (IntegrityPanel != null) IntegrityPanel.Visibility = Visibility.Collapsed;
                if (ExamineeDataPanel != null) ExamineeDataPanel.Visibility = Visibility.Collapsed;

                // Show selected panel and load fresh data
                if (ClassOverviewTab?.IsChecked == true && ClassOverviewPanel != null)
                {
                    ClassOverviewPanel.Visibility = Visibility.Visible;
                    Task.Run(async () =>
                    {
                        await LoadClassOverviewDataAsync();
                        Dispatcher.Invoke(() => DrawScoreHistogram());
                    });
                }
                else if (ItemAnalysisTab?.IsChecked == true && ItemAnalysisPanel != null)
                {
                    ItemAnalysisPanel.Visibility = Visibility.Visible;
                    Task.Run(LoadItemAnalysisDataAsync);
                }
                else if (IntegrityTab?.IsChecked == true && IntegrityPanel != null)
                {
                    IntegrityPanel.Visibility = Visibility.Visible;
                    Task.Run(LoadIntegrityDataAsync);
                }
                else if (ExamineeDataTab?.IsChecked == true && ExamineeDataPanel != null)
                {
                    ExamineeDataPanel.Visibility = Visibility.Visible;
                    Task.Run(LoadExamineeDataAsync);
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
                await UpdateClassMetricsDisplay(new ExamForge.Services.ClassOverviewMetrics());
            }
        }

        private async Task UpdateClassMetricsDisplay(ExamForge.Services.ClassOverviewMetrics metrics)
        {
            try
            {
                // Ensure UI updates happen on UI thread
                await Dispatcher.InvokeAsync(async () =>
                {
                    try
                    {
                        ClassAverageMetricText.Text = $"{metrics.ClassAverage:F1}%";
                        PassRateMetricText.Text = $"{metrics.PassRate:F0}%";
                        CompletionRateMetricText.Text = $"{metrics.CompletionRate:F0}%";

                        var totalStudents = metrics.TotalStudents;
                        var passingStudents = metrics.PassingStudents;
                        var completedStudents = totalStudents > 0
                            ? (int)Math.Round((metrics.CompletionRate / 100.0) * totalStudents)
                            : 0;

                        if (ClassAverageDetailText != null)
                            ClassAverageDetailText.Text = "Current exam average";

                        if (PassRateDetailText != null)
                            PassRateDetailText.Text = $"{passingStudents} of {totalStudents} students";

                        if (CompletionRateDetailText != null)
                            CompletionRateDetailText.Text = $"{completedStudents} of {totalStudents} submitted";

                        try
                        {
                            var incidents = await _firestoreService.GetIntegrityIncidentsAsync(_currentExamId);
                            var flaggedRate = metrics.TotalStudents > 0
                                ? (incidents.Count / (double)metrics.TotalStudents) * 100
                                : 0;
                            FlaggedRateMetricText.Text = $"{flaggedRate:F1}%";

                            if (FlaggedRateDetailText != null)
                                FlaggedRateDetailText.Text = $"{incidents.Count} integrity incidents";
                        }
                        catch (Exception ex)
                        {
                            Debug.WriteLine($"Error getting integrity incidents: {ex.Message}");
                            FlaggedRateMetricText.Text = "0.0%";
                            if (FlaggedRateDetailText != null)
                                FlaggedRateDetailText.Text = "0 integrity incidents";
                        }

                        // Update Quick Insights with real data
                        await UpdateQuickInsights();
                    }
                    catch (Exception ex)
                    {
                        Debug.WriteLine($"Error updating UI on main thread: {ex.Message}");
                    }
                });
            }
            catch (Exception ex)
            {
                Debug.WriteLine($"Error updating metrics display: {ex.Message}");
            }
        }

        private async Task UpdateQuickInsights()
        {
            try
            {
                if (string.IsNullOrEmpty(_currentExamId)) return;

                // Get real insights data
                var insights = await GenerateQuickInsights(_currentExamId);

                // Find Quick Insights panel and update content
                // var quickInsightsPanel = QuickInsightsPanel; // Removed as Quick Insights panel was removed from UI

                // Quick Insights panel was removed from UI
                /*
                if (quickInsightsPanel != null)
                {
                    // Clear existing content except title
                    quickInsightsPanel.Children.Clear();

                    // Add title
                    var titleBlock = new TextBlock
                    {
                        Text = "?? Quick Insights",
                        FontSize = 18,
                        FontWeight = FontWeights.SemiBold,
                        Foreground = new SolidColorBrush(Color.FromRgb(232, 234, 237)),
                        Margin = new Thickness(0, 0, 0, 16)
                    };
                    quickInsightsPanel.Children.Add(titleBlock);

                    // Most Missed Items
                    if (insights.MostMissedItems.Any())
                    {
                        var missedBorder = new Border
                        {
                            Background = new SolidColorBrush(Color.FromRgb(40, 37, 56)), // SurfaceLight
                            CornerRadius = new CornerRadius(8),
                            Padding = new Thickness(12),
                            Margin = new Thickness(0, 0, 0, 12)
                        };

                        var missedPanel = new StackPanel();
                        
                        var missedTitle = new TextBlock
                        {
                            Text = "Most Missed Items",
                            FontSize = 14,
                            FontWeight = FontWeights.Medium,
                            Foreground = new SolidColorBrush(Color.FromRgb(232, 234, 237)),
                            Margin = new Thickness(0, 0, 0, 8)
                        };
                        missedPanel.Children.Add(missedTitle);

                        foreach (var item in insights.MostMissedItems.Take(3))
                        {
                            var itemText = new TextBlock
                            {
                                Text = $"Q{item.QuestionNumber}: {item.Topic} ({item.CorrectPercentage:F0}% correct)",
                                FontSize = 12,
                                Foreground = new SolidColorBrush(Color.FromRgb(239, 68, 68)), // ErrorRed
                                Margin = new Thickness(0, 0, 0, 4)
                            };
                            missedPanel.Children.Add(itemText);
                        }

                        missedBorder.Child = missedPanel;
                        quickInsightsPanel.Children.Add(missedBorder);
                    }

                    // Easiest Items
                    if (insights.EasiestItems.Any())
                    {
                        var easiestBorder = new Border
                        {
                            Background = new SolidColorBrush(Color.FromRgb(40, 37, 56)), // SurfaceLight
                            CornerRadius = new CornerRadius(8),
                            Padding = new Thickness(12),
                            Margin = new Thickness(0, 0, 0, 12)
                        };

                        var easiestPanel = new StackPanel();
                        
                        var easiestTitle = new TextBlock
                        {
                            Text = "Easiest Items",
                            FontSize = 14,
                            FontWeight = FontWeights.Medium,
                            Foreground = new SolidColorBrush(Color.FromRgb(232, 234, 237)),
                            Margin = new Thickness(0, 0, 0, 8)
                        };
                        easiestPanel.Children.Add(easiestTitle);

                        foreach (var item in insights.EasiestItems.Take(3))
                        {
                            var itemText = new TextBlock
                            {
                                Text = $"Q{item.QuestionNumber}: {item.Topic} ({item.CorrectPercentage:F0}% correct)",
                                FontSize = 12,
                                Foreground = new SolidColorBrush(Color.FromRgb(16, 185, 129)), // SuccessGreen
                                Margin = new Thickness(0, 0, 0, 4)
                            };
                            easiestPanel.Children.Add(itemText);
                        }

                        easiestBorder.Child = easiestPanel;
                        quickInsightsPanel.Children.Add(easiestBorder);
                    }

                    // At-Risk Students
                    if (insights.AtRiskStudents.Any())
                    {
                        var riskBorder = new Border
                        {
                            Background = new SolidColorBrush(Color.FromRgb(40, 37, 56)), // SurfaceLight
                            CornerRadius = new CornerRadius(8),
                            Padding = new Thickness(12)
                        };

                        var riskPanel = new StackPanel();
                        
                        var riskTitle = new TextBlock
                        {
                            Text = "At-Risk Students",
                            FontSize = 14,
                            FontWeight = FontWeights.Medium,
                            Foreground = new SolidColorBrush(Color.FromRgb(232, 234, 237)),
                            Margin = new Thickness(0, 0, 0, 8)
                        };
                        riskPanel.Children.Add(riskTitle);

                        foreach (var student in insights.AtRiskStudents.Take(3))
                        {
                            var studentText = new TextBlock
                            {
                                Text = $"{student.Name} ({student.Percentage:F0}%)",
                                FontSize = 12,
                                Foreground = new SolidColorBrush(Color.FromRgb(251, 191, 36)), // WarningYellow
                                Margin = new Thickness(0, 0, 0, 4)
                            };
                            riskPanel.Children.Add(studentText);
                        }

                        riskBorder.Child = riskPanel;
                        quickInsightsPanel.Children.Add(riskBorder);
                    }
                }
                */ // End of commented Quick Insights code
            }
            catch (Exception ex)
            {
                Debug.WriteLine($"Error updating quick insights: {ex.Message}");
            }
        }

        private async Task<QuickInsightsData> GenerateQuickInsights(string examId)
        {
            try
            {
                var insights = new QuickInsightsData();
                
                // Get exam submissions and content
                var submissions = await _firestoreService.GetExamSubmissionsAsync(examId);
                var exam = _availableExams.FirstOrDefault(e => e.Id == examId);
                
                if (!submissions.Any() || exam?.Contents == null)
                    return insights;

                // Calculate most missed items
                var questionStats = exam.Contents.Select((q, index) =>
                {
                    var questionNumber = index + 1;
                    var responses = submissions.SelectMany(s => s.Responses)
                                              .Where(r =>
                                                  r.QuestionId == q.Id ||
                                                  r.QuestionNumber == questionNumber ||
                                                  string.Equals(r.QuestionId, $"question{questionNumber}", StringComparison.OrdinalIgnoreCase))
                                              .ToList();
                    var correctCount = responses.Count(r => r.IsCorrect);
                    var totalCount = responses.Count;
                    var correctPercentage = totalCount > 0 ? (correctCount * 100.0) / totalCount : 0;

                    return new QuestionInsight
                    {
                        QuestionNumber = questionNumber,
                        Topic = GetTopicFromQuestionId(q.Id),
                        CorrectPercentage = correctPercentage,
                        QuestionId = q.Id
                    };
                }).ToList();

                // Most missed items (lowest correct percentage)
                insights.MostMissedItems = questionStats
                    .Where(q => q.CorrectPercentage < 60)
                    .OrderBy(q => q.CorrectPercentage)
                    .Take(5)
                    .ToList();

                // Easiest items (highest correct percentage)
                insights.EasiestItems = questionStats
                    .Where(q => q.CorrectPercentage > 80)
                    .OrderByDescending(q => q.CorrectPercentage)
                    .Take(5)
                    .ToList();

                // At-risk students (below 60% overall)
                insights.AtRiskStudents = submissions
                    .Select(s => new StudentInsight
                    {
                        Name = s.StudentName,
                        Percentage = s.TotalPossiblePoints > 0 ? (s.TotalScore / s.TotalPossiblePoints) * 100 : 0,
                        StudentId = s.StudentId
                    })
                    .Where(s => s.Percentage < 60)
                    .OrderBy(s => s.Percentage)
                    .Take(5)
                    .ToList();

                return insights;
            }
            catch (Exception ex)
            {
                Debug.WriteLine($"Error generating quick insights: {ex.Message}");
                return new QuickInsightsData();
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
                        if (!submissions.Any())
                        {
                            Dispatcher.Invoke(() => UpdateScoreStatistics(new List<double>()));
                            return;
                        }

                        var scores = submissions.Select(s => s.TotalPossiblePoints > 0 ? 
                            (s.TotalScore / s.TotalPossiblePoints) * 100 : 0).ToList();

                        // Create score distribution bins
                        var scoreBins = new List<ScoreBin>();
                        for (int i = 0; i < 10; i++)
                        {
                            var min = i * 10;
                            var max = (i + 1) * 10;
                            var count = i == 9
                                ? scores.Count(s => s >= min && s <= max)
                                : scores.Count(s => s >= min && s < max);
                            
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
                        Dispatcher.Invoke(() =>
                        {
                            DrawHistogramBars(scoreBins);
                            UpdateScoreStatistics(scores);
                        });
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

        private void UpdateScoreStatistics(List<double> scores)
        {
            if (scores == null || scores.Count == 0)
            {
                MeanStatText.Text = "Mean: 0.0%";
                MedianStatText.Text = "Median: 0.0%";
                StdDevStatText.Text = "Std Dev: 0.0";
                RangeStatText.Text = "Range: 0-0%";
                Q1StatText.Text = "Q1: 0.0%";
                Q3StatText.Text = "Q3: 0.0%";
                return;
            }

            var ordered = scores.OrderBy(s => s).ToList();
            var mean = ordered.Average();
            var median = Percentile(ordered, 0.5);
            var q1 = Percentile(ordered, 0.25);
            var q3 = Percentile(ordered, 0.75);
            var min = ordered.First();
            var max = ordered.Last();

            var variance = ordered.Average(v => Math.Pow(v - mean, 2));
            var stdDev = Math.Sqrt(variance);

            MeanStatText.Text = $"Mean: {mean:F1}%";
            MedianStatText.Text = $"Median: {median:F1}%";
            StdDevStatText.Text = $"Std Dev: {stdDev:F1}";
            RangeStatText.Text = $"Range: {min:F0}-{max:F0}%";
            Q1StatText.Text = $"Q1: {q1:F1}%";
            Q3StatText.Text = $"Q3: {q3:F1}%";
        }

        private static double Percentile(List<double> sortedValues, double percentile)
        {
            if (sortedValues.Count == 0) return 0;
            if (sortedValues.Count == 1) return sortedValues[0];

            var position = (sortedValues.Count - 1) * percentile;
            var lower = (int)Math.Floor(position);
            var upper = (int)Math.Ceiling(position);

            if (lower == upper) return sortedValues[lower];

            var weight = position - lower;
            return sortedValues[lower] + (sortedValues[upper] - sortedValues[lower]) * weight;
        }

        private void DrawHistogramBars(List<ScoreBin> scoreBins)
        {
            try
            {
                if (ScoreHistogram == null) return;

                if (ScoreHistogram.ActualWidth <= 0 || ScoreHistogram.ActualHeight <= 0)
                {
                    Dispatcher.BeginInvoke(() => DrawHistogramBars(scoreBins), DispatcherPriority.Loaded);
                    return;
                }

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

        #endregion

        #region Integrity & Incidents

        private async Task LoadIntegrityDataAsync()
        {
            try
            {
                if (string.IsNullOrEmpty(_currentExamId))
                    return;

                var incidents = await _firestoreService.GetIntegrityIncidentsAsync(_currentExamId);
                
                // Convert to UI model
                var incidentData = incidents.Select(incident => new IntegrityIncidentData
                {
                    Id = incident.Id,
                    Timestamp = incident.Timestamp,
                    StudentName = incident.StudentName,
                    EventType = incident.IncidentType,
                    Severity = incident.Severity,
                    Details = incident.Details ?? "",
                    IpAddress = "192.168.1.xxx"
                }).OrderByDescending(i => i.Timestamp).ToList();

                await Dispatcher.InvokeAsync(() =>
                {
                    if (IntegrityIncidentsGrid != null)
                        IntegrityIncidentsGrid.ItemsSource = incidentData;
                        
                    UpdateIntegritySummary(incidents);
                });
            }
            catch (Exception ex)
            {
                Debug.WriteLine($"Error loading integrity data: {ex.Message}");
            }
        }

        private void UpdateIntegritySummary(List<IntegrityIncident> incidents)
        {
            try
            {
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
                    ShowError("Select an exam first.");
                    return;
                }

                var exam = _availableExams.FirstOrDefault(e => e.Id == _currentExamId);
                var exportService = new ExportService();
                var path = exportService.ExportIntegrityReportAsync(_currentExamId, exam?.Title ?? "Exam").Result;
                MessageBox.Show($"Integrity report exported to: {path}", "Success", MessageBoxButton.OK, MessageBoxImage.Information);
            }
            catch (Exception ex)
            {
                ShowError($"Failed to export integrity report: {ex.Message}");
            }
        }

        private void ReviewIncident_Click(object sender, RoutedEventArgs e)
        {
            if (sender is Button btn && btn.Tag is string incidentId)
            {
                MessageBox.Show($"Review incident {incidentId} functionality coming soon!", "Info", 
                    MessageBoxButton.OK, MessageBoxImage.Information);
            }
        }

        #endregion

        #region Examinee Data

        private List<ExamineeDataByExam> _allExamineeData = new();
        private List<ExamineeDataByExam> _filteredExamineeData = new();

        private async Task LoadExamineeDataAsync()
        {
            try
            {
                await Dispatcher.InvokeAsync(() =>
                {
                    ExamineeDataEmptyState.Visibility = Visibility.Collapsed;
                });

                // Get all published exams for the current user
                var allExams = await _firestoreService.GetAllPublishedExamsAsync();
                _allExamineeData.Clear();

                foreach (var exam in allExams.OrderBy(e => e.Title))
                {
                    try
                    {
                        // Get submissions for this exam
                        var submissions = await _firestoreService.GetExamSubmissionsAsync(exam.Id);
                        
                        if (submissions.Any())
                        {
                            var examinees = submissions.Select(submission => new ExamineeData
                            {
                                Id = submission.Id,
                                StudentName = submission.StudentName,
                                Email = GetStudentEmail(submission.StudentId), // You may need to implement this
                                Score = submission.TotalPossiblePoints > 0 
                                    ? (submission.TotalScore / submission.TotalPossiblePoints) * 100 
                                    : 0,
                                Status = GetExamineeStatus(submission),
                                StartTime = submission.StartTime,
                                EndTime = submission.EndTime,
                                TimeTaken = GetTimeTaken(submission.StartTime, submission.EndTime),
                                ExamId = exam.Id
                            }).OrderBy(e => e.StudentName).ToList();

                            _allExamineeData.Add(new ExamineeDataByExam
                            {
                                ExamId = exam.Id,
                                ExamTitle = exam.Title,
                                ExamineeCount = examinees.Count,
                                Examinees = examinees
                            });
                        }
                    }
                    catch (Exception ex)
                    {
                        Debug.WriteLine($"Error loading examinee data for exam {exam.Title}: {ex.Message}");
                    }
                }

                // Update filtered data and UI
                _filteredExamineeData = new List<ExamineeDataByExam>(_allExamineeData);
                await UpdateExamineeDataDisplay();
            }
            catch (Exception ex)
            {
                Debug.WriteLine($"Error loading examinee data: {ex.Message}");
                await Dispatcher.InvokeAsync(() =>
                {
                    ExamineeDataEmptyState.Visibility = Visibility.Visible;
                });
            }
        }

        private async Task UpdateExamineeDataDisplay()
        {
            try
            {
                await Dispatcher.InvokeAsync(() =>
                {
                    if (_filteredExamineeData.Any())
                    {
                        ExamineeDataContainer.ItemsSource = null;
                        ExamineeDataContainer.ItemsSource = _filteredExamineeData;
                        ExamineeDataEmptyState.Visibility = Visibility.Collapsed;
                    }
                    else
                    {
                        ExamineeDataContainer.ItemsSource = null;
                        ExamineeDataEmptyState.Visibility = Visibility.Visible;
                    }
                });
            }
            catch (Exception ex)
            {
                Debug.WriteLine($"Error updating examinee data display: {ex.Message}");
            }
        }

        private string GetStudentEmail(string studentId)
        {
            // In a real implementation, you would look up the student's email
            // For now, return a placeholder
            return $"{studentId.Substring(0, Math.Min(8, studentId.Length))}@student.edu";
        }

        private string GetExamineeStatus(ExamSubmission submission)
        {
            if (!submission.StartTime.HasValue)
                return "Not Started";
            
            if (submission.StartTime.HasValue && !submission.EndTime.HasValue)
                return "In Progress";
                
            if (submission.EndTime.HasValue)
                return "Completed";
                
            return "Unknown";
        }

        private string GetTimeTaken(DateTime? startTime, DateTime? endTime)
        {
            if (startTime.HasValue && endTime.HasValue && endTime > startTime)
            {
                var duration = endTime.Value - startTime.Value;
                return $"{duration.Hours:D2}:{duration.Minutes:D2}:{duration.Seconds:D2}";
            }
            
            return "N/A";
        }

        #endregion

        #region Examinee Data Events

        private void ExamineeSearchBox_GotFocus(object sender, RoutedEventArgs e)
        {
            if (ExamineeSearchBox.Text == "Search examinees by name, exam, or email...")
            {
                ExamineeSearchBox.Text = "";
                ExamineeSearchBox.Foreground = new SolidColorBrush(Color.FromRgb(232, 234, 237));
            }
        }

        private void ExamineeSearchBox_LostFocus(object sender, RoutedEventArgs e)
        {
            if (string.IsNullOrWhiteSpace(ExamineeSearchBox.Text))
            {
                ExamineeSearchBox.Text = "Search examinees by name, exam, or email...";
                ExamineeSearchBox.Foreground = new SolidColorBrush(Color.FromRgb(156, 163, 175));
            }
        }

        private async void ExamineeSearchBox_TextChanged(object sender, TextChangedEventArgs e)
        {
            await FilterExamineeData();
        }

        private async void SearchExaminees_Click(object sender, RoutedEventArgs e)
        {
            await FilterExamineeData();
        }

        private async Task FilterExamineeData()
        {
            try
            {
                var searchText = ExamineeSearchBox.Text?.ToLower() ?? "";
                
                if (string.IsNullOrWhiteSpace(searchText) || searchText == "search examinees by name, exam, or email...")
                {
                    _filteredExamineeData = new List<ExamineeDataByExam>(_allExamineeData);
                }
                else
                {
                    _filteredExamineeData = _allExamineeData
                        .Select(examGroup => new ExamineeDataByExam
                        {
                            ExamId = examGroup.ExamId,
                            ExamTitle = examGroup.ExamTitle,
                            Examinees = examGroup.Examinees
                                .Where(examinee => 
                                    examinee.StudentName.ToLower().Contains(searchText) ||
                                    examinee.Email.ToLower().Contains(searchText) ||
                                    examGroup.ExamTitle.ToLower().Contains(searchText))
                                .ToList(),
                            ExamineeCount = 0 // Will be updated below
                        })
                        .Where(examGroup => examGroup.Examinees.Any())
                        .ToList();

                    // Update examinee counts
                    foreach (var examGroup in _filteredExamineeData)
                    {
                        examGroup.ExamineeCount = examGroup.Examinees.Count;
                    }
                }

                await UpdateExamineeDataDisplay();
            }
            catch (Exception ex)
            {
                Debug.WriteLine($"Error filtering examinee data: {ex.Message}");
            }
        }

        private async void ViewExamineeDetails_Click(object sender, RoutedEventArgs e)
        {
            try
            {
                if (sender is Button btn && btn.Tag is string submissionId)
                {
                    // Find the submission details
                    var examineeData = _filteredExamineeData
                        .SelectMany(e => e.Examinees)
                        .FirstOrDefault(e => e.Id == submissionId);

                    if (examineeData != null)
                    {
                        // Get the full submission data
                        var submissions = await _firestoreService.GetExamSubmissionsAsync(examineeData.ExamId);
                        var submission = submissions.FirstOrDefault(s => s.Id == submissionId);
                        
                        if (submission != null)
                        {
                            // Get the exam data
                            var exam = _availableExams.FirstOrDefault(e => e.Id == examineeData.ExamId);
                            
                            if (exam != null)
                            {
                                // Show detailed submission view
                                var dialog = new Views.ViewSubmissionDialog(submission, exam)
                                {
                                    Owner = Window.GetWindow(this)
                                };
                                dialog.ShowDialog();
                            }
                            else
                            {
                                ShowError("Exam data not found.");
                            }
                        }
                        else
                        {
                            ShowError("Submission data not found.");
                        }
                    }
                }
            }
            catch (Exception ex)
            {
                ShowError($"Failed to view examinee details: {ex.Message}");
                Debug.WriteLine($"Error viewing examinee details: {ex.Message}");
            }
        }

        private async void ExportExamineeData_Click(object sender, RoutedEventArgs e)
        {
            try
            {
                if (sender is Button btn && btn.Tag is string submissionId)
                {
                    // Find the examinee data
                    var examineeData = _filteredExamineeData
                        .SelectMany(e => e.Examinees)
                        .FirstOrDefault(e => e.Id == submissionId);

                    if (examineeData != null)
                    {
                        // Get the full submission data
                        var submissions = await _firestoreService.GetExamSubmissionsAsync(examineeData.ExamId);
                        var submission = submissions.FirstOrDefault(s => s.Id == submissionId);
                        
                        if (submission != null)
                        {
                            // Get the exam data
                            var exam = _availableExams.FirstOrDefault(e => e.Id == examineeData.ExamId);
                            
                            if (exam != null)
                            {
                                var exportService = new ExportService();
                                var path = await exportService.ExportStudentReportAsync(
                                    examineeData.StudentName, 
                                    exam.Title,
                                    submission,
                                    exam);
                                
                                MessageBox.Show($"Examinee report exported to: {path}", "Success", 
                                    MessageBoxButton.OK, MessageBoxImage.Information);
                            }
                            else
                            {
                                ShowError("Exam data not found.");
                            }
                        }
                        else
                        {
                            ShowError("Submission data not found.");
                        }
                    }
                }
            }
            catch (Exception ex)
            {
                ShowError($"Failed to export examinee data: {ex.Message}");
                Debug.WriteLine($"Error exporting examinee data: {ex.Message}");
            }
        }

        #endregion

        #region Item Analysis

        private List<ItemAnalysisResult> _itemAnalysisData = new();

        private async Task LoadItemAnalysisDataAsync()
        {
            try
            {
                if (string.IsNullOrEmpty(_currentExamId))
                {
                    await ShowItemAnalysisEmptyState();
                    return;
                }

                // Get exam submissions for analysis
                var submissions = await _firestoreService.GetExamSubmissionsAsync(_currentExamId);
                var exam = _availableExams.FirstOrDefault(e => e.Id == _currentExamId);

                if (submissions.Any() && exam != null)
                {
                    // Calculate item analysis for each question
                    _itemAnalysisData = CalculateItemAnalysis(submissions, exam);
                    
                    await Dispatcher.InvokeAsync(() =>
                    {
                        UpdateItemAnalysisDisplay();
                        DrawItemAnalysisChart();
                    });
                }
                else
                {
                    await ShowItemAnalysisEmptyState();
                }
            }
            catch (Exception ex)
            {
                Debug.WriteLine($"Error loading item analysis data: {ex.Message}");
                await ShowItemAnalysisEmptyState();
            }
        }

        private List<ItemAnalysisResult> CalculateItemAnalysis(List<ExamSubmission> submissions, PublishedExam exam)
        {
            var results = new List<ItemAnalysisResult>();
            int questionNumber = 1;

            foreach (var content in exam.Contents)
            {
                // Get all responses for this question
                var responses = submissions
                    .SelectMany(s => s.Responses)
                    .Where(r =>
                        r.QuestionId == content.ContentId ||
                        r.QuestionNumber == questionNumber ||
                        string.Equals(r.QuestionId, $"question{questionNumber}", StringComparison.OrdinalIgnoreCase))
                    .ToList();

                if (responses.Any())
                {
                    var totalCount = responses.Count;
                    var correctCount = responses.Count(r => r.IsCorrect);
                    var difficultyIndex = (correctCount * 100.0) / totalCount;
                    
                    // Classify based on difficulty index
                    string classification = ClassifyDifficulty(difficultyIndex);

                    results.Add(new ItemAnalysisResult
                    {
                        QuestionId = content.ContentId,
                        QuestionNumber = questionNumber,
                        QuestionText = content.Question,
                        TotalCount = totalCount,
                        CorrectCount = correctCount,
                        DifficultyIndex = difficultyIndex,
                        Classification = classification
                    });
                }
                
                questionNumber++;
            }

            return results.OrderBy(r => r.QuestionNumber).ToList();
        }

        private string ClassifyDifficulty(double difficultyIndex)
        {
            if (difficultyIndex >= 70.0)
                return "Easy";
            else if (difficultyIndex >= 30.0)
                return "Moderate";
            else
                return "Difficult";
        }

        private void UpdateItemAnalysisDisplay()
        {
            try
            {
                // Update summary cards
                var easyCount = _itemAnalysisData.Count(i => i.Classification == "Easy");
                var moderateCount = _itemAnalysisData.Count(i => i.Classification == "Moderate");
                var difficultCount = _itemAnalysisData.Count(i => i.Classification == "Difficult");
                var avgDifficulty = _itemAnalysisData.Any() ? _itemAnalysisData.Average(i => i.DifficultyIndex) : 0;

                EasyItemsCount.Text = easyCount.ToString();
                ModerateItemsCount.Text = moderateCount.ToString();
                DifficultItemsCount.Text = difficultCount.ToString();
                AverageDifficultyIndex.Text = $"{avgDifficulty:F1}%";

                // Update data grid
                ItemAnalysisGrid.ItemsSource = _itemAnalysisData;
            }
            catch (Exception ex)
            {
                Debug.WriteLine($"Error updating item analysis display: {ex.Message}");
            }
        }

        private void DrawItemAnalysisChart()
        {
            try
            {
                if (ItemAnalysisChart == null || !_itemAnalysisData.Any()) return;

                ItemAnalysisChart.Children.Clear();

                double width = ItemAnalysisChart.ActualWidth > 0 ? ItemAnalysisChart.ActualWidth : 400;
                double height = ItemAnalysisChart.ActualHeight > 0 ? ItemAnalysisChart.ActualHeight : 300;
                double margin = 40;
                double chartWidth = width - (margin * 2);
                double chartHeight = height - (margin * 2);

                // Draw axes
                var xAxis = new Line
                {
                    X1 = margin,
                    Y1 = height - margin,
                    X2 = width - margin,
                    Y2 = height - margin,
                    Stroke = new SolidColorBrush(Color.FromRgb(156, 163, 175)),
                    StrokeThickness = 2
                };
                ItemAnalysisChart.Children.Add(xAxis);

                var yAxis = new Line
                {
                    X1 = margin,
                    Y1 = margin,
                    X2 = margin,
                    Y2 = height - margin,
                    Stroke = new SolidColorBrush(Color.FromRgb(156, 163, 175)),
                    StrokeThickness = 2
                };
                ItemAnalysisChart.Children.Add(yAxis);

                // Draw bars for each question
                if (_itemAnalysisData.Any())
                {
                    double barWidth = chartWidth / _itemAnalysisData.Count - 4;
                    
                    for (int i = 0; i < _itemAnalysisData.Count; i++)
                    {
                        var item = _itemAnalysisData[i];
                        double barHeight = (item.DifficultyIndex / 100.0) * chartHeight;
                        
                        // Choose color based on classification
                        Color barColor = item.Classification switch
                        {
                            "Easy" => Color.FromRgb(16, 185, 129),      // Green
                            "Moderate" => Color.FromRgb(59, 130, 246), // Blue
                            "Difficult" => Color.FromRgb(239, 68, 68), // Red
                            _ => Color.FromRgb(156, 163, 175)          // Gray
                        };

                        var bar = new Rectangle
                        {
                            Width = barWidth,
                            Height = barHeight,
                            Fill = new SolidColorBrush(barColor)
                        };

                        double x = margin + (i * (barWidth + 4));
                        Canvas.SetLeft(bar, x);
                        Canvas.SetTop(bar, height - margin - barHeight);
                        ItemAnalysisChart.Children.Add(bar);

                        // Add question number label
                        var label = new TextBlock
                        {
                            Text = $"Q{item.QuestionNumber}",
                            FontSize = 10,
                            Foreground = new SolidColorBrush(Color.FromRgb(107, 114, 128))
                        };
                        Canvas.SetLeft(label, x + (barWidth / 2) - 10);
                        Canvas.SetTop(label, height - margin + 5);
                        ItemAnalysisChart.Children.Add(label);

                        // Add percentage label
                        var percentLabel = new TextBlock
                        {
                            Text = $"{item.DifficultyIndex:F1}%",
                            FontSize = 9,
                            FontWeight = FontWeights.Bold,
                            Foreground = new SolidColorBrush(barColor)
                        };
                        Canvas.SetLeft(percentLabel, x + (barWidth / 2) - 15);
                        Canvas.SetTop(percentLabel, height - margin - barHeight - 20);
                        ItemAnalysisChart.Children.Add(percentLabel);
                    }
                }

                // Add Y-axis labels (0%, 50%, 100%)
                for (int i = 0; i <= 4; i++)
                {
                    double y = height - margin - (i * chartHeight / 4);
                    var gridLine = new Line
                    {
                        X1 = margin - 5,
                        Y1 = y,
                        X2 = margin,
                        Y2 = y,
                        Stroke = new SolidColorBrush(Color.FromRgb(156, 163, 175)),
                        StrokeThickness = 1
                    };
                    ItemAnalysisChart.Children.Add(gridLine);

                    var yLabel = new TextBlock
                    {
                        Text = $"{i * 25}%",
                        FontSize = 10,
                        Foreground = new SolidColorBrush(Color.FromRgb(107, 114, 128))
                    };
                    Canvas.SetLeft(yLabel, margin - 35);
                    Canvas.SetTop(yLabel, y - 8);
                    ItemAnalysisChart.Children.Add(yLabel);
                }
            }
            catch (Exception ex)
            {
                Debug.WriteLine($"Error drawing item analysis chart: {ex.Message}");
            }
        }

        private async Task ShowItemAnalysisEmptyState()
        {
            await Dispatcher.InvokeAsync(() =>
            {
                EasyItemsCount.Text = "0";
                ModerateItemsCount.Text = "0";
                DifficultItemsCount.Text = "0";
                AverageDifficultyIndex.Text = "0%";
                ItemAnalysisGrid.ItemsSource = null;
                ItemAnalysisChart.Children.Clear();
            });
        }

        #endregion

        #region Item Analysis Events

        private async void ExportItemAnalysis_Click(object sender, RoutedEventArgs e)
        {
            try
            {
                if (!_itemAnalysisData.Any())
                {
                    ShowInfo("No item analysis data available to export.");
                    return;
                }

                var exam = _availableExams.FirstOrDefault(e => e.Id == _currentExamId);
                var exportService = new ExportService();
                
                // Convert to export format
                var exportData = _itemAnalysisData.Select(item => new Dictionary<string, string>
                {
                    ["Question #"] = item.QuestionNumber.ToString(),
                    ["Question Text"] = item.QuestionText,
                    ["Total Responses"] = item.TotalCount.ToString(),
                    ["Correct Responses"] = item.CorrectCount.ToString(),
                    ["Difficulty Index"] = $"{item.DifficultyIndex:F2}%",
                    ["Classification"] = item.Classification
                }).ToList();

                var fileName = $"ItemAnalysis_{exam?.Title?.Replace(" ", "_") ?? "Exam"}_{DateTime.Now:yyyyMMdd_HHmmss}.csv";
                var path = await exportService.ExportToCsvAsync(exportData, fileName);
                
                MessageBox.Show($"Item analysis exported to: {path}", "Export Complete", 
                    MessageBoxButton.OK, MessageBoxImage.Information);
            }
            catch (Exception ex)
            {
                ShowError($"Failed to export item analysis: {ex.Message}");
                Debug.WriteLine($"Error exporting item analysis: {ex.Message}");
            }
        }

        private void ViewQuestionDetails_Click(object sender, RoutedEventArgs e)
        {
            try
            {
                if (sender is Button btn && btn.Tag is string questionId)
                {
                    var itemData = _itemAnalysisData.FirstOrDefault(i => i.QuestionId == questionId);
                    if (itemData != null)
                    {
                        var message = $"Question {itemData.QuestionNumber} Details:\n\n" +
                                    $"Question: {itemData.QuestionText}\n\n" +
                                    $"Total Responses: {itemData.TotalCount}\n" +
                                    $"Correct Responses: {itemData.CorrectCount}\n" +
                                    $"Difficulty Index: {itemData.DifficultyIndex:F2}%\n" +
                                    $"Classification: {itemData.Classification}\n\n" +
                                    $"Recommendation: {GetRecommendation(itemData.Classification)}";

                        MessageBox.Show(message, "Question Details", MessageBoxButton.OK, MessageBoxImage.Information);
                    }
                }
            }
            catch (Exception ex)
            {
                ShowError($"Failed to load question details: {ex.Message}");
            }
        }

        private string GetRecommendation(string classification)
        {
            return classification switch
            {
                "Easy" => "Consider making this question more challenging or using it as a confidence builder.",
                "Moderate" => "This question effectively discriminates between students. Good item quality.",
                "Difficult" => "Review this question for clarity, errors, or consider if the material was adequately covered.",
                _ => "Unable to determine recommendation."
            };
        }

        #endregion

        #region Helper Methods

        private string GetTopicFromQuestionId(string questionId)
        {
            if (questionId.Contains("math", StringComparison.OrdinalIgnoreCase))
                return "Mathematics";
            if (questionId.Contains("sci", StringComparison.OrdinalIgnoreCase))
                return "Science";
            if (questionId.Contains("hist", StringComparison.OrdinalIgnoreCase))
                return "History";
            
            return "General";
        }

        private void ShowError(string message)
        {
            MessageBox.Show(message, "Error", MessageBoxButton.OK, MessageBoxImage.Error);
        }

        private void ShowInfo(string message)
        {
            MessageBox.Show(message, "Information", MessageBoxButton.OK, MessageBoxImage.Information);
        }

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

        #endregion
    }

    #region Data Models


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

    public class QuickInsightsData
    {
        public List<QuestionInsight> MostMissedItems { get; set; } = new();
        public List<QuestionInsight> EasiestItems { get; set; } = new();
        public List<StudentInsight> AtRiskStudents { get; set; } = new();
    }

    public class QuestionInsight
    {
        public int QuestionNumber { get; set; }
        public string Topic { get; set; } = "";
        public double CorrectPercentage { get; set; }
        public string QuestionId { get; set; } = "";
    }

    public class StudentInsight
    {
        public string Name { get; set; } = "";
        public double Percentage { get; set; }
        public string StudentId { get; set; } = "";
    }

    public class ExamineeDataByExam
    {
        public string ExamId { get; set; } = "";
        public string ExamTitle { get; set; } = "";
        public int ExamineeCount { get; set; }
        public List<ExamineeData> Examinees { get; set; } = new();
    }

    public class ExamineeData
    {
        public string Id { get; set; } = "";
        public string StudentName { get; set; } = "";
        public string Email { get; set; } = "";
        public double Score { get; set; }
        public string Status { get; set; } = "";
        public DateTime? StartTime { get; set; }
        public DateTime? EndTime { get; set; }
        public string TimeTaken { get; set; } = "";
        public string ExamId { get; set; } = "";
    }

    public class ItemAnalysisResult
    {
        public string QuestionId { get; set; } = "";
        public int QuestionNumber { get; set; }
        public string QuestionText { get; set; } = "";
        public int TotalCount { get; set; }
        public int CorrectCount { get; set; }
        public double DifficultyIndex { get; set; }
        public string Classification { get; set; } = "";
    }

    public class EnhancedClassStatistics
    {
        public double Mean { get; set; }
        public double Median { get; set; }
        public double StandardDeviation { get; set; }
        public ScoreRange Range { get; set; } = new();
        public Quartiles Quartiles { get; set; } = new();
        public List<ScoreBin> ScoreDistribution { get; set; } = new();
    }

    public class ScoreRange
    {
        public double Min { get; set; }
        public double Max { get; set; }
    }

    public class Quartiles
    {
        public double Q1 { get; set; }
        public double Q3 { get; set; }
    }

    #endregion
}