using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;
using ExamForge.Models;
using ExamForge.Services;

namespace ExamForge
{
    public partial class dashboard_usercontrol : UserControl
    {
        private readonly FirestoreService _firestoreService;
        private readonly AnalyticsService _analyticsService;
        private readonly SignalRService? _signalRService;
        private const string SignalRHubUrl = "https://examforge-signalr.onrender.com/sessionHub";

        private List<PublishedExam> _exams = new();
        private List<ExamSession> _liveSessions = new();
        private List<IntegrityIncident> _recentIncidents = new();
        private bool _signalrSubscribed = false;

        public dashboard_usercontrol()
        {
            InitializeComponent();
            _firestoreService = App.FirestoreService ?? throw new InvalidOperationException("Firestore service not initialized");
            _analyticsService = new AnalyticsService();
            _signalRService = new SignalRService(SignalRHubUrl);
        }

        private async void Dashboard_usercontrol_Loaded(object sender, RoutedEventArgs e)
        {
            await LoadDashboardAsync();
        }

        private async Task LoadDashboardAsync()
        {
            try
            {
                var examsTask = _firestoreService.GetAllPublishedExamsAsync();
                var sessionsTask = _firestoreService.GetActiveExamSessionsAsync();
                var incidentsTask = _firestoreService.GetRecentIntegrityIncidentsAsync(TimeSpan.FromDays(7));

                await Task.WhenAll(examsTask, sessionsTask, incidentsTask);

                _exams = examsTask.Result ?? new();
                _liveSessions = sessionsTask.Result ?? new();
                _recentIncidents = incidentsTask.Result ?? new();

                UpdateDashboard();
                await EnsureSignalRAsync();
            }
            catch (Exception ex)
            {
                MessageBox.Show($"Failed to load dashboard: {ex.Message}", "Error", MessageBoxButton.OK, MessageBoxImage.Error);
            }
        }

        private async Task EnsureSignalRAsync()
        {
            if (_signalRService == null || _signalrSubscribed)
                return;

            try
            {
                if (!_signalRService.IsConnected)
                {
                    await _signalRService.ConnectAsync();
                }

                _signalRService.OnStudentJoined += HandleLiveSignal;
                _signalRService.OnStudentHeartbeat += HandleLiveSignal;
                _signalRService.OnIntegrityEvent += HandleLiveSignal;

                foreach (var session in _liveSessions)
                {
                    try
                    {
                        await _signalRService.MonitorSessionAsync(session.Id);
                    }
                    catch (Exception ex)
                    {
                        System.Diagnostics.Debug.WriteLine($"MonitorSession failed: {ex.Message}");
                    }
                }

                _signalrSubscribed = true;
            }
            catch (Exception ex)
            {
                System.Diagnostics.Debug.WriteLine($"SignalR connect failed: {ex.Message}");
            }
        }

        private async void HandleLiveSignal(object? _)
        {
            await RefreshLiveDataAsync();
        }

        private async Task RefreshLiveDataAsync()
        {
            try
            {
                var sessionsTask = _firestoreService.GetActiveExamSessionsAsync();
                var incidentsTask = _firestoreService.GetRecentIntegrityIncidentsAsync(TimeSpan.FromDays(7));
                await Task.WhenAll(sessionsTask, incidentsTask);

                _liveSessions = sessionsTask.Result ?? new();
                _recentIncidents = incidentsTask.Result ?? new();

                await Dispatcher.InvokeAsync(() => UpdateDashboard());

                if (_signalRService != null && _signalRService.IsConnected)
                {
                    foreach (var session in _liveSessions)
                    {
                        try
                        {
                            await _signalRService.MonitorSessionAsync(session.Id);
                        }
                        catch (Exception ex)
                        {
                            System.Diagnostics.Debug.WriteLine($"MonitorSession refresh failed: {ex.Message}");
                        }
                    }
                }
            }
            catch (Exception ex)
            {
                System.Diagnostics.Debug.WriteLine($"RefreshLiveData failed: {ex.Message}");
            }
        }

        private void UpdateDashboard()
        {
            UpdateKpis();
            UpdateScheduleAndLiveMonitor();
        }

        private void UpdateKpis()
        {
            var upcoming = _exams.Count(e => e.StartTime >= DateTime.UtcNow && e.StartTime <= DateTime.UtcNow.AddDays(7));
            var flags = _recentIncidents.Count;

            UpcomingCountText.Text = upcoming > 0 ? upcoming.ToString() : "–";
            FlagsCountText.Text = flags > 0 ? flags.ToString() : "–";

            UpcomingSubtitle.Text = upcoming > 0 ? "Scheduled this week" : "Nothing scheduled";
            FlagsSubtitle.Text = flags > 0 ? "Review incidents" : "No alerts";
        }

