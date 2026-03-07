using ExamForge.Models;
using ExamForge.Services;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text.Json;
using System.Threading.Tasks;
using System.Windows;
using System.Windows.Controls;

namespace ExamForge;

public partial class essay_checker_usercontrol : UserControl
{
    private readonly FirestoreService? _firestoreService;
    private readonly EssayAutoGradingService _autoGradingService = new();

    private readonly List<PublishedExam> _essayExams = new();
    private readonly List<EssayCheckerQueueItem> _queueItems = new();
    private readonly List<EssayCheckerQueueItem> _gradedItems = new();

    private EssayCheckerQueueItem? _currentItem;
    private EssayAutoGradeResult? _currentResult;
    private double _currentAdjustedScore;
    private bool _apiStatusShown;

    public essay_checker_usercontrol()
    {
        InitializeComponent();
        _firestoreService = App.FirestoreService;
    }

    private async void EssayChecker_Loaded(object sender, RoutedEventArgs e)
    {
        await LoadEssayExamsAsync();
    }

    private async Task LoadEssayExamsAsync()
    {
        if (_firestoreService == null) return;

        var allExams = await _firestoreService.GetAllPublishedExamsAsync();
        _essayExams.Clear();
        _essayExams.AddRange(allExams.Where(e => e.Contents.Any(c => string.Equals(c.QuestionType, "Essay", StringComparison.OrdinalIgnoreCase))));

        EssayExamFilter.Items.Clear();
        EssayExamFilter.Items.Add(new ComboBoxItem { Content = "All Essay Exams", Tag = "ALL" });
        foreach (var exam in _essayExams)
        {
            EssayExamFilter.Items.Add(new ComboBoxItem { Content = exam.Title, Tag = exam.Id });
        }

        EssayExamFilter.SelectedIndex = 0;
        await RefreshQueueAsync();
    }

    private async void EssayExamFilter_SelectionChanged(object sender, SelectionChangedEventArgs e)
    {
        if (!IsLoaded) return;
        await RefreshQueueAsync();
    }

    private async Task RefreshQueueAsync()
    {
        if (_firestoreService == null) return;

        _queueItems.Clear();
        _gradedItems.Clear();
        var selectedExamId = (EssayExamFilter.SelectedItem as ComboBoxItem)?.Tag?.ToString() ?? "ALL";
        var targetExams = selectedExamId == "ALL"
            ? _essayExams
            : _essayExams.Where(e => e.Id == selectedExamId).ToList();

        foreach (var exam in targetExams)
        {
            var submissions = await _firestoreService.GetExamSubmissionsAsync(exam.Id);
            var essayContents = exam.Contents
                .Where(c => string.Equals(c.QuestionType, "Essay", StringComparison.OrdinalIgnoreCase))
                .Select((content, idx) => new { Content = content, Number = idx + 1 })
                .ToList();

            foreach (var submission in submissions)
            {
                foreach (var essay in essayContents)
                {
                    var response = submission.Responses.FirstOrDefault(r =>
                        string.Equals(r.QuestionId, essay.Content.ContentId, StringComparison.OrdinalIgnoreCase)
                        || r.QuestionNumber == essay.Number);

                    if (response == null || string.IsNullOrWhiteSpace(response.Answer))
                        continue;

                    var meta = ParseEssayMetadata(essay.Content.Explanation);
                    var resolvedQuestionId = ResolveQuestionId(essay.Content.ContentId, response.QuestionId, response.QuestionNumber, essay.Number);
                    var existingGrade = TryGetExistingGrade(submission, resolvedQuestionId, response.QuestionId, response.QuestionNumber, essay.Number);

                    var queueItem = new EssayCheckerQueueItem
                    {
                        SubmissionId = submission.Id,
                        ExamId = exam.Id,
                        ExamTitle = exam.Title,
                        Subject = exam.Subject,
                        StudentName = submission.StudentName,
                        StudentId = submission.StudentId,
                        QuestionId = resolvedQuestionId,
                        QuestionNumber = response.QuestionNumber,
                        EssayQuestion = essay.Content.Question,
                        EssayAnswer = response.Answer,
                        RubricText = meta.Rubric,
                        ModelAnswer = meta.ModelAnswer,
                        KeyPoints = meta.KeyPoints,
                        MaxPoints = essay.Content.Points > 0 ? essay.Content.Points : 1,
                        ThesisWeight = meta.ThesisWeight,
                        EvidenceWeight = meta.EvidenceWeight,
                        ClarityWeight = meta.ClarityWeight,
                        ExistingGrade = existingGrade
                    };

                    if (existingGrade != null)
                    {
                        _gradedItems.Add(queueItem);
                    }
                    else
                    {
                        _queueItems.Add(queueItem);
                    }
                }
            }
        }

        EssayQueueList.ItemsSource = null;
        EssayQueueList.ItemsSource = _queueItems
            .OrderBy(q => q.ExamTitle)
            .ThenBy(q => q.StudentName)
            .ToList();

        GradedArchiveList.ItemsSource = null;
        GradedArchiveList.ItemsSource = _gradedItems
            .OrderByDescending(g => g.ExistingGrade?.GradedAt ?? DateTime.MinValue)
            .ToList();

        QueueCountText.Text = $"({_queueItems.Count} pending, {_gradedItems.Count} graded)";

        if (EssayQueueList.Items.Count > 0)
        {
            EssayQueueList.SelectedIndex = 0;
        }
    }

