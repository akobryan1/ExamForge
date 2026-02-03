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
        if (ScheduledPanel == null || LivePanel == null || ClosedPanel == null || ReportsPanel == null)
            return;

        // Hide all panels
        ScheduledPanel.Visibility = Visibility.Collapsed;
        LivePanel.Visibility = Visibility.Collapsed;
        ClosedPanel.Visibility = Visibility.Collapsed;
        ReportsPanel.Visibility = Visibility.Collapsed;

        // Show selected panel
        if (ScheduledTab.IsChecked == true)
        {
            ScheduledPanel.Visibility = Visibility.Visible;
        }
        else if (LiveTab.IsChecked == true)
        {
            LivePanel.Visibility = Visibility.Visible;
            await InitializeLiveMonitoringAsync(); // Connect SignalR when Live tab is opened
        }
        else if (ClosedTab.IsChecked == true)
        {
            ClosedPanel.Visibility = Visibility.Visible;
        }
        else if (ReportsTab.IsChecked == true)
        {
            ReportsPanel.Visibility = Visibility.Visible;
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