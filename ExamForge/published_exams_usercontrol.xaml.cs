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
    private const string SignalRHubUrl = "http://localhost:5102/sessionHub"; // Change to deployed URL in production

    // Live monitoring data
    private ObservableCollection<StudentRosterItem> _rosterItems = new();
    private ObservableCollection<IncidentItem> _incidentFeed = new();

    public published_exams_usercontrol()
    {
        InitializeComponent();
        Loaded += Published_Exams_UserControl_Loaded;
    }

    private async void Published_Exams_UserControl_Loaded(object sender, RoutedEventArgs e)
    {
        await LoadPublishedExamsAsync();
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
            Debug.WriteLine($"❌ Error loading published exams: {ex.Message}");
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
            Debug.WriteLine("⚠️ Controls not yet initialized, skipping ApplyFilters");
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
            ExamCardsContainer.ItemsSource = filteredList;
        }
    }

    private void Filter_Changed(object sender, RoutedEventArgs e)
    {
        if (sender is RadioButton rb && rb.IsChecked == true)
        {
            _currentFilter = rb.Content.ToString() ?? "All";
            ApplyFilters();
        }
    }

    private void SearchBox_TextChanged(object sender, TextChangedEventArgs e)
    {
        _searchQuery = SearchBox.Text;
        ApplyFilters();
    }

    private async void Tab_Changed(object sender, RoutedEventArgs e)
    {
        try
        {
            // Hide all panels safely
            if (ScheduledPanel != null) ScheduledPanel.Visibility = Visibility.Collapsed;
            if (LivePanel != null) LivePanel.Visibility = Visibility.Collapsed;
            if (ExamBankPanel != null) ExamBankPanel.Visibility = Visibility.Collapsed;
            if (ClosedPanel != null) ClosedPanel.Visibility = Visibility.Collapsed;
            if (ReportsPanel != null) ReportsPanel.Visibility = Visibility.Collapsed;

            // Show selected panel
            if (ScheduledTab?.IsChecked == true && ScheduledPanel != null)
            {
                ScheduledPanel.Visibility = Visibility.Visible;
            }
            else if (LiveTab?.IsChecked == true && LivePanel != null)
            {
                LivePanel.Visibility = Visibility.Visible;
                await InitializeLiveMonitoringAsync(); // Connect SignalR when Live tab is opened
            }
            else if (ExamBankTab?.IsChecked == true && ExamBankPanel != null)
            {
                ExamBankPanel.Visibility = Visibility.Visible;
                LoadExamBank();
            }
            else if (ClosedTab?.IsChecked == true && ClosedPanel != null)
            {
                ClosedPanel.Visibility = Visibility.Visible;
                LoadClosedExams();
            }
            else if (ReportsTab?.IsChecked == true && ReportsPanel != null)
            {
                ReportsPanel.Visibility = Visibility.Visible;
                LoadRecentExports();
            }
        }
        catch (Exception ex)
        {
            Debug.WriteLine($"Error in Tab_Changed: {ex.Message}");
        }
    }

    /// <summary>
    /// Initialize SignalR connection for live monitoring
    /// </summary>
    private async Task InitializeLiveMonitoringAsync()
    {
        try
        {
            if (_signalRService == null)
            {
                _signalRService = new SignalRService(SignalRHubUrl);

                // Subscribe to real-time events
                _signalRService.OnStudentJoined += OnStudentJoinedHandler;
                _signalRService.OnStudentHeartbeat += OnStudentHeartbeatHandler;
                _signalRService.OnIntegrityEvent += OnIntegrityEventHandler;

                await _signalRService.ConnectAsync();
                
                MessageBox.Show("✅ Connected to live monitoring server!", "Success", 
                    MessageBoxButton.OK, MessageBoxImage.Information);
            }
        }
        catch (Exception ex)
        {
            ShowError($"Failed to connect to live monitoring: {ex.Message}");
        }
    }

    /// <summary>
    /// Handle student joined event
    /// </summary>
    private void OnStudentJoinedHandler(object data)
    {
        Dispatcher.Invoke(() =>
        {
            Debug.WriteLine($"🟢 Student joined: {data}");
            // TODO: Parse data and add to roster
        });
    }

    /// <summary>
    /// Handle student heartbeat (progress updates)
    /// </summary>
    private void OnStudentHeartbeatHandler(object data)
    {
        Dispatcher.Invoke(() =>
        {
            Debug.WriteLine($"💓 Heartbeat received: {data}");
            // TODO: Parse data and update roster
        });
    }

    /// <summary>
    /// Handle integrity event (tab switch, focus loss, etc.)
    /// </summary>
    private void OnIntegrityEventHandler(object data)
    {
        Dispatcher.Invoke(() =>
        {
            Debug.WriteLine($"⚠️ Integrity event: {data}");
            // TODO: Parse data and add to incident feed
        });
    }

    // Action Handlers
    private void OpenPreview_Click(object sender, RoutedEventArgs e)
    {
        if (sender is Button btn && btn.Tag is string examId)
        {
            var exam = _allExams.FirstOrDefault(e => e.Id == examId);
            if (exam != null && !string.IsNullOrEmpty(exam.ExamUrl))
            {
                Process.Start(new ProcessStartInfo
                {
                    FileName = exam.ExamUrl,
                    UseShellExecute = true
                });
            }
        }
    }

    private void EditSchedule_Click(object sender, RoutedEventArgs e)
    {
        if (sender is Button btn && btn.Tag is string examId)
        {
            MessageBox.Show($"Edit schedule for exam: {examId}\n\n(Feature coming soon)",
                "Edit Schedule", MessageBoxButton.OK, MessageBoxImage.Information);
        }
    }

    private void CopyUrl_Click(object sender, RoutedEventArgs e)
    {
        if (sender is Button btn && btn.Tag is string url)
        {
            try
            {
                Clipboard.SetText(url);
                MessageBox.Show("Exam URL copied to clipboard!", "Success",
                    MessageBoxButton.OK, MessageBoxImage.Information);
            }
            catch (Exception ex)
            {
                ShowError($"Failed to copy URL: {ex.Message}");
            }
        }
    }

    private async void Unpublish_Click(object sender, RoutedEventArgs e)
    {
        if (sender is Button btn && btn.Tag is string examId)
        {
            var result = MessageBox.Show(
                "Are you sure you want to unpublish this exam?\n\n" +
                "Students will no longer be able to access it.",
                "Confirm Unpublish",
                MessageBoxButton.YesNo,
                MessageBoxImage.Warning);

            if (result == MessageBoxResult.Yes)
            {
                try
                {
                    var firestoreService = App.FirestoreService;
                    if (firestoreService != null)
                    {
                        await firestoreService.DeletePublishedExamAsync(examId);
                        MessageBox.Show("Exam unpublished successfully!", "Success",
                            MessageBoxButton.OK, MessageBoxImage.Information);
                        await LoadPublishedExamsAsync();
                    }
                }
                catch (Exception ex)
                {
                    ShowError($"Failed to unpublish exam: {ex.Message}");
                }
            }
        }
    }

    #region Live Exam Event Handlers

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

    #endregion

    #region Exam Bank Event Handlers

    private void LoadExamBank()
    {
        try
        {
            // TODO: Load exam bank data
            var sampleData = new List<ExamBankItem>
            {
                new ExamBankItem { Id = "1", Title = "Algebra Basics", Subject = "Mathematics", QuestionCount = 25, Difficulty = "Medium", TimesUsed = 12, LastUsed = DateTime.Now.AddDays(-5) },
                new ExamBankItem { Id = "2", Title = "Biology Cell Structure", Subject = "Science", QuestionCount = 30, Difficulty = "Hard", TimesUsed = 8, LastUsed = DateTime.Now.AddDays(-12) },
                new ExamBankItem { Id = "3", Title = "World War II", Subject = "History", QuestionCount = 20, Difficulty = "Easy", TimesUsed = 15, LastUsed = DateTime.Now.AddDays(-3) }
            };
            
            if (ExamBankGrid != null)
                ExamBankGrid.ItemsSource = sampleData;
        }
        catch (Exception ex)
        {
            Debug.WriteLine($"Error loading exam bank: {ex.Message}");
        }
    }

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

    #endregion

    #region Closed & Grading Event Handlers

    private void LoadClosedExams()
    {
        try
        {
            // TODO: Load closed exams and grading queue
            var sampleGradingData = new List<GradingQueueItem>
            {
                new GradingQueueItem { Id = "1", StudentName = "John Doe", ExamTitle = "Midterm Exam", SubmittedAt = DateTime.Now.AddHours(-2), AutoScore = "85/100", EssayCount = 2 },
                new GradingQueueItem { Id = "2", StudentName = "Jane Smith", ExamTitle = "Final Exam", SubmittedAt = DateTime.Now.AddHours(-1), AutoScore = "92/100", EssayCount = 1 },
                new GradingQueueItem { Id = "3", StudentName = "Bob Johnson", ExamTitle = "Quiz 5", SubmittedAt = DateTime.Now.AddMinutes(-30), AutoScore = "78/100", EssayCount = 3 }
            };
            
            if (GradingQueueGrid != null)
                GradingQueueGrid.ItemsSource = sampleGradingData;
        }
        catch (Exception ex)
        {
            Debug.WriteLine($"Error loading closed exams: {ex.Message}");
        }
    }

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

    #endregion

    #region Reports Event Handlers

    private void LoadRecentExports()
    {
        try
        {
            // TODO: Load recent exports data
            var sampleExports = new List<ExportItem>
            {
                new ExportItem { ExportType = "Grade Report", ExamTitle = "Midterm Exam", GeneratedAt = DateTime.Now.AddDays(-1), FileSize = "2.3 MB", FilePath = "C:\\Exports\\grades_midterm.csv" },
                new ExportItem { ExportType = "Item Analysis", ExamTitle = "Quiz 5", GeneratedAt = DateTime.Now.AddDays(-3), FileSize = "1.8 MB", FilePath = "C:\\Exports\\analysis_quiz5.csv" },
                new ExportItem { ExportType = "Integrity Report", ExamTitle = "Final Exam", GeneratedAt = DateTime.Now.AddDays(-7), FileSize = "945 KB", FilePath = "C:\\Exports\\integrity_final.csv" }
            };
            
            if (RecentExportsGrid != null)
                RecentExportsGrid.ItemsSource = sampleExports;
        }
        catch (Exception ex)
        {
            Debug.WriteLine($"Error loading recent exports: {ex.Message}");
        }
    }

    private async void ExportGrades_Click(object sender, RoutedEventArgs e)
    {
        try
        {
            // Get selected exam (for demo, use first available exam)
            var exam = _allExams.FirstOrDefault();
            if (exam == null)
            {
                MessageBox.Show("No exams available for export.", "Info", 
                    MessageBoxButton.OK, MessageBoxImage.Information);
                return;
            }

            var exportService = new ExportService();
            var filePath = await exportService.ExportGradesToExcelAsync(exam.Id, exam.Title);
            
            MessageBox.Show($"Grades exported successfully!\nSaved to: {filePath}", "Success", 
                MessageBoxButton.OK, MessageBoxImage.Information);
        }
        catch (Exception ex)
        {
            ShowError($"Failed to export grades: {ex.Message}");
        }
    }

    private async void ExportSubmissions_Click(object sender, RoutedEventArgs e)
    {
        try
        {
            var exam = _allExams.FirstOrDefault();
            if (exam == null)
            {
                MessageBox.Show("No exams available for export.", "Info", 
                    MessageBoxButton.OK, MessageBoxImage.Information);
                return;
            }

            var exportService = new ExportService();
            var filePath = await exportService.ExportSubmissionsAsync(exam.Id, exam.Title);
            
            MessageBox.Show($"Submissions exported successfully!\nSaved to: {filePath}", "Success", 
                MessageBoxButton.OK, MessageBoxImage.Information);
        }
        catch (Exception ex)
        {
            ShowError($"Failed to export submissions: {ex.Message}");
        }
    }

    private void ExportClassSummary_Click(object sender, RoutedEventArgs e)
    {
        MessageBox.Show("Export Class Summary functionality coming soon!", "Info", 
            MessageBoxButton.OK, MessageBoxImage.Information);
    }

    private void ExportItemAnalysis_Click(object sender, RoutedEventArgs e)
    {
        MessageBox.Show("Export Item Analysis functionality coming soon!", "Info", 
            MessageBoxButton.OK, MessageBoxImage.Information);
    }

    private void ExportStudentPerformance_Click(object sender, RoutedEventArgs e)
    {
        MessageBox.Show("Export Student Performance functionality coming soon!", "Info", 
            MessageBoxButton.OK, MessageBoxImage.Information);
    }

    private void ExportTrends_Click(object sender, RoutedEventArgs e)
    {
        MessageBox.Show("Export Trends Over Time functionality coming soon!", "Info", 
            MessageBoxButton.OK, MessageBoxImage.Information);
    }

    private async void ExportIntegrityReport_Click(object sender, RoutedEventArgs e)
    {
        try
        {
            var exam = _allExams.FirstOrDefault();
            if (exam == null)
            {
                MessageBox.Show("No exams available for export.", "Info", 
                    MessageBoxButton.OK, MessageBoxImage.Information);
                return;
            }

            var exportService = new ExportService();
            var filePath = await exportService.ExportIntegrityReportAsync(exam.Id, exam.Title);
            
            MessageBox.Show($"Integrity report exported successfully!\nSaved to: {filePath}", "Success", 
                MessageBoxButton.OK, MessageBoxImage.Information);
        }
        catch (Exception ex)
        {
            ShowError($"Failed to export integrity report: {ex.Message}");
        }
    }

    private void ExportAuditLogs_Click(object sender, RoutedEventArgs e)
    {
        MessageBox.Show("Export Audit Logs functionality coming soon!", "Info", 
            MessageBoxButton.OK, MessageBoxImage.Information);
    }

    private void ExportSessionEvents_Click(object sender, RoutedEventArgs e)
    {
        MessageBox.Show("Export Session Events functionality coming soon!", "Info", 
            MessageBoxButton.OK, MessageBoxImage.Information);
    }

    private void ExportGradebook_Click(object sender, RoutedEventArgs e)
    {
        MessageBox.Show("Export Gradebook functionality coming soon!", "Info", 
            MessageBoxButton.OK, MessageBoxImage.Information);
    }

    private void OpenExport_Click(object sender, RoutedEventArgs e)
    {
        if (sender is Button btn && btn.Tag is string filePath)
        {
            try
            {
                Process.Start(new ProcessStartInfo
                {
                    FileName = filePath,
                    UseShellExecute = true
                });
            }
            catch (Exception ex)
            {
                ShowError($"Failed to open file: {ex.Message}");
            }
        }
    }

    #endregion

    private void ShowError(string message)
    {
        MessageBox.Show(message, "Error", MessageBoxButton.OK, MessageBoxImage.Error);
    }
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
    public int ProgressPercent { get; set; }
    public int TimeRemaining { get; set; }
    public int FlagCount { get; set; }
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

// Exam Bank item
public class ExamBankItem
{
    public string Id { get; set; } = "";
    public string Title { get; set; } = "";
    public string Subject { get; set; } = "";
    public int QuestionCount { get; set; }
    public string Difficulty { get; set; } = "";
    public int TimesUsed { get; set; }
    public DateTime LastUsed { get; set; }
}

// Grading Queue item
public class GradingQueueItem
{
    public string Id { get; set; } = "";
    public string StudentName { get; set; } = "";
    public string ExamTitle { get; set; } = "";
    public DateTime SubmittedAt { get; set; }
    public string AutoScore { get; set; } = "";
    public int EssayCount { get; set; }
}

// Export item
public class ExportItem
{
    public string ExportType { get; set; } = "";
    public string ExamTitle { get; set; } = "";
    public DateTime GeneratedAt { get; set; }
    public string FileSize { get; set; } = "";
    public string FilePath { get; set; } = "";
}