    private async void EssayQueueList_SelectionChanged(object sender, SelectionChangedEventArgs e)
    {
        if (EssayQueueList.SelectedItem is not EssayCheckerQueueItem item) return;
        GradedArchiveList.SelectedItem = null;

        _currentItem = item;
        ExamMetaText.Text = $"Student: {item.StudentName} ({item.StudentId}) | Exam: {item.ExamTitle} | Subject: {item.Subject}";
        QuestionTextBlock.Text = item.EssayQuestion;
        RubricTextBlock.Text = string.IsNullOrWhiteSpace(item.RubricText) ? "No rubric specified." : item.RubricText;
        ModelAnswerTextBlock.Text = string.IsNullOrWhiteSpace(item.ModelAnswer)
            ? item.KeyPoints
            : $"Model Answer: {item.ModelAnswer}\n\nKey Points: {item.KeyPoints}";
        StudentEssayText.Text = item.EssayAnswer;

        _currentResult = await _autoGradingService.GradeEssayAsync(item);
        _currentAdjustedScore = _currentResult.AiScore;
        RenderCurrentResult();

        if (!_apiStatusShown)
        {
            _apiStatusShown = true;
            var mode = _currentResult.UsedDeepSeek ? "DeepSeek API" : "Heuristic Fallback";
            MessageBox.Show($"Grading mode: {mode}\nStatus: {_currentResult.ProviderStatus}",
                "Essay Grading Provider Status", MessageBoxButton.OK, MessageBoxImage.Information);
            System.Diagnostics.Debug.WriteLine($"Essay grading provider: {mode}. {_currentResult.ProviderStatus}");
        }
    }

    private void GradedArchiveList_SelectionChanged(object sender, SelectionChangedEventArgs e)
    {
        if (GradedArchiveList.SelectedItem is not EssayCheckerQueueItem item || item.ExistingGrade == null) return;
        EssayQueueList.SelectedItem = null;

        _currentItem = item;
        ExamMetaText.Text = $"Student: {item.StudentName} ({item.StudentId}) | Exam: {item.ExamTitle} | Subject: {item.Subject}";
        QuestionTextBlock.Text = item.EssayQuestion;
        RubricTextBlock.Text = string.IsNullOrWhiteSpace(item.RubricText) ? "No rubric specified." : item.RubricText;
        ModelAnswerTextBlock.Text = string.IsNullOrWhiteSpace(item.ModelAnswer)
            ? item.KeyPoints
            : $"Model Answer: {item.ModelAnswer}\n\nKey Points: {item.KeyPoints}";
        StudentEssayText.Text = item.EssayAnswer;

        _currentResult = new EssayAutoGradeResult
        {
            AiScore = item.ExistingGrade.AiScore,
            MaxScore = item.ExistingGrade.MaxScore,
            ConfidencePercent = item.ExistingGrade.ConfidencePercent,
            FlagForReview = item.ExistingGrade.FlagForReview,
            Justification = item.ExistingGrade.Justification,
            RubricBreakdown = item.ExistingGrade.RubricBreakdown,
            ProviderStatus = $"Loaded from archive ({item.ExistingGrade.GradedAt:g})",
            UsedDeepSeek = true
        };
        _currentAdjustedScore = item.ExistingGrade.FinalScore;
        AdjustmentReasonTextBox.Text = item.ExistingGrade.InstructorAdjustmentNote;
        RenderCurrentResult();
    }

