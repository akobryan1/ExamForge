using System;
using System.Threading.Tasks;
using System.Windows;
using System.Windows.Media;
using System.Linq;
using System.Collections.Generic;
using System.Windows.Controls;
using System.Windows.Threading;
using System.Net.Http;
using System.Diagnostics;
using Microsoft.Extensions.Configuration;
using ExamForge.Views;
using ExamForge.Models;

namespace ExamForge;

/// <summary>
/// Interaction logic for MainWindow.xaml
/// </summary>
public partial class MainWindow : Window
{
    private object? _currentExamBuilderView;
    private structure_builder_usercontrol? _structureBuilderInstance;
    private sidebar_usercontrol? _sidebar;
    private DispatcherTimer? _signalTimer;
    private readonly HttpClient _signalHttpClient = new() { Timeout = TimeSpan.FromSeconds(4) };
    private bool _isCheckingSignal;
    private string _signalCheckUrl = "https://examforge-signalr.onrender.com";

    public MainWindow()
    {
        InitializeComponent();
        Loaded += MainWindow_Loaded;
    }

    private void MainWindow_Loaded(object sender, RoutedEventArgs e)
    {
        InitializeSignalStrengthMonitoring();

        // Get reference to sidebar
        _sidebar = this.FindName("Sidebar") as sidebar_usercontrol;

        // Default to dashboard on launch
        if (MainContentHost.Content == null)
        {
            MainContentHost.Content = new dashboard_usercontrol();
        }
    }

    protected override void OnClosed(EventArgs e)
    {
        _signalTimer?.Stop();
        _signalTimer = null;
        _signalHttpClient.Dispose();
        base.OnClosed(e);
    }

    private void InitializeSignalStrengthMonitoring()
    {
        try
        {
            var configuration = new ConfigurationBuilder()
                .SetBasePath(AppDomain.CurrentDomain.BaseDirectory)
                .AddJsonFile("appsettings.json", optional: true, reloadOnChange: false)
                .Build();

            var configuredUrl = configuration["Firebase:SignalRHubUrl"];
            if (!string.IsNullOrWhiteSpace(configuredUrl))
            {
                _signalCheckUrl = configuredUrl.TrimEnd('/');
            }
        }
        catch
        {
            // Keep fallback URL
        }

        _signalTimer = new DispatcherTimer
        {
            Interval = TimeSpan.FromSeconds(8)
        };
        _signalTimer.Tick += async (_, _) => await UpdateSignalStrengthAsync();
        _signalTimer.Start();

        _ = UpdateSignalStrengthAsync();
    }

    private async Task UpdateSignalStrengthAsync()
    {
        if (_isCheckingSignal) return;

        _isCheckingSignal = true;
        try
        {
            var sw = Stopwatch.StartNew();
            using var response = await _signalHttpClient.GetAsync(_signalCheckUrl);
            sw.Stop();

            if (!response.IsSuccessStatusCode)
            {
                SignalStrengthText.Text = "Disconnected";
                return;
            }

            var ms = sw.ElapsedMilliseconds;
            SignalStrengthText.Text = ms switch
            {
                <= 180 => "Strong",
                <= 450 => "Medium",
                _ => "Poor"
            };
        }
        catch
        {
            SignalStrengthText.Text = "Disconnected";
        }
        finally
        {
            _isCheckingSignal = false;
        }
    }

    public void SetLoading(bool isLoading, string message = "Loading...")
    {
        LoadingOverlay.Visibility = isLoading ? Visibility.Visible : Visibility.Collapsed;
        LoadingStatusText.Text = message;
    }

    private void sidebar_usercontrol_Loaded(object sender, RoutedEventArgs e)
    {
    }

    /// <summary>
    /// Store reference to structure_builder when it's loaded
    /// </summary>
    public void SetStructureBuilder(structure_builder_usercontrol structureBuilder)
    {
        _structureBuilderInstance = structureBuilder;
    }

    /// <summary>
    /// Load an existing exam for editing
    /// </summary>
    public async Task LoadExamForEditing(string examId)
    {
        try
        {
            // For now, just show a message that the feature is coming soon
            MessageBox.Show($"Loading exam {examId} for editing...\nThis feature is under development.", "Info", 
                MessageBoxButton.OK, MessageBoxImage.Information);
        }
        catch (Exception ex)
        {
            MessageBox.Show($"Failed to load exam for editing: {ex.Message}", "Error", 
                MessageBoxButton.OK, MessageBoxImage.Error);
        }
    }

