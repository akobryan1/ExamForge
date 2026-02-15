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
        private List<GradingQueueItem> _gradingQueue = new();
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
            UpdateKpis();
            UpdateScheduleAndLiveMonitor();
            UpdateGradingQueue();
        }

        private void UpdateKpis()
        {
            var upcoming = _exams.Count(e => e.StartTime >= DateTime.UtcNow && e.StartTime <= DateTime.UtcNow.AddDays(7));
            var liveCount = _liveSessions.Count;
            var pendingGrading = _gradingQueue.Count(g => !g.PointsAwarded.HasValue);
            var flags = _recentIncidents.Count;

            UpcomingCountText.Text = upcoming > 0 ? upcoming.ToString() : "–";
            LiveCountText.Text = liveCount > 0 ? liveCount.ToString() : "–";
            GradingCountText.Text = pendingGrading > 0 ? pendingGrading.ToString() : "–";
            FlagsCountText.Text = flags > 0 ? flags.ToString() : "–";

            UpcomingSubtitle.Text = upcoming > 0 ? "Scheduled this week" : "Nothing scheduled";
            LiveSubtitle.Text = liveCount > 0 ? "Monitoring active" : "No live exams";
            GradingSubtitle.Text = pendingGrading > 0 ? "Needs review" : "All graded";
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

        private void UpdateGradingQueue()
        {
            var items = _gradingQueue.Take(5).Select(g => 
            {
                var isGraded = g.PointsAwarded.HasValue;
                var item = new GradingDisplayItem
                {
                    Id = g.Id,
                    ExamId = g.ExamId,
                    ExamTitle = g.ExamTitle,
                    Student = $"{g.StudentName} • Q{g.QuestionNumber}",
                    Due = g.SubmittedAt == default ? "Submitted" : $"Submitted {g.SubmittedAt:g}",
                    QuestionNumber = g.QuestionNumber,
                    QuestionText = g.QuestionText,
                    StudentAnswer = g.StudentAnswer,
                    MaxPoints = g.MaxPoints,
                    IsGraded = isGraded
                };

                // Set status text and color
                if (isGraded)
                {
                    item.StatusText = $"✓ Graded ({g.PointsAwarded}/{g.MaxPoints} pts)";
                    item.StatusColor = (System.Windows.Media.Brush)FindResource("SuccessTextBrush");
                    item.ButtonText = "Review";
                    item.ButtonStyle = (Style)FindResource("SecondaryButtonStyle");
                    item.RemoveButtonVisibility = Visibility.Visible;
                }
                else
                {
                    item.StatusText = "Pending";
                    item.StatusColor = (System.Windows.Media.Brush)FindResource("WarningTextBrush");
                    item.ButtonText = "Grade";
                    item.ButtonStyle = (Style)FindResource("PrimaryButtonStyle");
                    item.RemoveButtonVisibility = Visibility.Collapsed;
                }

                return item;
            }).ToList();

            GradingQueueList.ItemsSource = items;
            GradingEmpty.Visibility = items.Any() ? Visibility.Collapsed : Visibility.Visible;
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

        private async void RemoveGraded_Click(object sender, RoutedEventArgs e)
        {
            try
            {
                var gradedItems = _gradingQueue.Where(g => g.PointsAwarded.HasValue).ToList();
                if (!gradedItems.Any())
                {
                    MessageBox.Show("No graded items to remove.", "Remove Graded", MessageBoxButton.OK, MessageBoxImage.Information);
                    return;
                }

                var result = MessageBox.Show($"Remove {gradedItems.Count} graded item(s) from the queue?", 
                    "Remove Graded", MessageBoxButton.YesNo, MessageBoxImage.Question);
                
                if (result != MessageBoxResult.Yes) return;

                // Remove graded items from Firestore
                foreach (var item in gradedItems)
                {
                    await _firestoreService.RemoveGradingQueueItemAsync(item.Id);
                }

                // Refresh the queue
                await RefreshGradingQueueAsync();
                MessageBox.Show($"Removed {gradedItems.Count} graded item(s).", "Remove Graded", MessageBoxButton.OK, MessageBoxImage.Information);
            }
            catch (Exception ex)
            {
                MessageBox.Show($"Failed to remove graded items: {ex.Message}", "Error", MessageBoxButton.OK, MessageBoxImage.Error);
            }
        }

        private async void RemoveQueueItem_Click(object sender, RoutedEventArgs e)
        {
            if (sender is not Button btn || btn.Tag is not string id)
                return;

            var gradingItem = _gradingQueue.FirstOrDefault(g => g.Id == id);
            if (gradingItem == null)
            {
                MessageBox.Show("Grading item not found.", "Remove Item", MessageBoxButton.OK, MessageBoxImage.Information);
                return;
            }

            var result = MessageBox.Show($"Remove graded item for {gradingItem.StudentName}?", 
                "Remove Item", MessageBoxButton.YesNo, MessageBoxImage.Question);
            
            if (result != MessageBoxResult.Yes) return;

            try
            {
                await _firestoreService.RemoveGradingQueueItemAsync(id);
                await RefreshGradingQueueAsync();
                MessageBox.Show("Item removed.", "Remove Item", MessageBoxButton.OK, MessageBoxImage.Information);
            }
            catch (Exception ex)
            {
                MessageBox.Show($"Failed to remove item: {ex.Message}", "Error", MessageBoxButton.OK, MessageBoxImage.Error);
            }
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
            public string Due { get; set; } = string.Empty;
            public int QuestionNumber { get; set; }
            public string QuestionText { get; set; } = string.Empty;
            public string StudentAnswer { get; set; } = string.Empty;
            public int MaxPoints { get; set; }
            public bool IsGraded { get; set; }
            public string StatusText { get; set; } = string.Empty;
            public System.Windows.Media.Brush StatusColor { get; set; } = System.Windows.Media.Brushes.Gray;
            public string ButtonText { get; set; } = "Grade";
            public Style ButtonStyle { get; set; } = null!;
            public Visibility RemoveButtonVisibility { get; set; } = Visibility.Collapsed;
        }
    }
}
