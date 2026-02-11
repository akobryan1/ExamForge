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

        private string _selectedYear = "All Years";
        private string _selectedSubject = "All Subjects";
        private string _selectedExamId = string.Empty;
        private bool _filtersCollapsed = false;

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

                PopulateFilters();
                ApplyFilters();
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

                await Dispatcher.InvokeAsync(() => ApplyFilters());

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

        private void PopulateFilters()
        {
            var years = _exams.Select(e => e.PublishedDate.Year).Distinct().OrderByDescending(y => y).ToList();
            var subjects = _exams.Select(e => e.Subject).Where(s => !string.IsNullOrWhiteSpace(s)).Distinct().OrderBy(s => s).ToList();

            SchoolYearFilter.ItemsSource = new List<string> { "All Years" }.Concat(years.Select(y => $"{y}-{y + 1}")).ToList();
            SubjectFilter.ItemsSource = new List<string> { "All Subjects" }.Concat(subjects).ToList();
            SectionFilter.ItemsSource = new List<string> { "All Sections" };

            var examItems = _exams.OrderByDescending(e => e.PublishedDate).Select(e => new ExamFilterItem
            {
                ExamId = e.Id,
                Title = e.Title
            }).ToList();

            ExamFilter.ItemsSource = examItems;

            SchoolYearFilter.SelectedIndex = 0;
            SubjectFilter.SelectedIndex = 0;
            SectionFilter.SelectedIndex = 0;
            ExamFilter.SelectedIndex = examItems.Any() ? 0 : -1;

            _selectedExamId = examItems.FirstOrDefault()?.ExamId ?? string.Empty;
        }

        private void Filters_Changed(object sender, SelectionChangedEventArgs e)
        {
            _selectedYear = SchoolYearFilter.SelectedItem as string ?? "All Years";
            _selectedSubject = SubjectFilter.SelectedItem as string ?? "All Subjects";

            if (ExamFilter.SelectedItem is ExamFilterItem selectedExam)
            {
                _selectedExamId = selectedExam.ExamId;
            }

            ApplyFilters();
        }

        private void ApplyFilters()
        {
            var filtered = _exams.AsEnumerable();

            if (_selectedYear != "All Years" && int.TryParse(_selectedYear[..4], out var yearStart))
            {
                filtered = filtered.Where(e => e.PublishedDate.Year == yearStart);
            }

            if (_selectedSubject != "All Subjects")
            {
                filtered = filtered.Where(e => string.Equals(e.Subject, _selectedSubject, StringComparison.OrdinalIgnoreCase));
            }

            var filteredList = filtered.ToList();
            if (!filteredList.Any())
            {
                UpcomingCountText.Text = "0";
                LiveCountText.Text = "0";
                GradingCountText.Text = "0";
                FlagsCountText.Text = "0";
                TodayScheduleList.ItemsSource = null;
                TodayEmptyState.Visibility = Visibility.Visible;
                return;
            }

            if (string.IsNullOrEmpty(_selectedExamId) || !filteredList.Any(e => e.Id == _selectedExamId))
            {
                _selectedExamId = filteredList.First().Id;
            }

            UpdateKpis(filteredList);
            UpdateSchedule(filteredList);
            UpdateLiveMonitor();
            UpdateGradingQueue();
            _ = UpdateSubmissionsHealthAsync();
            _ = UpdateInsightsAsync();
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

            UpcomingSubtitle.Text = upcoming > 0 ? "Scheduled this week" : "Nothing scheduled";
            LiveSubtitle.Text = liveCount > 0 ? "Monitoring active" : "No live exams";
            GradingSubtitle.Text = pendingGrading > 0 ? "Needs review" : "All graded";
            FlagsSubtitle.Text = flags > 0 ? "Review incidents" : "No alerts";
        }

        private void UpdateSchedule(List<PublishedExam> filtered)
        {
            var today = DateTime.UtcNow.Date;
            var todayItems = filtered
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
        }

        private void UpdateLiveMonitor()
        {
            if (!_liveSessions.Any())
            {
                LiveEmpty.Visibility = Visibility.Visible;
                LiveEmpty.Text = "? No exams running right now. Start one to see live stats!";
                StartedCountText.Text = "–";
                SubmittedCountText.Text = "–";
                AvgProgressText.Text = "–";
                LastActivityText.Text = "–";
                return;
            }

            LiveEmpty.Visibility = Visibility.Collapsed;
            var participants = _liveSessions.SelectMany(s => s.Participants.Values).ToList();
            var started = participants.Count;
            var avgProgress = participants.Any() ? participants.Average(p => p.ProgressPercent) : 0;

            StartedCountText.Text = started.ToString();
            SubmittedCountText.Text = _gradingQueue.Count(g => g.PointsAwarded.HasValue).ToString();
            AvgProgressText.Text = $"{avgProgress:F0}%";
            LastActivityText.Text = _liveSessions.Max(s => s.LastHeartbeat).ToLocalTime().ToString("t");
        }

        private void UpdateGradingQueue()
        {
            var items = _gradingQueue.Take(5).Select(g => new GradingDisplayItem
            {
                Id = g.Id,
                ExamId = g.ExamId,
                ExamTitle = g.ExamTitle,
                Student = $"{g.StudentName} • Q{g.QuestionNumber} ({g.QuestionType})",
                Due = g.SubmittedAt == default ? "Submitted" : $"Submitted {g.SubmittedAt:g}",
                QuestionNumber = g.QuestionNumber,
                QuestionText = g.QuestionText,
                StudentAnswer = g.StudentAnswer,
                MaxPoints = g.MaxPoints
            }).ToList();

            GradingQueueList.ItemsSource = items;
            if (!items.Any())
            {
                GradingEmpty.Visibility = Visibility.Visible;
                GradingEmpty.Text = "?? All caught up! No grading needed.";
            }
            else
            {
                GradingEmpty.Visibility = Visibility.Collapsed;
            }
        }

        private async Task UpdateSubmissionsHealthAsync()
        {
            if (string.IsNullOrEmpty(_selectedExamId))
            {
                NotStartedText.Text = "0";
                InProgressText.Text = "0";
                SubmittedHealthText.Text = "0";
                MissingText.Text = "0";
                return;
            }

            try
            {
                var submissions = await _firestoreService.GetExamSubmissionsAsync(_selectedExamId);
                var sessionParticipants = _liveSessions.SelectMany(s => s.Participants.Values).ToList();
                var inProgress = sessionParticipants.Count(p => p.ProgressPercent < 100);
                var submitted = submissions.Count;

                // Without roster size, estimate total from live participants + submitted
                var total = Math.Max(submitted + inProgress, _liveSessions.Sum(s => s.TotalParticipants));
                var notStarted = Math.Max(total - inProgress - submitted, 0);

                NotStartedText.Text = notStarted.ToString();
                InProgressText.Text = inProgress.ToString();
                SubmittedHealthText.Text = submitted.ToString();
                MissingText.Text = Math.Max(total - submitted, 0).ToString();
            }
            catch (Exception ex)
            {
                System.Diagnostics.Debug.WriteLine($"Error updating submission health: {ex.Message}");
            }
        }

        private async Task UpdateInsightsAsync()
        {
            if (string.IsNullOrEmpty(_selectedExamId)) return;

            try
            {
                var metrics = await _analyticsService.CalculateClassOverviewAsync(_selectedExamId);

                MeanScoreText.Text = double.IsNaN(metrics.ClassAverage) ? "--" : $"{metrics.ClassAverage:F1}%";
                MedianScoreText.Text = $"{metrics.CompletionRate:F1}%";
                PassRateText.Text = $"{metrics.PassRate:F0}%";
                MasteryText.Text = metrics.TotalStudents > 0 ? $"{metrics.PassingStudents * 100.0 / metrics.TotalStudents:F0}%" : "--";

                var submissions = await _firestoreService.GetExamSubmissionsAsync(_selectedExamId);
                var itemInsights = submissions.SelectMany(s => s.Responses).GroupBy(r => r.QuestionId)
                    .Select(g => new
                    {
                        QuestionId = g.Key,
                        CorrectRate = g.Any() ? g.Count(r => r.IsCorrect) * 100.0 / g.Count() : 0
                    })
                    .OrderBy(x => x.CorrectRate)
                    .Take(3)
                    .Select(x => $"{x.QuestionId}: {x.CorrectRate:F0}% correct")
                    .ToList();

                TopMissedList.ItemsSource = itemInsights;
                PerformanceEmpty.Visibility = itemInsights.Any() ? Visibility.Collapsed : Visibility.Visible;

                var masteryItems = new List<MasteryDisplay>();
                var currentExam = _exams.FirstOrDefault(e => e.Id == _selectedExamId);
                if (currentExam?.Structures != null)
                {
                    masteryItems = currentExam.Structures.Take(4).Select(s => new MasteryDisplay
                    {
                        Label = s.SectionName,
                        Percent = metrics.PassRate,
                        PercentText = $"{metrics.PassRate:F0}%"
                    }).ToList();
                }
                MasteryList.ItemsSource = masteryItems;

                var atRisk = submissions
                    .Select(s => new AtRiskDisplay
                    {
                        Name = s.StudentName,
                        Percentage = s.TotalPossiblePoints > 0 ? $"{(s.TotalScore / s.TotalPossiblePoints) * 100:F0}%" : "0%"
                    })
                    .Where(s => double.TryParse(s.Percentage.Trim('%'), out var p) && p < 60)
                    .Take(4)
                    .ToList();
                AtRiskList.ItemsSource = atRisk;
                AtRiskEmpty.Visibility = atRisk.Any() ? Visibility.Collapsed : Visibility.Visible;

                var signals = _recentIncidents.Take(5).Select(i => $"{i.IncidentType} • {i.StudentName} • {i.Timestamp:g}").ToList();
                IntegrityList.ItemsSource = signals;
                IntegrityEmpty.Visibility = signals.Any() ? Visibility.Collapsed : Visibility.Visible;
            }
            catch (Exception ex)
            {
                System.Diagnostics.Debug.WriteLine($"Error updating insights: {ex.Message}");
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

        private async void ExtendTime_Click(object sender, RoutedEventArgs e)
        {
            if (!_liveSessions.Any())
            {
                MessageBox.Show("No live sessions to extend.", "Extend Time", MessageBoxButton.OK, MessageBoxImage.Information);
                return;
            }

            var dialog = new SimplePromptWindow("Extend time by (minutes):", "5");
            if (dialog.ShowDialog() == true && int.TryParse(dialog.ResultText, out var minutes) && minutes > 0)
            {
                var addedSeconds = minutes * 60;

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
                        Details = $"Extended time by {minutes} minute(s) for all participants"
                    });

                    if (_signalRService != null)
                    {
                        try
                        {
                            if (!_signalRService.IsConnected)
                            {
                                await _signalRService.ConnectAsync();
                            }

                            await _signalRService.BroadcastMessageAsync(session.Id, $"extend_time:{minutes}");
                        }
                        catch (Exception ex)
                        {
                            System.Diagnostics.Debug.WriteLine($"SignalR extend time failed: {ex.Message}");
                        }
                    }
                }

                MessageBox.Show($"Extended {_liveSessions.Count} session(s) by {minutes} minute(s).", "Extend Time", MessageBoxButton.OK, MessageBoxImage.Information);
            }
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
                ApplyFilters();
            }
            catch (Exception ex)
            {
                System.Diagnostics.Debug.WriteLine($"RefreshGradingQueue failed: {ex.Message}");
            }
        }

        private void ToggleFilters_Click(object sender, RoutedEventArgs e)
        {
            _filtersCollapsed = !_filtersCollapsed;
            GlobalFiltersPanel.Visibility = _filtersCollapsed ? Visibility.Collapsed : Visibility.Visible;
            if (sender is Button btn)
            {
                btn.Content = _filtersCollapsed ? "Show" : "Hide";
            }
        }

        

        

        

        private class SimplePromptWindow : Window
        {
            public string ResultText { get; private set; } = string.Empty;

            public SimplePromptWindow(string prompt, string defaultValue)
            {
                Title = prompt;
                Width = 320;
                Height = 160;
                WindowStartupLocation = WindowStartupLocation.CenterOwner;
                Background = (System.Windows.Media.Brush)Application.Current.FindResource("SurfaceDark");
                Foreground = (System.Windows.Media.Brush)Application.Current.FindResource("TextPrimary");

                var panel = new StackPanel { Margin = new Thickness(12) };
                panel.Children.Add(new TextBlock { Text = prompt, Margin = new Thickness(0, 0, 0, 8), Foreground = Foreground });
                var box = new TextBox { Text = defaultValue, Margin = new Thickness(0, 0, 0, 12) };
                panel.Children.Add(box);

                var buttons = new StackPanel { Orientation = Orientation.Horizontal, HorizontalAlignment = HorizontalAlignment.Right };
                var ok = new Button { Content = "OK", Width = 64, Margin = new Thickness(0, 0, 8, 0) };
                ok.Click += (_, __) => { ResultText = box.Text; DialogResult = true; Close(); };
                var cancel = new Button { Content = "Cancel", Width = 64 };
                cancel.Click += (_, __) => { DialogResult = false; Close(); };
                buttons.Children.Add(ok);
                buttons.Children.Add(cancel);

                panel.Children.Add(buttons);
                Content = panel;
            }
        }

        private class ExamFilterItem
        {
            public string ExamId { get; set; } = string.Empty;
            public string Title { get; set; } = string.Empty;

            public override string ToString() => Title;
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
        }

        private class MasteryDisplay
        {
            public string Label { get; set; } = string.Empty;
            public double Percent { get; set; }
            public string PercentText { get; set; } = string.Empty;
        }

        private class AtRiskDisplay
        {
            public string Name { get; set; } = string.Empty;
            public string Percentage { get; set; } = string.Empty;
        }
    }
}