    private void RenderCurrentResult()
    {
        if (_currentItem == null || _currentResult == null) return;

        AiScoreText.Text = $"{_currentAdjustedScore:F1}/{_currentResult.MaxScore:F1}";
        ConfidenceText.Text = $"Confidence: {_currentResult.ConfidencePercent:F1}%";
        FlagReviewText.Text = _currentResult.FlagForReview ? "Flagged for Review" : "";

        RubricBreakdownGrid.ItemsSource = null;
        RubricBreakdownGrid.ItemsSource = _currentResult.RubricBreakdown;

        var adjustmentReason = AdjustmentReasonTextBox.Text?.Trim();
        var justification = _currentResult.Justification;
        if (Math.Abs(_currentAdjustedScore - _currentResult.AiScore) > 0.01)
        {
            var reason = string.IsNullOrWhiteSpace(adjustmentReason) ? "Instructor adjustment" : adjustmentReason;
            justification += $"\nInstructor adjusted score from {_currentResult.AiScore:F1} to {_currentAdjustedScore:F1} due to: {reason}.";
        }

        JustificationText.Text = justification;
    }

    private async void AutoGradeAll_Click(object sender, RoutedEventArgs e)
    {
        if (!_queueItems.Any())
        {
            MessageBox.Show("No pending essay submissions were found.", "Essay Checker", MessageBoxButton.OK, MessageBoxImage.Information);
            return;
        }

        if (_firestoreService == null)
        {
            MessageBox.Show("Firestore service is not available.", "Essay Checker", MessageBoxButton.OK, MessageBoxImage.Warning);
            return;
        }

        var processed = 0;
        var skipped = 0;
        var deepSeekCount = 0;
        var fallbackCount = 0;

        foreach (var item in _queueItems)
        {
            if (string.IsNullOrWhiteSpace(item.SubmissionId))
            {
                skipped++;
                continue;
            }

            try
            {
                var result = await _autoGradingService.GradeEssayAsync(item);
                if (result.UsedDeepSeek) deepSeekCount++; else fallbackCount++;
                var record = new EssayGradeRecord
                {
                    QuestionId = ResolveQuestionId(item.QuestionId, string.Empty, item.QuestionNumber, item.QuestionNumber),
                    QuestionNumber = item.QuestionNumber,
                    StudentAnswer = item.EssayAnswer,
                    AiScore = result.AiScore,
                    FinalScore = result.AiScore,
                    MaxScore = result.MaxScore,
                    ConfidencePercent = result.ConfidencePercent,
                    FlagForReview = result.FlagForReview,
                    Justification = result.Justification,
                    RubricBreakdown = result.RubricBreakdown,
                    GradedAt = DateTime.UtcNow
                };

                await _firestoreService.UpdateSubmissionEssayGradeAsync(item.SubmissionId, record);
                processed++;
            }
            catch (Exception ex)
            {
                skipped++;
                System.Diagnostics.Debug.WriteLine($"Essay auto-grade skipped for submission {item.SubmissionId}: {ex.Message}");
            }
        }

        MessageBox.Show($"Auto-grading finished. Processed: {processed}, Skipped: {skipped}.\nDeepSeek: {deepSeekCount}, Fallback: {fallbackCount}.",
            "Essay Checker", MessageBoxButton.OK, MessageBoxImage.Information);
        System.Diagnostics.Debug.WriteLine($"Auto-grade provider usage => DeepSeek: {deepSeekCount}, Fallback: {fallbackCount}");
        await RefreshQueueAsync();
    }

