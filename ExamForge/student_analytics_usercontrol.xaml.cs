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
                else if (StudentDrilldownTab?.IsChecked == true)
                {
                    await LoadStudentDataAsync();
                    LoadStudentListAsync();
                }
                else if (IntegrityTab?.IsChecked == true)
                {
                    await LoadIntegrityDataAsync();
                }

                _lastDataRefresh = DateTime.UtcNow;
                Debug.WriteLine($"?? Analytics data refreshed at {_lastDataRefresh:HH:mm:ss}");
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
                    ShowInfo("No exams available to filter.");
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
                await LoadItemAnalysisDataAsync();
                await LoadStudentDataAsync();
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
                
                // Load data for the first available exam or show demo data
                if (_availableExams.Any())
                {
                    _currentExamId = _availableExams.First().Id;
                    await LoadClassOverviewDataAsync();
                    await LoadItemAnalysisDataAsync();
                    await LoadStudentDataAsync();
                    await LoadIntegrityDataAsync();
                    DrawScoreHistogram();
                    
                    _lastDataRefresh = DateTime.UtcNow;
                    Debug.WriteLine($"? Initial analytics data loaded at {_lastDataRefresh:HH:mm:ss}");
                }
                else
                {
                    // Show demo data when no exams exist
                    await LoadDemoDataAsync();
                    ShowInfo("No published exams found. Showing demo data. Please publish some exams to see real analytics.");
                }
            }
            catch (Exception ex)
            {
                Debug.WriteLine($"? Error loading analytics data: {ex.Message}");
                // Show demo data on error
                await LoadDemoDataAsync();
                ShowError($"Failed to load analytics data. Showing demo data. Error: {ex.Message}");
            }
        }

        private async Task LoadAvailableExamsAsync()
        {
            try
            {
                _availableExams = await _firestoreService.GetAllPublishedExamsAsync();
                await PopulateFiltersAsync();
                Debug.WriteLine($"? Loaded {_availableExams.Count} exams for analytics");
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

        private async Task LoadDemoDataAsync()
        {
            try
            {
                // Create demo published exam
                _availableExams = new List<PublishedExam>
                {
                    new PublishedExam
                    {
                        Id = "demo-exam-001",
                        Title = "Demo Mathematics Exam",
                        PublishedDate = DateTime.Now.AddDays(-1),
                        ExamUrl = "https://demo-exam.example.com"
                    }
                };
                _currentExamId = "demo-exam-001";

                // Update dropdown
                if (ExamAnalyticsFilter != null)
                {
                    ExamAnalyticsFilter.Items.Clear();
                    ExamAnalyticsFilter.Items.Add(new ComboBoxItem { Content = "All Exams" });
                    ExamAnalyticsFilter.Items.Add(new ComboBoxItem 
                    { 
                        Content = "Demo Mathematics Exam",
                        Tag = "demo-exam-001"
                    });
                    ExamAnalyticsFilter.SelectedIndex = 1;
                }

                // Load demo metrics
                await LoadDemoClassOverview();
                LoadDemoStudents();
                
                Debug.WriteLine("?? Demo data loaded successfully");
            }
            catch (Exception ex)
            {
                Debug.WriteLine($"Error loading demo data: {ex.Message}");
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

        private void LoadDemoStudents()
        {
            try
            {
                _allStudents = new List<StudentPerformanceData>
                {
                    new StudentPerformanceData { Id = "demo1", Name = "Alice Johnson", Score = 95, Percentage = 95.0, Rank = 1, TimeTaken = TimeSpan.FromMinutes(45), FlagCount = 0 },
                    new StudentPerformanceData { Id = "demo2", Name = "Bob Smith", Score = 87, Percentage = 87.0, Rank = 2, TimeTaken = TimeSpan.FromMinutes(52), FlagCount = 0 },
                    new StudentPerformanceData { Id = "demo3", Name = "Carol Davis", Score = 78, Percentage = 78.0, Rank = 3, TimeTaken = TimeSpan.FromMinutes(48), FlagCount = 1 }
                };
            }
            catch (Exception ex)
            {
                Debug.WriteLine($"Error loading demo students: {ex.Message}");
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
                else if (StudentDrilldownTab?.IsChecked == true && StudentDrilldownPanel != null)
                {
                    StudentDrilldownPanel.Visibility = Visibility.Visible;
                    Task.Run(async () =>
                    {
                        await LoadStudentDataAsync();
                        Dispatcher.Invoke(LoadStudentListAsync);
                    });
                }
                else if (IntegrityTab?.IsChecked == true && IntegrityPanel != null)
                {
                    IntegrityPanel.Visibility = Visibility.Visible;
                    Task.Run(LoadIntegrityDataAsync);
                }
                else if (TrendsTab?.IsChecked == true && TrendsPanel != null)
                {
                    TrendsPanel.Visibility = Visibility.Visible;
                    Dispatcher.BeginInvoke(new Action(DrawTrendChart));
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
                            try
                            {
                                var incidents = await _firestoreService.GetIntegrityIncidentsAsync(_currentExamId);
                                var flaggedRate = metrics.TotalStudents > 0 ? 
                                    (incidents.Count / (double)metrics.TotalStudents) * 100 : 0;
                                metricCards[3].Text = $"{flaggedRate:F1}%";
                            }
                            catch (Exception ex)
                            {
                                Debug.WriteLine($"Error getting integrity incidents: {ex.Message}");
                                metricCards[3].Text = "0.0%";
                            }
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
                var quickInsightsPanel = QuickInsightsPanel;

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
                var questionStats = exam.Contents.Select(q =>
                {
                    var responses = submissions.SelectMany(s => s.Responses)
                                              .Where(r => r.QuestionId == q.Id)
                                              .ToList();
                    var correctCount = responses.Count(r => r.IsCorrect);
                    var totalCount = responses.Count;
                    var correctPercentage = totalCount > 0 ? (correctCount * 100.0) / totalCount : 0;

                    return new QuestionInsight
                    {
                        QuestionNumber = exam.Contents.IndexOf(q) + 1,
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
                    AverageTime = 0,
                    QualityIndicator = item.QualityIndicator
                }).OrderBy(x => x.QuestionNumber).ToList();

                if (ItemAnalysisGrid != null)
                    ItemAnalysisGrid.ItemsSource = itemData;
            }
            catch (Exception ex)
            {
                Debug.WriteLine($"Error loading item analysis: {ex.Message}");
                if (ItemAnalysisGrid != null)
                    ItemAnalysisGrid.ItemsSource = new List<ItemAnalysisData>();
            }
        }

        private void ExportItemAnalysis_Click(object sender, RoutedEventArgs e)
        {
            MessageBox.Show("Export Item Analysis functionality coming soon!", "Info", 
                MessageBoxButton.OK, MessageBoxImage.Information);
        }

        private void ItemDetails_Click(object sender, RoutedEventArgs e)
        {
            if (sender is Button btn && btn.Tag is string questionId)
            {
                MessageBox.Show($"Question details for {questionId} coming soon!", "Info", 
                    MessageBoxButton.OK, MessageBoxImage.Information);
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

                    return new StudentPerformanceData
                    {
                        Id = submission.StudentId,
                        Name = submission.StudentName,
                        Score = (int)submission.TotalScore,
                        Percentage = percentage,
                        Rank = rank,
                        TimeTaken = timeTaken,
                        FlagCount = 0 // Will be updated separately
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
                StudentSearchBox.Foreground = new SolidColorBrush(Color.FromRgb(232, 234, 237));
            }
        }

        private void StudentSearchBox_LostFocus(object sender, RoutedEventArgs e)
        {
            if (string.IsNullOrWhiteSpace(StudentSearchBox.Text))
            {
                StudentSearchBox.Text = "Search students...";
                StudentSearchBox.Foreground = new SolidColorBrush(Color.FromRgb(156, 163, 175));
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
                UpdateStudentMetrics(student);
            }
            catch (Exception ex)
            {
                Debug.WriteLine($"Error loading student details: {ex.Message}");
            }
        }

        private void UpdateStudentMetrics(StudentPerformanceData student)
        {
            // Find metric TextBlocks and update them
            var metricBlocks = FindVisualChildren<TextBlock>(StudentPerformanceGrid)
                .Where(tb => tb.FontSize == 24 && tb.FontWeight == FontWeights.Bold)
                .ToList();

            if (metricBlocks.Count >= 4)
            {
                metricBlocks[0].Text = $"{student.Percentage:F1}%";
                metricBlocks[1].Text = $"{student.Rank}{GetOrdinalSuffix(student.Rank)}";
                metricBlocks[2].Text = student.TimeTaken.ToString(@"mm\:ss");
                metricBlocks[3].Text = student.FlagCount.ToString();
                metricBlocks[3].Foreground = student.FlagCount == 0 ? 
                    new SolidColorBrush(Color.FromRgb(16, 185, 129)) : 
                    new SolidColorBrush(Color.FromRgb(239, 68, 68));
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
            MessageBox.Show("Export Integrity Timeline functionality coming soon!", "Info", 
                MessageBoxButton.OK, MessageBoxImage.Information);
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

        #region Trends Over Time

        private async void DrawTrendChart()
        {
            try
            {
                if (TrendChart == null) return;

                TrendChart.Children.Clear();

                // Get real historical trend data
                var trendData = await GetHistoricalTrendDataAsync();
                
                if (!trendData.Any())
                {
                    var noDataText = new TextBlock
                    {
                        Text = "No historical data available",
                        FontSize = 14,
                        Foreground = new SolidColorBrush(Colors.Gray),
                        HorizontalAlignment = HorizontalAlignment.Center,
                        VerticalAlignment = VerticalAlignment.Center
                    };
                    
                    Canvas.SetLeft(noDataText, TrendChart.ActualWidth / 2 - 100);
                    Canvas.SetTop(noDataText, TrendChart.ActualHeight / 2);
                    TrendChart.Children.Add(noDataText);
                    
                    await UpdateTrendInsights(new List<TrendDataPoint>());
                    return;
                }

                // Draw trend chart
                DrawTrendLine(trendData);
                await UpdateTrendInsights(trendData);
            }
            catch (Exception ex)
            {
                Debug.WriteLine($"Error drawing trend chart: {ex.Message}");
            }
        }

        private void DrawTrendLine(List<TrendDataPoint> trendData)
        {
            if (trendData.Count < 2) return;

            var chartWidth = TrendChart.ActualWidth > 0 ? TrendChart.ActualWidth - 40 : 400;
            var chartHeight = TrendChart.ActualHeight > 0 ? TrendChart.ActualHeight - 40 : 260;
            var pointSpacing = chartWidth / Math.Max(trendData.Count - 1, 1);

            var minScore = trendData.Min(d => d.Score);
            var maxScore = trendData.Max(d => d.Score);
            var scoreRange = Math.Max(maxScore - minScore, 1);

            // Draw trend line
            for (int i = 0; i < trendData.Count - 1; i++)
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
                    Stroke = new SolidColorBrush(Color.FromRgb(212, 184, 150)),
                    StrokeThickness = 3
                };

                TrendChart.Children.Add(line);
            }
        }

        private async Task UpdateTrendInsights(List<TrendDataPoint> trendData)
        {
            try
            {
                var trendsInsightsPanel = TrendInsightsPanel;

                if (trendsInsightsPanel != null)
                {
                    trendsInsightsPanel.Children.Clear();

                    var titleBlock = new TextBlock
                    {
                        Text = "?? Trend Insights",
                        FontSize = 16,
                        FontWeight = FontWeights.Medium,
                        Foreground = new SolidColorBrush(Color.FromRgb(232, 234, 237)),
                        Margin = new Thickness(0, 0, 0, 16)
                    };
                    trendsInsightsPanel.Children.Add(titleBlock);

                    if (!trendData.Any())
                    {
                        var noDataTexts = new[]
                        {
                            "• No historical performance data available",
                            "• Publish more exams to see trend analysis",
                            "• Student performance insights will appear here"
                        };

                        foreach (var text in noDataTexts)
                        {
                            var textBlock = new TextBlock
                            {
                                Text = text,
                                FontSize = 12,
                                Foreground = new SolidColorBrush(Color.FromRgb(156, 163, 175)),
                                Margin = new Thickness(0, 0, 0, 4)
                            };
                            trendsInsightsPanel.Children.Add(textBlock);
                        }
                        return;
                    }

                    var insights = GenerateTrendInsights(trendData);
                    foreach (var insight in insights)
                    {
                        var color = insight.Contains("improving") ? Color.FromRgb(16, 185, 129) :
                                   insight.Contains("declining") ? Color.FromRgb(239, 68, 68) :
                                   insight.Contains("Highest") ? Color.FromRgb(212, 184, 150) :
                                   Color.FromRgb(156, 163, 175);

                        var insightBlock = new TextBlock
                        {
                            Text = insight,
                            FontSize = 12,
                            Foreground = new SolidColorBrush(color),
                            Margin = new Thickness(0, 0, 0, 6),
                            TextWrapping = TextWrapping.Wrap
                        };
                        trendsInsightsPanel.Children.Add(insightBlock);
                    }
                }
            }
            catch (Exception ex)
            {
                Debug.WriteLine($"Error updating trend insights: {ex.Message}");
            }
        }

        private List<string> GenerateTrendInsights(List<TrendDataPoint> trendData)
        {
            var insights = new List<string>();
            
            if (trendData.Count < 2)
            {
                insights.Add("• Need more exam data for trend analysis");
                insights.Add($"• Latest score: {trendData.FirstOrDefault()?.Score:F1}%");
                insights.Add("• Historical comparison unavailable");
                return insights;
            }

            var firstScore = trendData.First().Score;
            var lastScore = trendData.Last().Score;
            var change = lastScore - firstScore;
            var avgScore = trendData.Average(t => t.Score);
            
            // Trend direction
            if (change > 5)
                insights.Add($"• Performance improving: +{change:F1}% over time");
            else if (change < -5)
                insights.Add($"• Performance declining: {change:F1}% over time");
            else
                insights.Add($"• Performance stable: {change:F1}% variance");

            insights.Add($"• Class average across exams: {avgScore:F1}%");
            
            var bestExam = trendData.OrderByDescending(t => t.Score).First();
            var worstExam = trendData.OrderBy(t => t.Score).First();
            
            if (bestExam.Score > worstExam.Score + 10)
                insights.Add($"• Highest: {bestExam.Score:F1}% ({bestExam.ExamTitle})");
            else
                insights.Add($"• Consistent performance across exams");

            return insights;
        }

        private async Task<List<TrendDataPoint>> GetHistoricalTrendDataAsync()
        {
            try
            {
                var trendData = new List<TrendDataPoint>();
                var allExams = await _firestoreService.GetAllPublishedExamsAsync();
                var sortedExams = allExams.OrderBy(e => e.PublishedDate).Take(6).ToList();
                
                foreach (var exam in sortedExams)
                {
                    try
                    {
                        var metrics = await _analyticsService.CalculateClassOverviewAsync(exam.Id);
                        
                        if (!double.IsNaN(metrics.ClassAverage) && metrics.ClassAverage > 0)
                        {
                            trendData.Add(new TrendDataPoint
                            {
                                Label = exam.PublishedDate.ToString("MMM dd"),
                                Score = metrics.ClassAverage,
                                ExamTitle = exam.Title
                            });
                        }
                    }
                    catch (Exception ex)
                    {
                        Debug.WriteLine($"Error getting metrics for exam {exam.Title}: {ex.Message}");
                    }
                }

                if (!trendData.Any())
                {
                    trendData.Add(new TrendDataPoint { Label = "No Data", Score = 0, ExamTitle = "No Exams" });
                }

                return trendData;
            }
            catch (Exception ex)
            {
                Debug.WriteLine($"Error getting historical trend data: {ex.Message}");
                return new List<TrendDataPoint>
                {
                    new TrendDataPoint { Label = "No Data", Score = 0, ExamTitle = "No Data Available" }
                };
            }
        }

        private void UpdateTrendChart_Click(object sender, RoutedEventArgs e)
        {
            Task.Run(async () =>
            {
                await Task.Delay(100);
                Dispatcher.Invoke(() => DrawTrendChart());
            });
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

    public class TrendDataPoint
    {
        public string Label { get; set; } = "";
        public double Score { get; set; }
        public string ExamTitle { get; set; } = "";
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

    #endregion
}