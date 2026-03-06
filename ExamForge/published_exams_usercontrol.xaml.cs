using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.Diagnostics;
using System.IO;
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
    private System.Windows.Threading.DispatcherTimer? _gradingQueueTimer;
    private const int LIVE_REFRESH_INTERVAL = 5000; // 5 seconds
    private const int GRADING_QUEUE_REFRESH_INTERVAL = 10000; // 10 seconds

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
        StopGradingQueueRefresh();
    }

    public void ShowClosedTab()
    {
        if (ScheduledTab != null)
        {
            ScheduledTab.IsChecked = true;
            Tab_Changed(ScheduledTab, new RoutedEventArgs());
        }
    }

    private void SetLoadingState(bool isLoading, string message = "Loading...")
    {
        if (Window.GetWindow(this) is MainWindow mainWindow)
        {
            mainWindow.SetLoading(isLoading, message);
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
                ExamUrl = ResolveExamUrl(exam.Id, exam.ExamUrl),
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

    private string ResolveExamUrl(string examId, string? currentUrl)
    {
        var reusableUrl = App.PublishingService?.BuildReusableExamUrl(examId);
        if (!string.IsNullOrWhiteSpace(reusableUrl))
            return reusableUrl;

        return currentUrl ?? string.Empty;
    }

    private static string MapQuestionTypeToTestType(string? questionType)
    {
        return questionType switch
        {
            "True/False" => "True or False",
            "Modified True/False" => "Modified True or False",
            "Short Answer" => "Identification",
            "Essay" => "Essay",
            "Multiple Choice" => "Multiple Choice",
            _ => "Multiple Choice"
        };
    }

    private static string GetTestGroupLabel(int index)
    {
        string[] groups = { "I", "II", "III", "IV", "V", "VI", "VII", "VIII", "IX", "X", "XI", "XII" };
        return index < groups.Length ? groups[index] : $"G{index + 1}";
    }

    private ExamConfiguration BuildTemplateConfiguration(PublishedExam exam)
    {
        var contents = exam.Contents ?? new List<ExamContent>();
        var totalItems = Math.Max(1, contents.Count);

        var rows = new List<ExamStructureRow>();
        int currentStart = 1;
        int groupIndex = 0;

        if (exam.Structures != null && exam.Structures.Count > 0)
        {
            foreach (var structure in exam.Structures)
            {
                if (currentStart > totalItems) break;

                var count = Math.Max(1, structure.QuestionCount);
                var end = Math.Min(totalItems, currentStart + count - 1);
                var firstItem = contents.ElementAtOrDefault(currentStart - 1);
                var testType = MapQuestionTypeToTestType(firstItem?.QuestionType ?? structure.Description);
                var pointsPerItem = Math.Max(1, firstItem?.Points ?? (count > 0 ? structure.Points / count : 1));

                rows.Add(new ExamStructureRow
                {
                    TestGroup = GetTestGroupLabel(groupIndex++),
                    TestType = testType,
                    Start = currentStart.ToString(),
                    End = end.ToString(),
                    Points = pointsPerItem.ToString()
                });

                currentStart = end + 1;
            }
        }

        if (rows.Count == 0)
        {
            rows.Add(new ExamStructureRow
            {
                TestGroup = "I",
                TestType = MapQuestionTypeToTestType(contents.FirstOrDefault()?.QuestionType),
                Start = "1",
                End = totalItems.ToString(),
                Points = Math.Max(1, contents.FirstOrDefault()?.Points ?? 1).ToString()
            });
        }

        var contentItems = contents.Select((content, idx) =>
        {
            var number = idx + 1;
            var matchedRow = rows.FirstOrDefault(r => int.TryParse(r.Start, out var s) && int.TryParse(r.End, out var e) && number >= s && number <= e);
            var testType = MapQuestionTypeToTestType(content.QuestionType);
            var options = content.Options ?? new List<string>();

            return new ExamItem
            {
                Number = number,
                Title = $"Item {number}",
                TestGroup = matchedRow?.TestGroup ?? "I",
                TestType = testType,
                Points = Math.Max(1, content.Points).ToString(),
                Status = string.IsNullOrWhiteSpace(content.QuestionText) ? "Incomplete" : "Complete",
                Question = content.QuestionText,
                OptionA = options.ElementAtOrDefault(0) ?? string.Empty,
                OptionB = options.ElementAtOrDefault(1) ?? string.Empty,
                OptionC = options.ElementAtOrDefault(2) ?? string.Empty,
                OptionD = options.ElementAtOrDefault(3) ?? string.Empty,
                CorrectAnswer = content.CorrectAnswer,
                TrueFalseAnswer = string.Equals(content.CorrectAnswer, "True", StringComparison.OrdinalIgnoreCase),
                ModifiedAnswer = content.CorrectAnswer,
                TextAnswer = content.CorrectAnswer,
                EnumerationAnswers = string.IsNullOrWhiteSpace(content.CorrectAnswer)
                    ? new List<string>()
                    : content.CorrectAnswer.Split(',', StringSplitOptions.RemoveEmptyEntries).Select(x => x.Trim()).ToList()
            };
        }).ToList();

        var duration = Math.Max(1, exam.ExamDuration);
        var config = new ExamConfiguration
        {
            ExamName = exam.Title,
            SubjectName = string.IsNullOrWhiteSpace(exam.Subject) ? "General" : exam.Subject,
            NumberOfItems = totalItems,
            DataGridState = new DataGridState
            {
                Rows = rows,
                TotalItems = totalItems
            },
            TimingState = new TimingState
            {
                Hours = (duration / 60).ToString(),
                Minutes = (duration % 60).ToString("00"),
                Seconds = "00",
                StartDateTime = exam.StartTime,
                EndDateTime = exam.EndTime
            },
            LoginConfigState = exam.LoginConfig ?? new LoginConfigState { IsGoogleSignIn = true },
            AntiCheatState = exam.AntiCheat ?? new AntiCheatState(),
            ContentBuilderState = contentItems
        };

        return config;
    }

    private void OpenExamTemplateInBuilder(PublishedExam exam)
    {
        var mainWindow = Window.GetWindow(this) as MainWindow;
        if (mainWindow == null)
        {
            ShowError("Unable to access main window.");
            return;
        }

        if (mainWindow.FindName("MainContentHost") is not ContentControl contentHost)
        {
            ShowError("Unable to access content host.");
            return;
        }

        var configuration = BuildTemplateConfiguration(exam);
        var structureBuilder = new structure_builder_usercontrol();
        structureBuilder.RestoreFromConfiguration(configuration);
        mainWindow.SetStructureBuilder(structureBuilder);

        var contentBuilder = new content_builder();
        contentBuilder.LoadConfiguration(configuration);
        contentHost.Content = contentBuilder;
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

        // Set the ItemsSource to display the filtered exams
        ExamCardsContainer.ItemsSource = filteredList;

        if (filteredList.Count == 0)
        {
            ExamCardsContainer.Visibility = Visibility.Collapsed;
            EmptyState.Visibility = Visibility.Visible;
        }
        else
        {
            ExamCardsContainer.Visibility = Visibility.Visible;
            EmptyState.Visibility = Visibility.Collapsed;
        }

        Debug.WriteLine($"✅ Applied filters: {filteredList.Count} exams displayed (Filter: {_currentFilter}, Search: '{_searchQuery}')");
    }

    #region Tab Event Handlers

    private async void Tab_Changed(object sender, RoutedEventArgs e)
    {
        SetLoadingState(true, "Loading tab...");
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
        finally
        {
            SetLoadingState(false);
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

    private void StartGradingQueueRefresh()
    {
        StopGradingQueueRefresh();

        _gradingQueueTimer = new System.Windows.Threading.DispatcherTimer
        {
            Interval = TimeSpan.FromMilliseconds(GRADING_QUEUE_REFRESH_INTERVAL)
        };
        _gradingQueueTimer.Tick += async (s, e) => await LoadClosedExamsAsync();
        _gradingQueueTimer.Start();
        
        Debug.WriteLine("🔄 Grading queue auto-refresh started (every 10s)");
    }

    private void StopGradingQueueRefresh()
    {
        _gradingQueueTimer?.Stop();
        _gradingQueueTimer = null;
        Debug.WriteLine("⏸️ Grading queue refresh stopped");
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
            StartGradingQueueRefresh(); // Auto-refresh every 10 seconds
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
    private async void BroadcastMessage_Click(object sender, RoutedEventArgs e)
    {
        try
        {
            var firestoreService = App.FirestoreService;
            if (firestoreService == null)
            {
                ShowError("Firestore service not initialized");
                return;
            }

            var sessions = await firestoreService.GetRunningSessionsAsync();
            
            if (!sessions.Any())
            {
                MessageBox.Show("No active exam sessions to broadcast to.", "Broadcast Message", 
                    MessageBoxButton.OK, MessageBoxImage.Information);
                return;
            }

            var dialog = new Views.BroadcastMessageDialog(sessions, _signalRService)
            {
                Owner = Window.GetWindow(this)
            };
            
            dialog.ShowDialog();
        }
        catch (Exception ex)
        {
            ShowError($"Failed to open broadcast dialog: {ex.Message}");
        }
    }

    private async void PauseAllExams_Click(object sender, RoutedEventArgs e)
    {
        try
        {
            var firestoreService = App.FirestoreService;
            if (firestoreService == null)
            {
                ShowError("Firestore service not initialized");
                return;
            }

            var sessions = await firestoreService.GetRunningSessionsAsync();
            
            if (!sessions.Any())
            {
                MessageBox.Show("No active exam sessions to pause.", "Pause Exams", 
                    MessageBoxButton.OK, MessageBoxImage.Information);
                return;
            }

            var result = MessageBox.Show(
                $"Pause all {sessions.Count} active exam session(s)?\n\n" +
                "Students will receive a notification and their timers will be frozen.",
                "Confirm Pause All", 
                MessageBoxButton.YesNo, 
                MessageBoxImage.Question);
            
            if (result != MessageBoxResult.Yes) return;

            int successCount = 0;
            
            foreach (var session in sessions)
            {
                try
                {
                    // Update session status
                    session.Status = "Paused";
                    await firestoreService.UpdateSessionAsync(session);

                    // Log event
                    await firestoreService.LogEventAsync(new SessionEvent
                    {
                        SessionId = session.Id,
                        ExamId = session.ExamId,
                        EventType = "teacher_pause_exam",
                        Severity = "Info",
                        Details = "Exam paused by instructor"
                    });

                    // Send SignalR notification
                    if (_signalRService != null && _signalRService.IsConnected)
                    {
                        await _signalRService.BroadcastMessageAsync(session.Id, "pause_exam");
                    }

                    successCount++;
                }
                catch (Exception ex)
                {
                    Debug.WriteLine($"Failed to pause session {session.Id}: {ex.Message}");
                }
            }

            await LoadLiveRosterData();
            MessageBox.Show(
                $"Paused {successCount} of {sessions.Count} session(s) successfully!", 
                "Pause Complete", 
                MessageBoxButton.OK, 
                MessageBoxImage.Information);
        }
        catch (Exception ex)
        {
            ShowError($"Failed to pause exams: {ex.Message}");
        }
    }

    private async void ExportLiveReport_Click(object sender, RoutedEventArgs e)
    {
        try
        {
            if (!_rosterItems.Any() || _rosterItems.First().StudentId == "none")
            {
                MessageBox.Show("No active sessions to export.", "Export Live Report", 
                    MessageBoxButton.OK, MessageBoxImage.Information);
                return;
            }

            var firestoreService = App.FirestoreService;
            if (firestoreService == null)
            {
                ShowError("Firestore service not initialized");
                return;
            }

            // Get session data
            var sessions = await firestoreService.GetRunningSessionsAsync();
            if (!sessions.Any())
            {
                MessageBox.Show("No active sessions found.", "Export Live Report", 
                    MessageBoxButton.OK, MessageBoxImage.Information);
                return;
            }

            // Prepare data for export
            var exportData = new List<Dictionary<string, object>>();
            
            foreach (var session in sessions)
            {
                foreach (var participant in session.Participants.Values)
                {
                    exportData.Add(new Dictionary<string, object>
                    {
                        ["Student ID"] = participant.StudentId,
                        ["Student Name"] = participant.StudentName,
                        ["Connection Status"] = participant.ConnectionStatus ?? "Unknown",
                        ["Progress"] = $"{participant.ProgressPercent}%",
                        ["Time Remaining"] = FormatTimeRemaining(TimeSpan.FromSeconds(participant.TimeRemaining)),
                        ["Flags"] = participant.FlagCount,
                        ["Last Heartbeat"] = participant.LastHeartbeat.ToString("yyyy-MM-dd HH:mm:ss"),
                        ["Session ID"] = session.Id,
                        ["Exam ID"] = session.ExamId
                    });
                }
            }

            // Export using CSV
            var timestamp = DateTime.Now.ToString("yyyyMMdd_HHmmss");
            var filename = $"LiveSessionReport_{timestamp}";
            
            var csv = new List<string>
            {
                "Student ID,Student Name,Connection Status,Progress,Time Remaining,Flags,Last Heartbeat,Session ID,Exam ID"
            };

            foreach (var item in exportData)
            {
                csv.Add($"\"{item["Student ID"]}\",\"{item["Student Name"]}\",{item["Connection Status"]},{item["Progress"]},{item["Time Remaining"]},{item["Flags"]},{item["Last Heartbeat"]},{item["Session ID"]},{item["Exam ID"]}");
            }

            var documentsPath = Environment.GetFolderPath(Environment.SpecialFolder.MyDocuments);
            var exportPath = Path.Combine(documentsPath, "ExamForge_Exports");
            Directory.CreateDirectory(exportPath);
            var path = Path.Combine(exportPath, $"{filename}.csv");
            
            await File.WriteAllLinesAsync(path, csv);
            
            MessageBox.Show($"Live report exported successfully!\n\nSaved to: {path}", 
                "Export Complete", MessageBoxButton.OK, MessageBoxImage.Information);
        }
        catch (Exception ex)
        {
            ShowError($"Failed to export live report: {ex.Message}");
        }
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

                OpenExamTemplateInBuilder(exam);
            }
            catch (Exception ex)
            {
                ShowError($"Failed to reuse exam: {ex.Message}");
            }
        }
    }

    private async void RepublishExam_Click(object sender, RoutedEventArgs e)
    {
        if (sender is not Button btn || btn.Tag is not string examId)
            return;

        try
        {
            var firestoreService = App.FirestoreService;
            if (firestoreService == null)
            {
                ShowError("Firestore service not initialized");
                return;
            }

            var publishingService = App.PublishingService;
            if (publishingService == null)
            {
                ShowError("Publishing service not initialized");
                return;
            }

            var exam = await firestoreService.GetPublishedExamAsync(examId);
            if (exam == null)
            {
                ShowError("Exam not found");
                return;
            }

            await publishingService.RepublishExamAsync(exam);
            await LoadPublishedExamsAsync();
            await LoadExamBankAsync();

            MessageBox.Show("Exam republished successfully.", "Republish",
                MessageBoxButton.OK, MessageBoxImage.Information);
        }
        catch (Exception ex)
        {
            ShowError($"Failed to republish exam: {ex.Message}");
        }
    }

    private async void DeleteExam_Click(object sender, RoutedEventArgs e)
    {
        if (sender is not Button btn || btn.Tag is not string examId)
            return;

        try
        {
            var firestoreService = App.FirestoreService;
            if (firestoreService == null)
            {
                ShowError("Firestore service not initialized");
                return;
            }

            var exam = await firestoreService.GetPublishedExamAsync(examId);
            var title = exam?.Title ?? examId;

            var result = MessageBox.Show(
                $"Delete exam '{title}'?\n\nThis will permanently remove it from Exam Bank.",
                "Delete Exam",
                MessageBoxButton.YesNo,
                MessageBoxImage.Warning);

            if (result != MessageBoxResult.Yes) return;

            await firestoreService.DeletePublishedExamAsync(examId);
            await LoadPublishedExamsAsync();
            await LoadExamBankAsync();

            MessageBox.Show("Exam deleted.", "Delete Exam", MessageBoxButton.OK, MessageBoxImage.Information);
        }
        catch (Exception ex)
        {
            ShowError($"Failed to delete exam: {ex.Message}");
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

                if (exam == null)
                {
                    ShowError("Exam not found");
                    return;
                }

                var examUrl = ResolveExamUrl(exam.Id, exam.ExamUrl);
                if (string.IsNullOrWhiteSpace(examUrl))
                {
                    ShowError("Exam URL not found");
                    return;
                }

                // Copy to clipboard
                Clipboard.SetText(examUrl);
                MessageBox.Show("Exam URL copied to clipboard!", "Success",
                    MessageBoxButton.OK, MessageBoxImage.Information);
            }
            catch (Exception ex)
            {
                ShowError($"Failed to copy exam link: {ex.Message}");
            }
        }
    }

    private async void BulkFinalize_Click(object sender, RoutedEventArgs e)
    {
        try
        {
            var gradedItems = _gradingQueueItems.Where(g => g.Status == "Graded").ToList();
            
            if (!gradedItems.Any())
            {
                MessageBox.Show("No graded items to finalize.", "Bulk Finalize", 
                    MessageBoxButton.OK, MessageBoxImage.Information);
                return;
            }

            var result = MessageBox.Show(
                $"Finalize {gradedItems.Count} graded item(s)?\n\n" +
                "This will mark all graded essays as finalized and update student scores.",
                "Bulk Finalize Grades", 
                MessageBoxButton.YesNo, 
                MessageBoxImage.Question);
            
            if (result != MessageBoxResult.Yes) return;

            var firestoreService = App.FirestoreService;
            if (firestoreService == null)
            {
                ShowError("Firestore service not initialized");
                return;
            }

            int successCount = 0;
            foreach (var item in gradedItems)
            {
                try
                {
                    // Update submission with finalized scores
                    var submissions = await firestoreService.GetExamSubmissionsAsync(item.ExamId);
                    var submission = submissions.FirstOrDefault(s => s.Id == item.SubmissionId);
                    
                    if (submission != null)
                    {
                        // Update the response with the graded points
                        var response = submission.Responses.FirstOrDefault(r => r.QuestionNumber == item.QuestionNumber);
                        if (response != null && item.PointsAwarded.HasValue)
                        {
                            response.PointsEarned = item.PointsAwarded.Value;
                            
                            // Recalculate total score
                            submission.TotalScore = submission.Responses.Sum(r => r.PointsEarned);
                            
                            // Update submission in Firestore
                            await firestoreService.SaveExamineeSubmissionAsync(submission);
                        }
                    }
                    
                    successCount++;
                }
                catch (Exception ex)
                {
                    Debug.WriteLine($"Error finalizing item {item.Id}: {ex.Message}");
                }
            }

            await LoadClosedExamsAsync();
            MessageBox.Show(
                $"Successfully finalized {successCount} of {gradedItems.Count} grades.\n\n" +
                "Student scores have been updated.",
                "Bulk Finalize Complete", 
                MessageBoxButton.OK, 
                MessageBoxImage.Information);
        }
        catch (Exception ex)
        {
            ShowError($"Failed to finalize grades: {ex.Message}");
        }
    }

    private async void ReleaseResults_Click(object sender, RoutedEventArgs e)
    {
        try
        {
            // Get selected exam from grading queue
            var pendingGrid = GradingQueueGrid ?? this.FindName("GradingQueueGrid") as DataGrid;
            var gradedGrid = this.FindName("GradedEssayGrid") as DataGrid;

            var selectedItem = (pendingGrid?.SelectedItem as GradingQueueItemViewModel)
                ?? (gradedGrid?.SelectedItem as GradingQueueItemViewModel);

            if (selectedItem == null)
            {
                MessageBox.Show("Please select an exam submission to release results.", 
                    "Release Results", MessageBoxButton.OK, MessageBoxImage.Information);
                return;
            }

            var result = MessageBox.Show(
                $"Release results for exam: {selectedItem.ExamTitle}?\n\n" +
                "Students will be able to view their grades and feedback.",
                "Release Results", 
                MessageBoxButton.YesNo, 
                MessageBoxImage.Question);
            
            if (result != MessageBoxResult.Yes) return;

            var firestoreService = App.FirestoreService;
            if (firestoreService == null)
            {
                ShowError("Firestore service not initialized");
                return;
            }

            // Update all submissions for this exam to "Released" status
            var submissions = await firestoreService.GetExamSubmissionsAsync(selectedItem.ExamId);
            int releasedCount = 0;

            foreach (var submission in submissions)
            {
                submission.Status = "Released";
                await firestoreService.SaveExamineeSubmissionAsync(submission);
                releasedCount++;
            }

            MessageBox.Show(
                $"Results released for {releasedCount} student(s).\n\n" +
                "Students can now view their grades.",
                "Release Complete", 
                MessageBoxButton.OK, 
                MessageBoxImage.Information);
        }
        catch (Exception ex)
        {
            ShowError($"Failed to release results: {ex.Message}");
        }
    }

    private async void ReopenMakeup_Click(object sender, RoutedEventArgs e)
    {
        try
        {
            var pendingGrid = GradingQueueGrid ?? this.FindName("GradingQueueGrid") as DataGrid;
            var gradedGrid = this.FindName("GradedEssayGrid") as DataGrid;

            var selectedItem = (pendingGrid?.SelectedItem as GradingQueueItemViewModel)
                ?? (gradedGrid?.SelectedItem as GradingQueueItemViewModel);

            if (selectedItem == null)
            {
                MessageBox.Show("Please select an exam to reopen for makeup.", 
                    "Reopen for Makeup", MessageBoxButton.OK, MessageBoxImage.Information);
                return;
            }

            var result = MessageBox.Show(
                $"Reopen exam '{selectedItem.ExamTitle}' for makeup?\n\n" +
                "This will:\n" +
                "• Extend the exam deadline by 7 days\n" +
                "• Allow students to retake the exam\n" +
                "• Keep existing submissions",
                "Confirm Reopen", 
                MessageBoxButton.YesNo, 
                MessageBoxImage.Question);
            
            if (result != MessageBoxResult.Yes) return;

            var firestoreService = App.FirestoreService;
            if (firestoreService == null)
            {
                ShowError("Firestore service not initialized");
                return;
            }

            // Get and update the exam
            var exam = await firestoreService.GetPublishedExamAsync(selectedItem.ExamId);
            if (exam != null)
            {
                exam.EndTime = DateTime.UtcNow.AddDays(7);
                exam.Status = "Makeup";
                await firestoreService.SavePublishedExamAsync(exam);

                await LoadClosedExamsAsync();
                MessageBox.Show(
                    "Exam reopened for makeup successfully!\n\n" +
                    $"New deadline: {exam.EndTime:MMM dd, yyyy h:mm tt}",
                    "Reopen Complete", 
                    MessageBoxButton.OK, 
                    MessageBoxImage.Information);
            }
        }
        catch (Exception ex)
        {
            ShowError($"Failed to reopen exam: {ex.Message}");
        }
    }

    private async void ExportGradebook_Click(object sender, RoutedEventArgs e)
    {
        try
        {
            var pendingGrid = GradingQueueGrid ?? this.FindName("GradingQueueGrid") as DataGrid;
            var gradedGrid = this.FindName("GradedEssayGrid") as DataGrid;

            var vm = (pendingGrid?.SelectedItem as GradingQueueItemViewModel)
                ?? (gradedGrid?.SelectedItem as GradingQueueItemViewModel);

            if (vm == null)
            {
                ShowError("Select a submission row first to export the gradebook for that exam.");
                return;
            }

            var firestoreService = App.FirestoreService;
            if (firestoreService == null)
            {
                ShowError("Firestore service not initialized");
                return;
            }

            var submissions = await firestoreService.GetExamSubmissionsAsync(vm.ExamId);
            
            var exportService = new ExportService();
            var pdfService = new PdfExportService();
            
            var excelPath = await exportService.ExportGradesToExcelAsync(vm.ExamId, vm.ExamTitle);
            var pdfPath = await pdfService.ExportGradebookToPdfAsync(vm.ExamId, vm.ExamTitle, submissions);
            
            MessageBox.Show(
                $"Gradebook exported successfully!\n\n" +
                $"Excel: {excelPath}\n" +
                $"PDF: {pdfPath}",
                "Export Complete", MessageBoxButton.OK, MessageBoxImage.Information);
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
                    var examUrl = ResolveExamUrl(exam.Id, exam.ExamUrl);
                    if (string.IsNullOrWhiteSpace(examUrl))
                    {
                        ShowError("Exam URL not found.");
                        return;
                    }

                    Process.Start(new ProcessStartInfo
                    {
                        FileName = examUrl,
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

    private async void EditSchedule_Click(object sender, RoutedEventArgs e)
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

                var exam = await firestoreService.GetPublishedExamAsync(examId);
                if (exam == null)
                {
                    ShowError("Exam not found");
                    return;
                }

                var dialog = new Views.EditScheduleDialog(exam)
                {
                    Owner = Window.GetWindow(this)
                };

                dialog.ShowDialog();

                if (dialog.ChangesSaved)
                {
                    await LoadPublishedExamsAsync();
                }
            }
            catch (Exception ex)
            {
                ShowError($"Failed to edit schedule: {ex.Message}");
            }
        }
    }

    private void CopyUrl_Click(object sender, RoutedEventArgs e)
    {
        if (sender is Button btn && btn.Tag is string examTag)
        {
            var exam = _allExams.FirstOrDefault(e => e.Id == examTag);
            var url = exam != null ? ResolveExamUrl(exam.Id, exam.ExamUrl) : examTag;

            if (!string.IsNullOrWhiteSpace(url))
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
    }

    private async void Unpublish_Click(object sender, RoutedEventArgs e)
    {
        if (sender is Button btn && btn.Tag is string examId)
        {
            try
            {
                var exam = _allExams.FirstOrDefault(e => e.Id == examId);
                if (exam == null)
                {
                    ShowError("Exam not found");
                    return;
                }

                var result = MessageBox.Show(
                    $"Are you sure you want to unpublish '{exam.Title}'?\n\n" +
                    "This will:\n" +
                    "• Remove the exam from the published list\n" +
                    "• Keep all submitted responses\n" +
                    "• Delete the public HTML file\n\n" +
                    "This action cannot be undone.",
                    "Confirm Unpublish", 
                    MessageBoxButton.YesNo, 
                    MessageBoxImage.Warning);
                
                if (result != MessageBoxResult.Yes) return;

                var firestoreService = App.FirestoreService;
                if (firestoreService == null)
                {
                    ShowError("Firestore service not initialized");
                    return;
                }

                // Update status to Unpublished
                var publishedExam = await firestoreService.GetPublishedExamAsync(examId);
                if (publishedExam != null)
                {
                    publishedExam.Status = "Unpublished";
                    publishedExam.LifecycleStatus = "Unpublished";
                    await firestoreService.SavePublishedExamAsync(publishedExam);
                }

                // Delete HTML file if it exists
                try
                {
                    var htmlFileName = System.IO.Path.GetFileName(new Uri(exam.ExamUrl).LocalPath);
                    var publicPath = System.IO.Path.Combine(AppDomain.CurrentDomain.BaseDirectory, "public", "exams", htmlFileName);
                    if (System.IO.File.Exists(publicPath))
                    {
                        System.IO.File.Delete(publicPath);
                    }
                }
                catch (Exception deleteEx)
                {
                    Debug.WriteLine($"Failed to delete HTML file: {deleteEx.Message}");
                }

                await LoadPublishedExamsAsync();
                MessageBox.Show("Exam unpublished successfully!", "Success",
                    MessageBoxButton.OK, MessageBoxImage.Information);
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