    private void DecreaseScore_Click(object sender, RoutedEventArgs e)
    {
        if (_currentResult == null) return;
        _currentAdjustedScore = Math.Max(0, _currentAdjustedScore - 1);
        RenderCurrentResult();
    }

    private void IncreaseScore_Click(object sender, RoutedEventArgs e)
    {
        if (_currentResult == null) return;
        _currentAdjustedScore = Math.Min(_currentResult.MaxScore, _currentAdjustedScore + 1);
        RenderCurrentResult();
    }

    private async void SaveCurrentGrade_Click(object sender, RoutedEventArgs e)
    {
        if (_currentItem == null || _currentResult == null || _firestoreService == null)
        {
            MessageBox.Show("Select an essay first.", "Essay Checker", MessageBoxButton.OK, MessageBoxImage.Information);
            return;
        }

        var record = new EssayGradeRecord
        {
            QuestionId = ResolveQuestionId(_currentItem.QuestionId, string.Empty, _currentItem.QuestionNumber, _currentItem.QuestionNumber),
            QuestionNumber = _currentItem.QuestionNumber,
            StudentAnswer = _currentItem.EssayAnswer,
            AiScore = _currentResult.AiScore,
            FinalScore = _currentAdjustedScore,
            MaxScore = _currentResult.MaxScore,
            ConfidencePercent = _currentResult.ConfidencePercent,
            FlagForReview = _currentResult.FlagForReview,
            Justification = JustificationText.Text,
            InstructorAdjustmentNote = AdjustmentReasonTextBox.Text?.Trim() ?? string.Empty,
            RubricBreakdown = _currentResult.RubricBreakdown,
            GradedAt = DateTime.UtcNow
        };

        await _firestoreService.UpdateSubmissionEssayGradeAsync(_currentItem.SubmissionId, record);
        MessageBox.Show("Essay grade saved.", "Essay Checker", MessageBoxButton.OK, MessageBoxImage.Information);
        await RefreshQueueAsync();
    }

    private static EssayGradeRecord? TryGetExistingGrade(ExamSubmission submission, string resolvedQuestionId, string? responseQuestionId, int responseNumber, int fallbackNumber)
    {
        if (submission.EssayGrades == null || submission.EssayGrades.Count == 0) return null;

        if (submission.EssayGrades.TryGetValue(resolvedQuestionId, out var record) && record != null)
            return record;

        if (!string.IsNullOrWhiteSpace(responseQuestionId) && submission.EssayGrades.TryGetValue(responseQuestionId, out record) && record != null)
            return record;

        var numberKey = $"essay_q_{Math.Max(1, responseNumber > 0 ? responseNumber : fallbackNumber)}";
        return submission.EssayGrades.TryGetValue(numberKey, out record) ? record : null;
    }

    private sealed class EssayMetadata
    {
        public string Rubric { get; set; } = "";
        public string ModelAnswer { get; set; } = "";
        public string KeyPoints { get; set; } = "";
        public double ThesisWeight { get; set; } = 33;
        public double EvidenceWeight { get; set; } = 34;
        public double ClarityWeight { get; set; } = 33;
    }

    private static EssayMetadata ParseEssayMetadata(string? rawExplanation)
    {
        if (string.IsNullOrWhiteSpace(rawExplanation))
            return new EssayMetadata();

        try
        {
            var parsed = JsonSerializer.Deserialize<EssayMetadata>(rawExplanation, new JsonSerializerOptions
            {
                PropertyNameCaseInsensitive = true
            });
            return parsed ?? new EssayMetadata();
        }
        catch
        {
            return new EssayMetadata { Rubric = rawExplanation };
        }
    }

    private static string ResolveQuestionId(string? primary, string? secondary, int responseNumber, int fallbackNumber)
    {
        if (!string.IsNullOrWhiteSpace(primary)) return primary.Trim();
        if (!string.IsNullOrWhiteSpace(secondary)) return secondary.Trim();

        var number = responseNumber > 0 ? responseNumber : fallbackNumber;
        return $"essay_q_{Math.Max(1, number)}";
    }
}