    /// <summary>
    /// Call this method when user clicks "Review" button
    /// </summary>
    public void ShowExamReview(ExamReviewData examData)
    {
        _currentExamBuilderView = MainContentHost.Content;

        var reviewControl = new ReviewExamControl();
        reviewControl.LoadExamData(examData);

        reviewControl.PublishRequested += (s, args) =>
        {
            // ✅ Redirect to Published Exams tab
            System.Diagnostics.Debug.WriteLine("[MainWindow] PublishRequested event received, redirecting to Published Exams");
            if (_sidebar != null)
            {
                // Ensure redirect happens on UI thread
                Dispatcher.Invoke(() =>
                {
                    _sidebar.NavigateToPublishedExams();
                    System.Diagnostics.Debug.WriteLine("[MainWindow] Redirect completed");
                });
            }
        };

        reviewControl.CancelRequested += (s, args) =>
        {
            ReturnToExamBuilder();
        };

        MainContentHost.Content = reviewControl;
    }

    private void ReturnToExamBuilder()
    {
        if (_currentExamBuilderView != null)
        {
            MainContentHost.Content = _currentExamBuilderView;
        }
    }

    /// <summary>
    /// Gathers all exam data from structure_builder and content_builder
    /// </summary>
    public ExamReviewData? GatherExamData()
    {
        if (_structureBuilderInstance == null)
        {
            MessageBox.Show(
                "Please configure the exam structure first.\n\n" +
                "Go to the Exam Foundry tab and set up:\n" +
                "- Exam name\n" +
                "- Test structure\n" +
                "- Timing configuration",
                "Configuration Required",
                MessageBoxButton.OK,
                MessageBoxImage.Warning);
            return null;
        }

        var examTitle = _structureBuilderInstance.GetExamTitle();
        if (string.IsNullOrWhiteSpace(examTitle))
        {
            MessageBox.Show("Please enter an exam name.", "Missing Exam Name",
                MessageBoxButton.OK, MessageBoxImage.Warning);
            return null;
        }

        var examSubject = _structureBuilderInstance.GetSubjectName();

        var structureState = _structureBuilderInstance.GetStructureState();
        if (structureState == null || structureState.Rows.Count == 0)
        {
            MessageBox.Show("No exam structure configured. Please configure the exam structure first.",
                "Missing Structure", MessageBoxButton.OK, MessageBoxImage.Warning);
            return null;
        }

        var timingState = _structureBuilderInstance.GetTimingState();
        if (timingState == null)
        {
            MessageBox.Show("No timing configured. Please configure the exam timing.",
                "Missing Timing", MessageBoxButton.OK, MessageBoxImage.Warning);
            return null;
        }

        if (timingState.StartDateTime == null || timingState.EndDateTime == null)
        {
            MessageBox.Show("Please set both start and end date/time for the exam.",
                "Incomplete Timing", MessageBoxButton.OK, MessageBoxImage.Warning);
            return null;
        }

        // Get login configuration
        var loginConfig = _structureBuilderInstance.GetLoginConfigState();

        var contentBuilder = FindVisualChild<content_builder>(this);
        var contentItems = contentBuilder?.GetAllItems() ?? new List<ExamItem>();

        if (contentItems.Count == 0)
        {
            MessageBox.Show(
                "No exam content found.\n\n" +
                "Please go to the Content Builder and add questions for your exam.",
                "Missing Content",
                MessageBoxButton.OK,
                MessageBoxImage.Warning);
            return null;
        }

        // Convert DateTime to UTC for Firestore
        DateTime startTimeUtc = ConvertToUtc(timingState.StartDateTime.Value);
        DateTime endTimeUtc = ConvertToUtc(timingState.EndDateTime.Value);

        var antiCheatState = _structureBuilderInstance?.GetAntiCheatState();
        System.Diagnostics.Debug.WriteLine($"[MainWindow] AntiCheatState is null: {antiCheatState == null}");
        if (antiCheatState != null)
        {
            System.Diagnostics.Debug.WriteLine($"[MainWindow] DetectTabbing: {antiCheatState.DetectTabbing}, WarningOnly: {antiCheatState.WarningOnly}");
        }

        return new ExamReviewData
        {
            Title = examTitle,
            Subject = examSubject,
            Structures = ConvertToExamStructures(structureState),
            Contents = ConvertToExamContents(contentItems),
            StartTime = startTimeUtc,
            EndTime = endTimeUtc,
            ExamDuration = CalculateDuration(timingState),
            LoginConfig = loginConfig,
            // Include anti-cheat configuration from the structure builder (if any)
            AntiCheat = antiCheatState
        };
    }

