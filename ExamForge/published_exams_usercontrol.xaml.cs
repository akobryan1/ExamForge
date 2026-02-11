using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.Diagnostics;
using System.Linq;
using System.Threading.Tasks;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Media;
using ExamForge.Models;
using ExamForge.Services;

namespace ExamForge;

public partial class published_exams_usercontrol : UserControl
{
    private List<PublishedExamViewModel> _allExams = new();
    private string _currentFilter = "All";
    private string _searchQuery = "";

    // SignalR client  
    private SignalRService? _signalRService;
    private const string SignalRHubUrl = "https://examforge-signalr.onrender.com/sessionHub";

    // Live monitoring data
    private ObservableCollection<StudentRosterItem> _rosterItems = new();
    private ObservableCollection<IncidentItem> _incidentFeed = new();
    
    // Auto-refresh timers
    private System.Windows.Threading.DispatcherTimer? _liveDataTimer;
    private const int LIVE_REFRESH_INTERVAL = 5000; // 5 seconds

    public published_exams_usercontrol()
    {
        InitializeComponent();
        Loaded += Published_Exams_UserControl_Loaded;
        Unloaded += Published_Exams_UserControl_Unloaded;
    }

    private async void Published_Exams_UserControl_Loaded(object sender, RoutedEventArgs e)
    {
        await LoadPublishedExamsAsync();
    }

    private void Published_Exams_UserControl_Unloaded(object sender, RoutedEventArgs e)
    {
        StopLiveDataRefresh();
    }

    private async Task LoadPublishedExamsAsync()
    {
        try
        {
            var firestoreService = App.FirestoreService;
            if (firestoreService == null)
            {
                ShowError("Firestore service not initialized");
                return;
            }

            var exams = await firestoreService.GetAllPublishedExamsAsync();
            _allExams = exams.Select(exam => new PublishedExamViewModel
            {
                Id = exam.Id,
                Title = exam.Title,
                StartTime = exam.StartTime,
                EndTime = exam.EndTime,
                ExamDuration = exam.ExamDuration,
                PublishedDate = exam.PublishedDate,
                CreatedBy = exam.CreatedBy,
                ExamUrl = exam.ExamUrl,
                Status = DetermineStatus(exam),
                ScheduleText = FormatSchedule(exam.StartTime, exam.EndTime),
                DurationText = $"{exam.ExamDuration} minutes",
                CreatedByText = $"Created by {exam.CreatedBy}",
                StatusColor = GetStatusColor(DetermineStatus(exam))
            }).ToList();

            ApplyFilters();
        }
        catch (Exception ex)
        {
            Debug.WriteLine($"? Error loading published exams: {ex.Message}");
            ShowError($"Failed to load exams: {ex.Message}");
        }
    }

    private string DetermineStatus(PublishedExam exam)
    {
        var now = DateTime.UtcNow;

        if (exam.Status == "Unpublished")
            return "Unpublished";

        if (now < exam.StartTime)
            return "Upcoming";

        if (now >= exam.StartTime && now <= exam.EndTime)
            return "Active";

        return "Completed";
    }

    private Brush GetStatusColor(string status)
    {
        return status switch
        {
            "Upcoming" => new SolidColorBrush(Color.FromRgb(251, 191, 36)),
            "Active" => new SolidColorBrush(Color.FromRgb(74, 222, 128)),
            "Completed" => new SolidColorBrush(Color.FromRgb(156, 163, 175)),
            "Unpublished" => new SolidColorBrush(Color.FromRgb(239, 68, 68)),
            _ => new SolidColorBrush(Color.FromRgb(156, 163, 175))
        };
    }

    private string FormatSchedule(DateTime start, DateTime end)
    {
        return $"{start:MMM dd, h:mm tt} - {end:h:mm tt}";
    }

    private void ApplyFilters()
    {
        if (ExamCardsContainer == null || EmptyState == null)
        {
            Debug.WriteLine("?? Controls not yet initialized, skipping ApplyFilters");
            return;
        }

        var filtered = _allExams.AsEnumerable();

        if (_currentFilter != "All")
        {
            filtered = filtered.Where(e => e.Status == _currentFilter);
        }

        if (!string.IsNullOrWhiteSpace(_searchQuery))
        {
            var query = _searchQuery.ToLower();
            filtered = filtered.Where(e =>
                e.Title.ToLower().Contains(query) ||
                e.CreatedBy.ToLower().Contains(query));
        }

        var filteredList = filtered.ToList();

        if (filteredList.Count == 0)
        {
            ExamCardsContainer.Visibility = Visibility.Collapsed;
            EmptyState.Visibility = Visibility.Visible;
        }
        else
        {
            ExamCardsContainer.Visibility = Visibility.Visible;
            EmptyState.Visibility = Visibility.Collapsed;
            // Update the ItemsSource with filtered results
        }
    }