        private void UpdateScheduleAndLiveMonitor()
        {
            var today = DateTime.UtcNow.Date;
            var todayItems = _exams
                .Where(e => e.StartTime.Date == today)
                .OrderBy(e => e.StartTime)
                .Select(e => new TodayScheduleItem
                {
                    ExamId = e.Id,
                    Title = e.Title,
                    Schedule = $"{e.StartTime:t} - {e.EndTime:t}",
                    Meta = $"Duration: {e.ExamDuration} mins • Subject: {e.Subject}"
                })
                .ToList();

            TodayScheduleList.ItemsSource = todayItems;
            TodayEmptyState.Visibility = todayItems.Any() ? Visibility.Collapsed : Visibility.Visible;

            // Live Monitor Stats
            if (!_liveSessions.Any())
            {
                LiveEmpty.Visibility = Visibility.Visible;
                StartedCountText.Text = "–";
                SubmittedCountText.Text = "–";
                AvgProgressText.Text = "–";
                LastActivityText.Text = "–";
            }
            else
            {
                LiveEmpty.Visibility = Visibility.Collapsed;
                var participants = _liveSessions.SelectMany(s => s.Participants.Values).ToList();
                var started = participants.Count;
                var submitted = participants.Count(p => p.ProgressPercent >= 100);
                var avgProgress = participants.Any() ? participants.Average(p => p.ProgressPercent) : 0;

                StartedCountText.Text = started.ToString();
                SubmittedCountText.Text = submitted.ToString();
                AvgProgressText.Text = $"{avgProgress:F0}%";
                LastActivityText.Text = _liveSessions.Max(s => s.LastHeartbeat).ToLocalTime().ToString("t");
            }
        }

        private void UpcomingCard_Click(object sender, MouseButtonEventArgs e)
        {
            NavigateToPublishedExams();
        }

        private void LiveCard_Click(object sender, MouseButtonEventArgs e)
        {
            NavigateToAnalytics();
        }

        private void FlagsCard_Click(object sender, MouseButtonEventArgs e)
        {
            NavigateToAnalytics();
        }

        private void SendReminder_Click(object sender, RoutedEventArgs e)
        {
            var count = _liveSessions.Count;
            MessageBox.Show(count > 0 ? $"Reminder sent to {count} live sessions." : "No live sessions to notify.", "Reminder", MessageBoxButton.OK, MessageBoxImage.Information);
        }

        private void EditExam_Click(object sender, RoutedEventArgs e)
        {
            NavigateToPublishedExams();
        }

        private void DuplicateExam_Click(object sender, RoutedEventArgs e)
        {
            NavigateToPublishedExams();
        }

        private void ViewMonitor_Click(object sender, RoutedEventArgs e)
        {
            NavigateToAnalytics();
        }

        // New Quick Action Methods
        private void CreateExam_Click(object sender, RoutedEventArgs e)
        {
            NavigateToStructureBuilder();
        }

        private void ViewAnalytics_Click(object sender, RoutedEventArgs e)
        {
            NavigateToAnalytics();
        }

        private void ManageExamBank_Click(object sender, RoutedEventArgs e)
        {
            NavigateToPublishedExams();
        }

        private void OpenSettings_Click(object sender, RoutedEventArgs e)
        {
            MessageBox.Show("Settings panel coming soon!", "Settings", MessageBoxButton.OK, MessageBoxImage.Information);
        }

        private async void ExtendTime_Click(object sender, RoutedEventArgs e)
        {
            if (!_liveSessions.Any())
            {
                MessageBox.Show("No live sessions to extend.", "Extend Time", MessageBoxButton.OK, MessageBoxImage.Information);
                return;
            }

            var result = MessageBox.Show("Extend time by 5 minutes for all active sessions?", "Extend Time", MessageBoxButton.YesNo, MessageBoxImage.Question);
            if (result != MessageBoxResult.Yes) return;

            var addedSeconds = 5 * 60; // 5 minutes

            foreach (var session in _liveSessions)
            {
                foreach (var participant in session.Participants.Values)
                {
                    participant.TimeRemaining += addedSeconds;
                    participant.LastHeartbeat = DateTime.UtcNow;
                }

                session.LastHeartbeat = DateTime.UtcNow;

                await _firestoreService.UpdateSessionAsync(session);
                await _firestoreService.LogEventAsync(new SessionEvent
                {
                    SessionId = session.Id,
                    ExamId = session.ExamId,
                    EventType = "teacher_extend_time",
                    Severity = "Info",
                    Details = "Extended time by 5 minute(s) for all participants"
                });

                if (_signalRService != null)
                {
                    try
                    {
                        if (!_signalRService.IsConnected)
                        {
                            await _signalRService.ConnectAsync();
                        }

                        await _signalRService.BroadcastMessageAsync(session.Id, "extend_time:5");
                    }
                    catch (Exception ex)
                    {
                        System.Diagnostics.Debug.WriteLine($"SignalR extend time failed: {ex.Message}");
                    }
                }
            }

            MessageBox.Show($"Extended {_liveSessions.Count} session(s) by 5 minutes.", "Extend Time", MessageBoxButton.OK, MessageBoxImage.Information);
        }

        private void OpenAnalytics_Click(object sender, RoutedEventArgs e)
        {
            NavigateToAnalytics();
        }

        private void NavigateToPublishedExams()
        {
            if (Window.GetWindow(this) is MainWindow main && main.FindName("MainContentHost") is ContentControl host)
            {
                host.Content = new published_exams_usercontrol();
            }
        }

        private void NavigateToAnalytics()
        {
            if (Window.GetWindow(this) is MainWindow main && main.FindName("MainContentHost") is ContentControl host)
            {
                host.Content = new student_analytics_usercontrol();
            }
        }

        private void NavigateToStructureBuilder()
        {
            if (Window.GetWindow(this) is MainWindow main && main.FindName("MainContentHost") is ContentControl host)
            {
                host.Content = new structure_builder_usercontrol();
            }
        }

        private class TodayScheduleItem
        {
            public string ExamId { get; set; } = string.Empty;
            public string Title { get; set; } = string.Empty;
            public string Schedule { get; set; } = string.Empty;
            public string Meta { get; set; } = string.Empty;
        }

    }
}
