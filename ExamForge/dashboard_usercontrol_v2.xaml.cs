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
    public partial class dashboard_usercontrol_v2 : UserControl
    {
        private readonly FirestoreService _firestoreService;
        private readonly AnalyticsService _analyticsService;
        private readonly SignalRService? _signalRService;
        private const string SignalRHubUrl = "https://examforge-signalr.onrender.com/sessionHub";

        private List<PublishedExam> _exams = new();
        private List<ExamSession> _liveSessions = new();
        private List<IntegrityIncident> _recentIncidents = new();
        private List<GradingQueueItem> _gradingQueue = new();
        private bool _signalrSubscribed = false;

        public dashboard_usercontrol_v2()
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
                var gradingTask = _firestoreService.GetGradingQueueItemsAsync();

                await Task.WhenAll(examsTask, sessionsTask, incidentsTask, gradingTask);

                _exams = examsTask.Result ?? new();
                _liveSessions = sessionsTask.Result ?? new();
                _recentIncidents = incidentsTask.Result ?? new();
                _gradingQueue = gradingTask.Result ?? new();

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
            UpdateKpis(_exams);
            UpdateHeroPanel();
            UpdateGradingQueue();
            _ = UpdatePerformanceSnapshotAsync();
        }

        private void UpdateKpis(List<PublishedExam> filtered)
        {
            var upcoming = filtered.Count(e => e.StartTime >= DateTime.UtcNow && e.StartTime <= DateTime.UtcNow.AddDays(7));
            var liveCount = _liveSessions.Count;
            var pendingGrading = _gradingQueue.Count(g => !g.PointsAwarded.HasValue);
            var flags = _recentIncidents.Count;

            UpcomingCountText.Text = upcoming > 0 ? upcoming.ToString() : "–";
            LiveCountText.Text = liveCount > 0 ? liveCount.ToString() : "–";
            GradingCountText.Text = pendingGrading > 0 ? pendingGrading.ToString() : "–";
            FlagsCountText.Text = flags > 0 ? flags.ToString() : "–";

            UpcomingSubtitle.Text = upcoming > 0 ? $"{upcoming} exams this week" : "Nothing scheduled";
            LiveSubtitle.Text = liveCount > 0 ? $"{liveCount} active sessions" : "No live exams";
            GradingSubtitle.Text = pendingGrading > 0 ? $"{pendingGrading} awaiting review" : "All caught up";
            FlagsSubtitle.Text = flags > 0 ? $"{flags} recent alerts" : "No incidents";
        }

        private void UpdateHeroPanel()
        {
            if (_liveSessions.Any())
            {
                // Show live monitor
                HeroPanelTitle.Text = "Live Exam Monitor";
                HeroActionButton.Content = "Open Monitor";
                HeroActionButton.Visibility = Visibility.Visible;
                LiveStatsPanel.Visibility = Visibility.Visible;
                TodayScheduleList.Visibility = Visibility.Collapsed;
                TodayEmptyState.Visibility = Visibility.Collapsed;

                var participants = _liveSessions.SelectMany(s => s.Participants.Values).ToList();
                var started = participants.Count;
                var submitted = participants.Count(p => p.ProgressPercent >= 100);
                var avgProgress = participants.Any() ? participants.Average(p => p.ProgressPercent) : 0;

                StartedCountRun.Text = started.ToString();
                SubmittedCountRun.Text = submitted.ToString();
                AvgProgressRun.Text = $"{avgProgress:F0}%";
            }
            else
            {
                // Show today's schedule
                HeroPanelTitle.Text = "Today's Schedule";
                HeroActionButton.Visibility = Visibility.Collapsed;
                LiveStatsPanel.Visibility = Visibility.Collapsed;

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
                TodayScheduleList.Visibility = todayItems.Any() ? Visibility.Visible : Visibility.Collapsed;
                TodayEmptyState.Visibility = todayItems.Any() ? Visibility.Collapsed : Visibility.Visible;
            }
        }

        private void UpdateGradingQueue()
        {
            var items = _gradingQueue.Take(3).Select(g => new GradingDisplayItem
            {
                Id = g.Id,
                ExamId = g.ExamId,
                ExamTitle = g.ExamTitle,
                Student = $"{g.StudentName} • Q{g.QuestionNumber}",
                QuestionNumber = g.QuestionNumber,
                QuestionText = g.QuestionText,
                StudentAnswer = g.StudentAnswer,
                MaxPoints = g.MaxPoints
            }).ToList();

            GradingQueueList.ItemsSource = items;
            GradingQueueList.Visibility = items.Any() ? Visibility.Visible : Visibility.Collapsed;
            GradingEmpty.Visibility = items.Any() ? Visibility.Collapsed : Visibility.Visible;
        }

        private async Task UpdatePerformanceSnapshotAsync()
        {
            var selectedExamId = _exams.FirstOrDefault()?.Id;
            if (string.IsNullOrEmpty(selectedExamId)) return;

            try
            {
                var metrics = await _analyticsService.CalculateClassOverviewAsync(selectedExamId);
                MeanScoreText.Text = double.IsNaN(metrics.ClassAverage) ? "--" : $"{metrics.ClassAverage:F0}%";
                PassRateText.Text = $"{metrics.PassRate:F0}%";
            }
            catch (Exception ex)
            {
                System.Diagnostics.Debug.WriteLine($"Error updating performance: {ex.Message}");
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

        private void GradingCard_Click(object sender, MouseButtonEventArgs e)
        {
            NavigateToGrading();
        }

        private void FlagsCard_Click(object sender, MouseButtonEventArgs e)
        {
            NavigateToAnalytics();
        }

        private void HeroAction_Click(object sender, RoutedEventArgs e)
        {
            NavigateToAnalytics();
        }

        private void OpenGrading_Click(object sender, RoutedEventArgs e)
        {
            NavigateToGrading();
        }

        private async void GradeQueueItem_Click(object sender, RoutedEventArgs e)
        {
            if (sender is not Button btn || btn.Tag is not string id)
                return;

            var gradingItem = _gradingQueue.FirstOrDefault(g => g.Id == id);
            if (gradingItem == null)
            {
                MessageBox.Show("Grading item not found.", "Grading", MessageBoxButton.OK, MessageBoxImage.Information);
                return;
            }

            var dialog = new Views.GradingDialog(
                gradingItem.Id,
                gradingItem.StudentName,
                gradingItem.QuestionNumber,
                gradingItem.QuestionText,
                gradingItem.StudentAnswer,
                gradingItem.MaxPoints)
            {
                Owner = Window.GetWindow(this)
            };

            var result = dialog.ShowDialog();
            if (result == true && dialog.PointsAwarded.HasValue)
            {
                try
                {
                    await _firestoreService.UpdateGradingQueueItemAsync(gradingItem.Id, dialog.PointsAwarded.Value, dialog.Feedback, "Instructor");
                    await RefreshGradingQueueAsync();
                    MessageBox.Show("Grade saved.", "Grading", MessageBoxButton.OK, MessageBoxImage.Information);
                }
                catch (Exception ex)
                {
                    MessageBox.Show($"Failed to save grade: {ex.Message}", "Grading", MessageBoxButton.OK, MessageBoxImage.Error);
                }
            }
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

        private void NavigateToGrading()
        {
            if (Window.GetWindow(this) is MainWindow main && main.FindName("MainContentHost") is ContentControl host)
            {
                var control = new published_exams_usercontrol();
                control.Loaded += (_, __) => control.ShowClosedTab();
                host.Content = control;
            }
        }

        private async Task RefreshGradingQueueAsync()
        {
            try
            {
                _gradingQueue = await _firestoreService.GetGradingQueueItemsAsync();
                UpdateDashboard();
            }
            catch (Exception ex)
            {
                System.Diagnostics.Debug.WriteLine($"RefreshGradingQueue failed: {ex.Message}");
            }
        }

        private class TodayScheduleItem
        {
            public string ExamId { get; set; } = string.Empty;
            public string Title { get; set; } = string.Empty;
            public string Schedule { get; set; } = string.Empty;
            public string Meta { get; set; } = string.Empty;
        }

        private class GradingDisplayItem
        {
            public string Id { get; set; } = string.Empty;
            public string ExamId { get; set; } = string.Empty;
            public string ExamTitle { get; set; } = string.Empty;
            public string Student { get; set; } = string.Empty;
            public int QuestionNumber { get; set; }
            public string QuestionText { get; set; } = string.Empty;
            public string StudentAnswer { get; set; } = string.Empty;
            public int MaxPoints { get; set; }
        }
    }
}
