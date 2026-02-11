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
using ExamForge.Views;

namespace ExamForge;

public partial class published_exams_usercontrol : UserControl
{
    private List<PublishedExamViewModel> _allExams = new();
    private string _currentFilter = "All";
    private string _searchQuery = "";
    private List<ExamBankItem> _examBankItems = new();
    private string _examBankSearch = "";
    private string _examBankSubject = "All Subjects";

    // SignalR client  
    private SignalRService? _signalRService;
    private const string SignalRHubUrl = "https://examforge-signalr.onrender.com/sessionHub";

    // Live monitoring data
    private ObservableCollection<StudentRosterItem> _rosterItems = new();
    private ObservableCollection<IncidentItem> _incidentFeed = new();
    private List<Models.GradingQueueItem> _gradingQueueItems = new();
    
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

    public void ShowClosedTab()
    {
        if (ClosedTab != null)
        {
            ClosedTab.IsChecked = true;
            Tab_Changed(ClosedTab, new RoutedEventArgs());
        }
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
                    Subject = exam.Subject,
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

                var sessions = await firestoreService.GetRunningSessionsAsync();

                var participants = new List<StudentRosterItem>();
                int online = 0, disconnected = 0, flags = 0, progressSum = 0, progressCount = 0;

                foreach (var session in sessions)
                {
                    foreach (var participant in session.Participants.Values)
                    {
                        participants.Add(new StudentRosterItem
                        {
                            StudentId = participant.StudentId,
                            StudentName = participant.StudentName,
                            ConnectionStatus = participant.ConnectionStatus,
                            ProgressPercent = $"{participant.ProgressPercent}%",
                            TimeRemaining = FormatTimeRemaining(TimeSpan.FromSeconds(participant.TimeRemaining)),
                            FlagCount = participant.FlagCount.ToString()
                        });

                        if (participant.ConnectionStatus?.Equals("Online", StringComparison.OrdinalIgnoreCase) == true)
                            online++;
                        else
                            disconnected++;

                        flags += participant.FlagCount;
                        progressSum += participant.ProgressPercent;
                        progressCount++;
                    }
                }

                var avgProgress = progressCount > 0 ? Math.Round(progressSum / (double)progressCount) : 0;

                if (!participants.Any())
                {
                    participants.Add(new StudentRosterItem
                    {
                        StudentId = "none",
                        StudentName = "No active sessions",
                        ConnectionStatus = "WAITING",
                        ProgressPercent = "0%",
                        TimeRemaining = "N/A",
                        FlagCount = "0"
                    });
                }

                await Dispatcher.InvokeAsync(() =>
                {
                    _rosterItems.Clear();
                    foreach (var p in participants) _rosterItems.Add(p);

                    if (LiveRosterGrid != null)
                        LiveRosterGrid.ItemsSource = _rosterItems;

                    if (ActiveSessionsText != null) ActiveSessionsText.Text = $"{sessions.Count} Active Sessions";
                    if (LiveOnlineValue != null) LiveOnlineValue.Text = online.ToString();
                    if (LiveDisconnectedValue != null) LiveDisconnectedValue.Text = disconnected.ToString();
                    if (LiveProgressValue != null) LiveProgressValue.Text = $"{avgProgress}%";
                    if (LiveFlagsValue != null) LiveFlagsValue.Text = flags.ToString();
                });
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
            _examBankItems = publishedExams.Select(exam => new ExamBankItem
            {
                Id = exam.Id,
                Title = exam.Title,
                Subject = string.IsNullOrWhiteSpace(exam.Subject) ? "General" : exam.Subject,
                QuestionCount = exam.Contents?.Count ?? 0,
                TimesUsed = exam.TimesUsed,
                LastUsed = exam.PublishedDate,
                CreatedBy = exam.CreatedBy,
                DateCreated = exam.PublishedDate,
                IsPublished = exam.Status == "Published"
            }).ToList();

            // Populate subject filter dynamically
            if (SubjectFilter != null)
            {
                SubjectFilter.Items.Clear();
                SubjectFilter.Items.Add(new ComboBoxItem { Content = "All Subjects" });
                foreach (var subj in _examBankItems.Select(i => i.Subject).Where(s => !string.IsNullOrWhiteSpace(s)).Distinct().OrderBy(s => s))
                {
                    SubjectFilter.Items.Add(new ComboBoxItem { Content = subj });
                }
                SubjectFilter.SelectedIndex = 0;
            }

            ApplyExamBankFilters();
            Debug.WriteLine($"?? Loaded {_examBankItems.Count} exams from Firestore");
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

            // Load grading queue items (one per essay/short-answer)
            _gradingQueueItems = await firestoreService.GetGradingQueueItemsAsync();

            var pendingItems = _gradingQueueItems
                .Where(i => i.Status != "Graded")
                .OrderByDescending(i => i.SubmittedAt)
                .Select(item => new GradingQueueItemViewModel
                {
                    Id = item.Id,
                    ExamId = item.ExamId,
                    StudentName = item.StudentName,
                    ExamTitle = item.ExamTitle,
                    SubmittedAt = item.SubmittedAt,
                    AutoScore = item.PointsAwarded.HasValue ? $"{item.PointsAwarded}/{item.MaxPoints}" : $"0/{item.MaxPoints}",
                    EssayCount = 1,
                    Status = item.Status,
                    SubmissionId = item.SubmissionId,
                    QuestionNumber = item.QuestionNumber,
                    QuestionText = item.QuestionText,
                    StudentAnswer = item.StudentAnswer,
                    MaxPoints = item.MaxPoints
                })
                .ToList();

            var gradedItems = _gradingQueueItems
                .Where(i => i.Status == "Graded")
                .OrderByDescending(i => i.GradedAt ?? i.SubmittedAt)
                .Select(item => new GradingQueueItemViewModel
                {
                    Id = item.Id,
                    ExamId = item.ExamId,
                    StudentName = item.StudentName,
                    ExamTitle = item.ExamTitle,
                    SubmittedAt = item.SubmittedAt,
                    AutoScore = item.PointsAwarded.HasValue ? $"{item.PointsAwarded}/{item.MaxPoints}" : $"0/{item.MaxPoints}",
                    EssayCount = 1,
                    Status = item.Status,
                    SubmissionId = item.SubmissionId,
                    QuestionNumber = item.QuestionNumber,
                    QuestionText = item.QuestionText,
                    StudentAnswer = item.StudentAnswer,
                    MaxPoints = item.MaxPoints
                })
                .ToList();

            var pendingGrid = GradingQueueGrid ?? this.FindName("GradingQueueGrid") as DataGrid;
            var gradedGrid = this.FindName("GradedEssayGrid") as DataGrid;

            if (pendingGrid != null)
                pendingGrid.ItemsSource = pendingItems;
            if (gradedGrid != null)
                gradedGrid.ItemsSource = gradedItems;

            var pendingCount = pendingItems.Count;
            var gradedCount = gradedItems.Count;

            if (PendingReviewBadge != null)
                PendingReviewBadge.Text = $"{pendingCount} Pending Review";
            if (ManualReviewValue != null)
                ManualReviewValue.Text = pendingCount.ToString();
            if (AutoGradedValue != null)
                AutoGradedValue.Text = gradedCount.ToString();

            // Get completed exams for metrics
            var publishedExams = await firestoreService.GetAllPublishedExamsAsync();
            var completedExams = publishedExams
                .Where(e => e.EndTime < DateTime.UtcNow)
                .OrderByDescending(e => e.EndTime)
                .Take(20)
                .ToList();

            var allSubmissions = new List<ExamSubmission>();
            
            foreach (var exam in completedExams)
            {
                try
                {
                    var submissions = await firestoreService.GetExamSubmissionsAsync(exam.Id);
                    allSubmissions.AddRange(submissions);
                }
                catch (Exception ex)
                {
                    Debug.WriteLine($"Error loading submissions for exam {exam.Title}: {ex.Message}");
                }
            }

            UpdateClosedTabMetrics(allSubmissions);
            Debug.WriteLine($"📝 Loaded {pendingItems.Count} pending and {gradedItems.Count} graded items");
        }
        catch (Exception ex)
        {
            Debug.WriteLine($"Error loading closed exams: {ex.Message}");
            ShowError($"Failed to load grading queue: {ex.Message}");
        }
    }

    #endregion

    #region Event Handlers

    private async void ReviewSubmission_Click(object sender, RoutedEventArgs e)
    {
        if (sender is not Button btn || btn.Tag is not string gradingItemId)
            return;

        var gradingItem = _gradingQueueItems.FirstOrDefault(i => i.Id == gradingItemId);
        if (gradingItem == null)
        {
            ShowError("Grading item not found.");
            return;
        }

        var dialog = new GradingDialog(
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
            var firestoreService = App.FirestoreService;
            if (firestoreService == null)
            {
                ShowError("Firestore service not initialized");
                return;
            }

            try
            {
                await firestoreService.UpdateGradingQueueItemAsync(
                    gradingItem.Id,
                    dialog.PointsAwarded.Value,
                    dialog.Feedback,
                    "Instructor");

                await LoadClosedExamsAsync();
            }
            catch (Exception ex)
            {
                ShowError($"Failed to save grade: {ex.Message}");
            }
        }
    }

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

    private void BankSearch_Changed(object sender, TextChangedEventArgs e)
    {
        if (BankSearchBox.Text == "Search exam bank...") return;
        _examBankSearch = BankSearchBox.Text ?? "";
        ApplyExamBankFilters();
    }

    private void SubjectFilter_Changed(object sender, SelectionChangedEventArgs e)
    {
        if (SubjectFilter?.SelectedItem is ComboBoxItem item)
            _examBankSubject = item.Content?.ToString() ?? "All Subjects";
        ApplyExamBankFilters();
    }

    private void ApplyExamBankFilters()
    {
        if (_examBankItems == null || !_examBankItems.Any())
        {
            if (ExamBankGrid != null) ExamBankGrid.ItemsSource = new List<ExamBankItem>();
            return;
        }

        var filtered = _examBankItems.AsEnumerable();

        if (!string.IsNullOrWhiteSpace(_examBankSearch))
        {
            var query = _examBankSearch.ToLower();
            filtered = filtered.Where(i =>
                i.Title.ToLower().Contains(query) ||
                (i.Subject ?? "").ToLower().Contains(query));
        }

        if (!string.IsNullOrWhiteSpace(_examBankSubject) && _examBankSubject != "All Subjects")
        {
            filtered = filtered.Where(i => string.Equals(i.Subject, _examBankSubject, StringComparison.OrdinalIgnoreCase));
        }

        var list = filtered.ToList();
        if (ExamBankGrid != null)
            ExamBankGrid.ItemsSource = list;
    }

    private void UpdateClosedTabMetrics(List<ExamSubmission> submissions)
    {
        if (AutoGradedValue == null || ManualReviewValue == null || PassRateValue == null || PendingReviewBadge == null || GradeABar == null)
            return;

        var total = submissions.Count;
        var manual = submissions.Count(s => s.Status?.Contains("manual", StringComparison.OrdinalIgnoreCase) == true);
        var auto = total - manual;

        double passRate = 0;
        if (total > 0)
        {
            var passing = submissions.Count(s => s.TotalPossiblePoints > 0 && (s.TotalScore / s.TotalPossiblePoints) * 100 >= 60);
            passRate = (passing * 100.0) / total;
        }

        AutoGradedValue.Text = auto.ToString();
        ManualReviewValue.Text = manual.ToString();
        PassRateValue.Text = $"{passRate:F0}%";
        PendingReviewBadge.Text = $"{manual} Pending Review";

        // Grade distribution
        if (total == 0)
        {
            GradeAValue.Text = " 0%"; GradeBValue.Text = " 0%"; GradeCValue.Text = " 0%"; GradeFValue.Text = " 0%";
            GradeABar.Width = GradeBBar.Width = GradeCBar.Width = GradeFBar.Width = 0;
            return;
        }

        var percents = submissions.Select(s => s.TotalPossiblePoints > 0 ? (s.TotalScore / s.TotalPossiblePoints) * 100 : 0).ToList();
        double aPct = percents.Count(p => p >= 90) * 100.0 / total;
        double bPct = percents.Count(p => p >= 80 && p < 90) * 100.0 / total;
        double cPct = percents.Count(p => p >= 70 && p < 80) * 100.0 / total;
        double fPct = percents.Count(p => p < 70) * 100.0 / total;

        GradeAValue.Text = $" {aPct:F0}%";
        GradeBValue.Text = $" {bPct:F0}%";
        GradeCValue.Text = $" {cPct:F0}%";
        GradeFValue.Text = $" {fPct:F0}%";

        const double barBase = 80;
        GradeABar.Width = aPct > 0 ? Math.Max(5, barBase * aPct / 100) : 0;
        GradeBBar.Width = bPct > 0 ? Math.Max(5, barBase * bPct / 100) : 0;
        GradeCBar.Width = cPct > 0 ? Math.Max(5, barBase * cPct / 100) : 0;
        GradeFBar.Width = fPct > 0 ? Math.Max(5, barBase * fPct / 100) : 0;
    }

    private void CloneExam_Click(object sender, RoutedEventArgs e)
    {
        // This method is no longer used - replaced by ReuseExam_Click
    }

    private void EditBankExam_Click(object sender, RoutedEventArgs e)
    {
        // This method is no longer used - replaced by ReuseExam_Click
    }

    private async void ReuseExam_Click(object sender, RoutedEventArgs e)
    {
        if (sender is Button btn && btn.Tag is string examId)
        {
            try
            {
                var firestoreService = App.FirestoreService;
                if (firestoreService == null)
                {
                    ShowError("Firestore service not initialized");
                    return;
                }

                // Load the exam from Firestore
                var publishedExams = await firestoreService.GetAllPublishedExamsAsync();
                var exam = publishedExams.FirstOrDefault(e => e.Id == examId);

                if (exam == null)
                {
                    ShowError("Exam not found");
                    return;
                }

                // Navigate to exam creation and load the exam data as a template
                var mainWindow = Window.GetWindow(this) as MainWindow;
                if (mainWindow != null)
                {
                    await mainWindow.LoadExamForEditing(examId);
                }
            }
            catch (Exception ex)
            {
                ShowError($"Failed to reuse exam: {ex.Message}");
            }
        }
    }

    private async void CopyExamLink_Click(object sender, RoutedEventArgs e)
    {
        if (sender is Button btn && btn.Tag is string examId)
        {
            try
            {
                var firestoreService = App.FirestoreService;
                if (firestoreService == null)
                {
                    ShowError("Firestore service not initialized");
                    return;
                }

                // Get the exam URL
                var publishedExams = await firestoreService.GetAllPublishedExamsAsync();
                var exam = publishedExams.FirstOrDefault(e => e.Id == examId);

                if (exam == null || string.IsNullOrEmpty(exam.ExamUrl))
                {
                    ShowError("Exam URL not found");
                    return;
                }

                // Copy to clipboard
                Clipboard.SetText(exam.ExamUrl);
                MessageBox.Show("Exam URL copied to clipboard!", "Success",
                    MessageBoxButton.OK, MessageBoxImage.Information);
            }
            catch (Exception ex)
            {
                ShowError($"Failed to copy exam link: {ex.Message}");
            }
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

    private async void ExportGradebook_Click(object sender, RoutedEventArgs e)
    {
        try
        {
            // Use selected row from pending or graded grid
            var pendingGrid = GradingQueueGrid ?? this.FindName("GradingQueueGrid") as DataGrid;
            var gradedGrid = this.FindName("GradedEssayGrid") as DataGrid;

            var vm = (pendingGrid?.SelectedItem as GradingQueueItemViewModel)
                ?? (gradedGrid?.SelectedItem as GradingQueueItemViewModel);

            if (vm == null)
            {
                ShowError("Select a submission row first to export the gradebook for that exam.");
                return;
            }

            var exportService = new ExportService();
            var path = await exportService.ExportGradesToExcelAsync(vm.ExamId, vm.ExamTitle);
            MessageBox.Show($"Gradebook exported to: {path}", "Success", MessageBoxButton.OK, MessageBoxImage.Information);
        }
        catch (Exception ex)
        {
            ShowError($"Failed to export gradebook: {ex.Message}");
        }
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
    public int TimesUsed { get; set; }
    public DateTime LastUsed { get; set; }
    public string CreatedBy { get; set; } = "";
    public DateTime DateCreated { get; set; }
    public bool IsPublished { get; set; }
}

public class GradingQueueItemViewModel
{
    public string Id { get; set; } = "";
    public string ExamId { get; set; } = "";
    public string StudentName { get; set; } = "";
    public string ExamTitle { get; set; } = "";
    public DateTime SubmittedAt { get; set; }
    public string AutoScore { get; set; } = "";
    public int EssayCount { get; set; }
    public string Status { get; set; } = "Pending"; // Default status
    public string SubmissionId { get; set; } = "";
    public int QuestionNumber { get; set; }
    public string QuestionText { get; set; } = "";
    public string StudentAnswer { get; set; } = "";
    public int MaxPoints { get; set; }
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
    public string Subject { get; set; } = "General";
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