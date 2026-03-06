using System;
using System.Collections.Generic;
using System.Linq;
using System.Windows;
using System.Windows.Media;
using ExamForge.Models;

namespace ExamForge.Views;

public partial class ViewSubmissionDialog : Window
{
    private ExamSubmission _submission;
    private PublishedExam _exam;

    public ViewSubmissionDialog(ExamSubmission submission, PublishedExam exam)
    {
        InitializeComponent();
        _submission = submission;
        _exam = exam;
        LoadSubmissionData();
    }

    private void LoadSubmissionData()
    {
        // Header information
        StudentNameText.Text = $"{_submission.StudentName} ({_submission.StudentId})";
        ExamInfoText.Text = $"{_exam.Title} • Submitted {_submission.SubmittedAt:MMM dd, yyyy h:mm tt}";
        
        // Score
        var percentage = _submission.TotalPossiblePoints > 0 
            ? (_submission.TotalScore / _submission.TotalPossiblePoints) * 100 
            : 0;
        ScoreText.Text = $"{_submission.TotalScore:F1}/{_submission.TotalPossiblePoints:F1} ({percentage:F0}%)";
        
        // Time taken
        if (_submission.StartTime.HasValue && _submission.EndTime.HasValue)
        {
            var duration = _submission.EndTime.Value - _submission.StartTime.Value;
            TimeText.Text = $"{duration.TotalMinutes:F0} minutes";
        }
        else
        {
            TimeText.Text = "N/A";
        }
        
        // Status
        StatusText.Text = _submission.Status ?? "Submitted";
        StatusText.Foreground = percentage >= 60 
            ? (Brush)FindResource("SuccessTextBrush") 
            : (Brush)FindResource("ErrorTextBrush");

        // Build question list
        var questions = new List<QuestionAnswerViewModel>();
        
        for (int i = 0; i < _exam.Contents.Count; i++)
        {
            var content = _exam.Contents[i];
            var questionNumber = i + 1;
            var response = _submission.Responses.FirstOrDefault(r =>
                r.QuestionId == content.ContentId ||
                r.QuestionNumber == questionNumber ||
                string.Equals(r.QuestionId, $"question{questionNumber}", StringComparison.OrdinalIgnoreCase));
            
            var vm = new QuestionAnswerViewModel
            {
                QuestionNumber = content.ContentId,
                QuestionNumberText = $"Q{questionNumber}",
                QuestionType = content.QuestionType,
                QuestionText = content.Question,
                StudentAnswer = response?.Answer ?? "(No answer provided)",
                CorrectAnswer = content.Answer,
                PointsEarned = response?.PointsEarned ?? 0,
                PointsPossible = content.Points,
                IsCorrect = response?.IsCorrect ?? false
            };

            vm.CorrectnessText = vm.IsCorrect ? "? Correct" : "? Incorrect";
            vm.CorrectnessBackground = vm.IsCorrect 
                ? (Brush)FindResource("SuccessBackgroundBrush") 
                : (Brush)FindResource("ErrorBackgroundBrush");
            vm.CorrectnessForeground = vm.IsCorrect 
                ? (Brush)FindResource("SuccessTextBrush") 
                : (Brush)FindResource("ErrorTextBrush");
            vm.ShowCorrectAnswer = !vm.IsCorrect;
            vm.PointsText = $"{vm.PointsEarned:F1}/{vm.PointsPossible} pts";

            questions.Add(vm);
        }

        QuestionsContainer.ItemsSource = questions;
    }

    private async void ExportReport_Click(object sender, RoutedEventArgs e)
    {
        try
        {
            var exportService = new Services.ExportService();
            var pdfService = new Services.PdfExportService();
            
            // Export both CSV and PDF
            var csvPath = await exportService.ExportStudentReportAsync(
                _submission.StudentName,
                _exam.Title,
                _submission,
                _exam);

            var pdfPath = await pdfService.ExportStudentReportToPdfAsync(
                _submission.StudentName,
                _exam.Title,
                _submission,
                _exam);

            MessageBox.Show(
                $"Student report exported successfully!\n\n" +
                $"CSV: {csvPath}\n" +
                $"PDF: {pdfPath}",
                "Export Complete", MessageBoxButton.OK, MessageBoxImage.Information);
        }
        catch (Exception ex)
        {
            MessageBox.Show($"Failed to export report: {ex.Message}", "Export Error",
                MessageBoxButton.OK, MessageBoxImage.Error);
        }
    }

    private async void ExportPdf_Click(object sender, RoutedEventArgs e)
    {
        try
        {
            var pdfService = new Services.PdfExportService();
            var path = await pdfService.ExportStudentReportToPdfAsync(
                _submission.StudentName,
                _exam.Title,
                _submission,
                _exam);

            MessageBox.Show($"PDF report exported successfully!\n\nSaved to: {path}",
                "Export Complete", MessageBoxButton.OK, MessageBoxImage.Information);
        }
        catch (Exception ex)
        {
            MessageBox.Show($"Failed to export PDF: {ex.Message}", "Export Error",
                MessageBoxButton.OK, MessageBoxImage.Error);
        }
    }

    private void Close_Click(object sender, RoutedEventArgs e)
    {
        Close();
    }
}

public class QuestionAnswerViewModel
{
    public string QuestionNumber { get; set; } = "";
    public string QuestionNumberText { get; set; } = "";
    public string QuestionType { get; set; } = "";
    public string QuestionText { get; set; } = "";
    public string StudentAnswer { get; set; } = "";
    public string CorrectAnswer { get; set; } = "";
    public double PointsEarned { get; set; }
    public double PointsPossible { get; set; }
    public bool IsCorrect { get; set; }
    public string CorrectnessText { get; set; } = "";
    public Brush CorrectnessBackground { get; set; } = Brushes.Gray;
    public Brush CorrectnessForeground { get; set; } = Brushes.White;
    public bool ShowCorrectAnswer { get; set; }
    public string PointsText { get; set; } = "";
}