    #region Tab Event Handlers

    private async void Tab_Changed(object sender, RoutedEventArgs e)
    {
        try
        {
            // Hide all panels first
            if (ScheduledPanel != null) ScheduledPanel.Visibility = Visibility.Collapsed;
            if (LivePanel != null) LivePanel.Visibility = Visibility.Collapsed;
            if (ExamBankPanel != null) ExamBankPanel.Visibility = Visibility.Collapsed;
            if (ClosedPanel != null) ClosedPanel.Visibility = Visibility.Collapsed;

            // Show selected panel and load data
            if (ScheduledTab?.IsChecked == true && ScheduledPanel != null)
            {
                ScheduledPanel.Visibility = Visibility.Visible;
                await LoadPublishedExamsAsync();
                Debug.WriteLine("?? Scheduled tab activated");
            }
            else if (LiveTab?.IsChecked == true && LivePanel != null)
            {
                LivePanel.Visibility = Visibility.Visible;
                await InitializeLiveMonitoringAsync();
                Debug.WriteLine("?? Live tab activated");
            }
            else if (ExamBankTab?.IsChecked == true && ExamBankPanel != null)
            {
                ExamBankPanel.Visibility = Visibility.Visible;
                await LoadExamBankAsync();
                Debug.WriteLine("?? Exam Bank tab activated");
            }
            else if (ClosedTab?.IsChecked == true && ClosedPanel != null)
            {
                ClosedPanel.Visibility = Visibility.Visible;
                await LoadClosedExamsAsync();
                Debug.WriteLine("?? Closed tab activated");
            }
            
            // Stop live data refresh if not on live tab
            if (LiveTab?.IsChecked != true)
            {
                StopLiveDataRefresh();
            }
        }
        catch (Exception ex)
        {
            Debug.WriteLine($"Error in Tab_Changed: {ex.Message}");
        }
    }

    #endregion

    #region Live Monitoring

    private async Task InitializeLiveMonitoringAsync()
    {
        try
        {
            if (_signalRService == null)
            {
                _signalRService = new SignalRService(SignalRHubUrl);
                await _signalRService.ConnectAsync();
                Debug.WriteLine("? Connected to live monitoring server!");
            }

            await LoadLiveRosterData();
            StartLiveDataRefresh();
        }
        catch (Exception ex)
        {
            Debug.WriteLine($"? Failed to initialize live monitoring: {ex.Message}");
            ShowError($"Failed to connect to live monitoring: {ex.Message}");
        }
    }

    private void StartLiveDataRefresh()
    {
        StopLiveDataRefresh();

        _liveDataTimer = new System.Windows.Threading.DispatcherTimer
        {
            Interval = TimeSpan.FromMilliseconds(LIVE_REFRESH_INTERVAL)
        };
        _liveDataTimer.Tick += async (s, e) => await RefreshLiveData();
        _liveDataTimer.Start();
        
        Debug.WriteLine("? Live data refresh started (every 5s)");
    }

    private void StopLiveDataRefresh()
    {
        _liveDataTimer?.Stop();
        _liveDataTimer = null;
        Debug.WriteLine("?? Live data refresh stopped");
    }

    private async Task RefreshLiveData()
    {
        await LoadLiveRosterData();
    }