    /// <summary>
    /// Convert DateTime to UTC, handling Unspecified kind as local time
    /// </summary>
    private DateTime ConvertToUtc(DateTime dateTime)
    {
        switch (dateTime.Kind)
        {
            case DateTimeKind.Utc:
                return dateTime;
            case DateTimeKind.Local:
                return dateTime.ToUniversalTime();
            case DateTimeKind.Unspecified:
                return DateTime.SpecifyKind(dateTime, DateTimeKind.Local).ToUniversalTime();
            default:
                return dateTime.ToUniversalTime();
        }
    }

    private List<ExamStructure> ConvertToExamStructures(DataGridState dataGridState)
    {
        var structures = new List<ExamStructure>();

        foreach (var row in dataGridState.Rows)
        {
            if (string.IsNullOrWhiteSpace(row.Start) || string.IsNullOrWhiteSpace(row.End))
                continue;

            if (!int.TryParse(row.Start, out int start) || !int.TryParse(row.End, out int end))
                continue;

            if (!int.TryParse(row.Points, out int points))
                continue;

            structures.Add(new ExamStructure
            {
                SectionName = $"Test Group {row.TestGroup}",
                Description = row.TestType,
                Points = points * (end - start + 1),
                QuestionCount = end - start + 1,
                IsComplete = true
            });
        }

        return structures;
    }

    private List<ExamContent> ConvertToExamContents(List<ExamItem> items)
    {
        var contents = new List<ExamContent>();

        foreach (var item in items)
        {
            var content = new ExamContent
            {
                QuestionText = item.Question,
                QuestionType = MapTestTypeToQuestionType(item.TestType),
                Points = int.TryParse(item.Points, out int p) ? p : 1,
                IsComplete = item.Status == "Complete"
            };

            switch (item.TestType)
            {
                case "Multiple Choice":
                    content.Options = new List<string> { item.OptionA, item.OptionB, item.OptionC, item.OptionD }
                        .Where(o => !string.IsNullOrWhiteSpace(o))
                        .ToList();
                    content.CorrectAnswer = item.CorrectAnswer;
                    break;

                case "True or False":
                    content.Options = new List<string> { "True", "False" };
                    content.CorrectAnswer = item.TrueFalseAnswer ? "True" : "False";
                    break;

                case "Modified True or False":
                    content.Options = new List<string> { "True", "False" };
                    content.CorrectAnswer = item.ModifiedAnswer;
                    break;

                case "Identification":
                case "Essay":
                    content.CorrectAnswer = item.TextAnswer;
                    break;

                case "Enumeration":
                    content.Options = item.EnumerationAnswers;
                    content.CorrectAnswer = string.Join(", ", item.EnumerationAnswers);
                    break;
            }

            contents.Add(content);
        }

        return contents;
    }

    private string MapTestTypeToQuestionType(string testType)
    {
        return testType switch
        {
            "Multiple Choice" => "Multiple Choice",
            "True or False" => "True/False",
            "Modified True or False" => "Modified True/False",
            "Identification" => "Short Answer",
            "Enumeration" => "Short Answer",
            "Essay" => "Essay",
            _ => "Multiple Choice"
        };
    }

    private int CalculateDuration(TimingState timingState)
    {
        int hours = int.TryParse(timingState.Hours, out int h) ? h : 0;
        int minutes = int.TryParse(timingState.Minutes, out int m) ? m : 0;
        int seconds = int.TryParse(timingState.Seconds, out int s) ? s : 0;

        return (hours * 60) + minutes + (seconds / 60);
    }

    private T? FindVisualChild<T>(DependencyObject parent) where T : DependencyObject
    {
        for (int i = 0; i < VisualTreeHelper.GetChildrenCount(parent); i++)
        {
            var child = VisualTreeHelper.GetChild(parent, i);
            if (child is T typedChild)
                return typedChild;

            var childOfChild = FindVisualChild<T>(child);
            if (childOfChild != null)
                return childOfChild;
        }
        return null;
    }
}