    private async Task LoadLiveRosterData()
    {
        try
        {
            var firestoreService = App.FirestoreService;
            if (firestoreService == null) return;

            // Get published exams and filter only currently active ones
            var publishedExams = await firestoreService.GetAllPublishedExamsAsync();
            var currentTime = DateTime.UtcNow;
            
            // Filter only currently active exams (start time <= now <= end time)
            var activeExams = publishedExams
                .Where(e => e.StartTime <= currentTime && e.EndTime >= currentTime)
                .ToList();

            // Update active session count
            await Dispatcher.InvokeAsync(() =>
            {
                try
                {
                    // Find and update the active sessions metric
                    var metricTexts = FindVisualChildren<TextBlock>(this)
                        .Where(tb => tb.FontSize == 32 && tb.FontWeight == FontWeights.Bold)
                        .ToList();

                    if (metricTexts.Any())
                    {
                        metricTexts[0].Text = activeExams.Count.ToString();
                    }
                }
                catch (Exception ex)
                {
                    Debug.WriteLine($"Error updating active sessions metric: {ex.Message}");
                }
            });

            _rosterItems.Clear();
            
            if (activeExams.Any())
            {
                // Show active exams as placeholder
                foreach (var exam in activeExams)
                {
                    _rosterItems.Add(new StudentRosterItem
                    {
                        StudentId = exam.Id,
                        StudentName = exam.Title,
                        ConnectionStatus = "LIVE",
                        ProgressPercent = "Waiting for students...",
                        TimeRemaining = FormatTimeRemaining(exam.EndTime - DateTime.UtcNow),
                        FlagCount = "0"
                    });
                }
            }
            else
            {
                // Show message when no active exams
                _rosterItems.Add(new StudentRosterItem
                {
                    StudentId = "none",
                    StudentName = "No active exams",
                    ConnectionStatus = "WAITING",
                    ProgressPercent = "0%",
                    TimeRemaining = "N/A",
                    FlagCount = "0"
                });
            }

            // Update UI
            if (LiveRosterGrid != null)
                LiveRosterGrid.ItemsSource = _rosterItems;
        }
        catch (Exception ex)
        {
            Debug.WriteLine($"Error loading live roster data: {ex.Message}");
        }
    }

    private string FormatTimeRemaining(TimeSpan remaining)
    {
        if (remaining.TotalSeconds <= 0) return "00:00";
        return $"{remaining.Hours:00}:{remaining.Minutes:00}";
    }

    #endregion

    #region Exam Bank

    private async Task LoadExamBankAsync()
    {
        try
        {
            var firestoreService = App.FirestoreService;
            if (firestoreService == null) return;

            // Load all published exams from Firestore
            var publishedExams = await firestoreService.GetAllPublishedExamsAsync();
            
            // Convert to exam bank items
            var examBankItems = publishedExams.Select(exam => new ExamBankItem
            {
                Id = exam.Id,
                Title = exam.Title,
                Subject = "General", // Can be enhanced based on exam metadata
                QuestionCount = exam.Contents?.Count ?? 0,
                Difficulty = "Medium", // Can be enhanced with actual difficulty calculation
                TimesUsed = 0, // Can be enhanced with submission tracking
                LastUsed = exam.PublishedDate,
                CreatedBy = exam.CreatedBy,
                DateCreated = exam.PublishedDate, // Using PublishedDate as creation date
                IsPublished = exam.Status == "Published"
            }).ToList();
            
            if (ExamBankGrid != null)
                ExamBankGrid.ItemsSource = examBankItems;
                
            Debug.WriteLine($"?? Loaded {examBankItems.Count} exams from Firestore");
        }
        catch (Exception ex)
        {
            Debug.WriteLine($"Error loading exam bank: {ex.Message}");
            ShowError($"Failed to load exam bank: {ex.Message}");
        }
    }

    #endregion

    #region Grading Queue

    private async Task LoadClosedExamsAsync()
    {
        try
        {
            var firestoreService = App.FirestoreService;
            if (firestoreService == null) return;

            // Get completed exams
            var publishedExams = await firestoreService.GetAllPublishedExamsAsync();
            var completedExams = publishedExams
                .Where(e => e.EndTime < DateTime.UtcNow)
                .OrderByDescending(e => e.EndTime)
                .Take(20)
                .ToList();

            // Load grading queue items from submissions
            var gradingItems = new List<GradingQueueItem>();
            
            foreach (var exam in completedExams)
            {
                try
                {
                    var submissions = await firestoreService.GetExamSubmissionsAsync(exam.Id);
                    
                    foreach (var submission in submissions)
                    {
                        gradingItems.Add(new GradingQueueItem
                        {
                            Id = submission.Id,
                            StudentName = submission.StudentName,
                            ExamTitle = exam.Title,
                            SubmittedAt = submission.EndTime ?? DateTime.Now,
                            AutoScore = $"{submission.TotalScore:F0}/{submission.TotalPossiblePoints:F0}",
                            EssayCount = 0 // Essay questions tracking can be enhanced
                        });
                    }
                }
                catch (Exception ex)
                {
                    Debug.WriteLine($"Error loading submissions for exam {exam.Title}: {ex.Message}");
                }
            }
            
            if (GradingQueueGrid != null)
                GradingQueueGrid.ItemsSource = gradingItems.OrderByDescending(g => g.SubmittedAt).ToList();
                
            Debug.WriteLine($"?? Loaded {gradingItems.Count} grading queue items");
        }
        catch (Exception ex)
        {
            Debug.WriteLine($"Error loading closed exams: {ex.Message}");
            ShowError($"Failed to load grading queue: {ex.Message}");
        }
    }

    #endregion

    #region Event Handlers

    private void Filter_Changed(object sender, RoutedEventArgs e)
    {
        if (sender is RadioButton rb && rb.Content?.ToString() is string filter)
        {
            _currentFilter = filter;
            ApplyFilters();
        }
    }

    private void SearchBox_TextChanged(object sender, TextChangedEventArgs e)
    {
        if (sender is TextBox tb)
        {
            _searchQuery = tb.Text;
            ApplyFilters();
        }
    }

    // Live monitoring event handlers
    private void BroadcastMessage_Click(object sender, RoutedEventArgs e)
    {
        MessageBox.Show("Broadcast Message functionality coming soon!", "Info", 
            MessageBoxButton.OK, MessageBoxImage.Information);
    }

    private void PauseAllExams_Click(object sender, RoutedEventArgs e)
    {
        MessageBox.Show("Pause All Exams functionality coming soon!", "Info", 
            MessageBoxButton.OK, MessageBoxImage.Information);
    }

    private void ExportLiveReport_Click(object sender, RoutedEventArgs e)
    {
        MessageBox.Show("Export Live Report functionality coming soon!", "Info", 
            MessageBoxButton.OK, MessageBoxImage.Information);
    }

    // Exam Bank event handlers
    private void CreateTemplate_Click(object sender, RoutedEventArgs e)
    {
        MessageBox.Show("Create Template functionality coming soon!", "Info", 
            MessageBoxButton.OK, MessageBoxImage.Information);
    }

    private void BankSearch_Changed(object sender, TextChangedEventArgs e)
    {
        // TODO: Filter exam bank based on search
    }

    private void BankSearchBox_GotFocus(object sender, RoutedEventArgs e)
    {
        if (BankSearchBox.Text == "Search exam bank...")
        {
            BankSearchBox.Text = "";
            BankSearchBox.Foreground = new SolidColorBrush(Color.FromRgb(232, 234, 237)); // TextPrimary
        }
    }

    private void BankSearchBox_LostFocus(object sender, RoutedEventArgs e)
    {
        if (string.IsNullOrWhiteSpace(BankSearchBox.Text))
        {
            BankSearchBox.Text = "Search exam bank...";
            BankSearchBox.Foreground = new SolidColorBrush(Color.FromRgb(156, 163, 175)); // TextSecondary
        }
    }

    private void CloneExam_Click(object sender, RoutedEventArgs e)
    {
        if (sender is Button btn && btn.Tag is string examId)
        {
            MessageBox.Show($"Clone exam {examId} functionality coming soon!", "Info", 
                MessageBoxButton.OK, MessageBoxImage.Information);
        }
    }

    private void EditBankExam_Click(object sender, RoutedEventArgs e)
    {
        if (sender is Button btn && btn.Tag is string examId)
        {
            MessageBox.Show($"Edit exam {examId} functionality coming soon!", "Info", 
                MessageBoxButton.OK, MessageBoxImage.Information);
        }
    }

    // Grading Queue event handlers
    private void ReviewSubmission_Click(object sender, RoutedEventArgs e)
    {
        if (sender is Button btn && btn.Tag is string submissionId)
        {
            MessageBox.Show($"Review submission {submissionId} functionality coming soon!", "Info", 
                MessageBoxButton.OK, MessageBoxImage.Information);
        }
    }

    private void BulkFinalize_Click(object sender, RoutedEventArgs e)
    {
        MessageBox.Show("Bulk Finalize Grades functionality coming soon!", "Info", 
            MessageBoxButton.OK, MessageBoxImage.Information);
    }

    private void ReleaseResults_Click(object sender, RoutedEventArgs e)
    {
        MessageBox.Show("Release Results functionality coming soon!", "Info", 
            MessageBoxButton.OK, MessageBoxImage.Information);
    }

    private void ReopenMakeup_Click(object sender, RoutedEventArgs e)
    {
        MessageBox.Show("Reopen for Make-up functionality coming soon!", "Info", 
            MessageBoxButton.OK, MessageBoxImage.Information);
    }

    private void ExportGradebook_Click(object sender, RoutedEventArgs e)
    {
        MessageBox.Show("Export Gradebook functionality coming soon!", "Info", 
            MessageBoxButton.OK, MessageBoxImage.Information);
    }

    // Published exam action handlers
    private void OpenPreview_Click(object sender, RoutedEventArgs e)
    {
        if (sender is Button btn && btn.Tag is string examId)
        {
            var exam = _allExams.FirstOrDefault(e => e.Id == examId);
            if (exam != null)
            {
                try
                {
                    Process.Start(new ProcessStartInfo
                    {
                        FileName = exam.ExamUrl,
                        UseShellExecute = true
                    });
                }
                catch (Exception ex)
                {
                    ShowError($"Failed to open exam preview: {ex.Message}");
                }
            }
        }
    }

    private void EditSchedule_Click(object sender, RoutedEventArgs e)
    {
        if (sender is Button btn && btn.Tag is string examId)
        {
            MessageBox.Show($"Edit schedule for exam {examId} functionality coming soon!", "Info", 
                MessageBoxButton.OK, MessageBoxImage.Information);
        }
    }

    private void CopyUrl_Click(object sender, RoutedEventArgs e)
    {
        if (sender is Button btn && btn.Tag is string examId)
        {
            var exam = _allExams.FirstOrDefault(e => e.Id == examId);
            if (exam != null)
            {
                try
                {
                    Clipboard.SetText(exam.ExamUrl);
                    MessageBox.Show("Exam URL copied to clipboard!", "Success", 
                        MessageBoxButton.OK, MessageBoxImage.Information);
                }
                catch (Exception ex)
                {
                    ShowError($"Failed to copy URL: {ex.Message}");
                }
            }
        }
    }

    private async void Unpublish_Click(object sender, RoutedEventArgs e)
    {
        if (sender is Button btn && btn.Tag is string examId)
        {
            try
            {
                var result = MessageBox.Show("Are you sure you want to unpublish this exam?", 
                    "Confirm Unpublish", MessageBoxButton.YesNo, MessageBoxImage.Question);
                
                if (result == MessageBoxResult.Yes)
                {
                    MessageBox.Show("Unpublish functionality coming soon!", "Info",
                        MessageBoxButton.OK, MessageBoxImage.Information);
                    // TODO: Implement unpublish functionality
                    // await firestoreService.UnpublishExamAsync(examId);
                }
            }
            catch (Exception ex)
            {
                ShowError($"Failed to unpublish exam: {ex.Message}");
            }
        }
    }

    #endregion

    private void ShowError(string message)
    {
        MessageBox.Show(message, "Error", MessageBoxButton.OK, MessageBoxImage.Error);
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

// ViewModel classes for UI binding
public class ExamBankItem
{
    public string Id { get; set; } = "";
    public string Title { get; set; } = "";
    public string Subject { get; set; } = "";
    public int QuestionCount { get; set; }
    public string Difficulty { get; set; } = "";
    public int TimesUsed { get; set; }
    public DateTime LastUsed { get; set; }
    public string CreatedBy { get; set; } = "";
    public DateTime DateCreated { get; set; }
    public bool IsPublished { get; set; }
}

public class GradingQueueItem
{
    public string Id { get; set; } = "";
    public string StudentName { get; set; } = "";
    public string ExamTitle { get; set; } = "";
    public DateTime SubmittedAt { get; set; }
    public string AutoScore { get; set; } = "";
    public int EssayCount { get; set; }
}

public class RecentExportItem
{
    public string Id { get; set; } = "";
    public string ExamTitle { get; set; } = "";
    public string ExportType { get; set; } = "";
    public DateTime GeneratedDate { get; set; }
    public int StudentCount { get; set; }
    public string AvgScore { get; set; } = "";
    public string PassRate { get; set; } = "";
}

// ViewModel for binding
public class PublishedExamViewModel
{
    public string Id { get; set; } = "";
    public string Title { get; set; } = "";
    public DateTime StartTime { get; set; }
    public DateTime EndTime { get; set; }
    public int ExamDuration { get; set; }
    public DateTime PublishedDate { get; set; }
    public string CreatedBy { get; set; } = "";
    public string ExamUrl { get; set; } = "";
    public string Status { get; set; } = "";
    public string ScheduleText { get; set; } = "";
    public string DurationText { get; set; } = "";
    public string CreatedByText { get; set; } = "";
    public Brush StatusColor { get; set; } = Brushes.Gray;
}

// Roster item for live monitoring
public class StudentRosterItem
{
    public string StudentId { get; set; } = "";
    public string StudentName { get; set; } = "";
    public string ConnectionStatus { get; set; } = "Offline";
    public string ProgressPercent { get; set; } = "0%";
    public string TimeRemaining { get; set; } = "N/A";
    public string FlagCount { get; set; } = "0";
}

// Incident feed item
public class IncidentItem
{
    public string StudentName { get; set; } = "";
    public string EventType { get; set; } = "";
    public string Severity { get; set; } = "";
    public string Details { get; set; } = "";
    public DateTime Timestamp { get; set; }
}