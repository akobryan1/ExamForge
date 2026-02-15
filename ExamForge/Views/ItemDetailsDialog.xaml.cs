using System;
using System.Collections.Generic;
using System.Linq;
using System.Windows;
using System.Windows.Media;
using ExamForge.Models;

namespace ExamForge.Views;

public partial class ItemDetailsDialog : Window
{
    private string _questionId;
    private string _examId;

    public ItemDetailsDialog(string questionId, string examId, ItemAnalysisData analysisData, ExamContent questionContent)
    {
        InitializeComponent();
        _questionId = questionId;
        _examId = examId;
        LoadItemDetails(analysisData, questionContent);
    }

    private void LoadItemDetails(ItemAnalysisData analysisData, ExamContent questionContent)
    {
        // Header
        QuestionNumberText.Text = $"Question {analysisData.QuestionNumber} Analysis";
        QuestionTypeText.Text = $"{analysisData.QuestionType} • {analysisData.QualityIndicator} Quality";
        
        // Question text
        QuestionText.Text = questionContent?.QuestionText ?? "(Question text not available)";
        
        // Key metrics
        DifficultyText.Text = $"{analysisData.DifficultyPercent:F1}%";
        DiscriminationText.Text = $"{analysisData.Discrimination:F2}";
        AvgTimeText.Text = $"{analysisData.AverageTime}s";
        
        // Additional statistics
        PointBiserialText.Text = $"Point Biserial: {analysisData.PointBiserial:F2}";
        NoResponseText.Text = $"No Response Rate: {analysisData.NoResponsePercent:F1}%";
        QualityText.Text = $"Quality Indicator: {analysisData.QualityIndicator}";
        CorrectAnswerText.Text = $"? Correct Answer: {questionContent?.CorrectAnswer ?? "N/A"}";
        TotalResponsesText.Text = $"Total Responses: {CalculateTotalResponses(analysisData)}";
        
        // Response distribution (for MCQ)
        if (questionContent?.Options != null && questionContent.Options.Any())
        {
            var responseData = GenerateResponseDistribution(questionContent, analysisData);
            ResponseDistribution.ItemsSource = responseData;
        }
        else
        {
            // For non-MCQ questions
            ResponseDistribution.ItemsSource = new List<ResponseDistributionItem>
            {
                new ResponseDistributionItem 
                { 
                    Option = "Correct", 
                    Percentage = $"{analysisData.DifficultyPercent:F1}%",
                    BarWidth = analysisData.DifficultyPercent * 3,
                    BarColor = (Brush)FindResource("SuccessTextBrush")
                },
                new ResponseDistributionItem 
                { 
                    Option = "Incorrect", 
                    Percentage = $"{100 - analysisData.DifficultyPercent:F1}%",
                    BarWidth = (100 - analysisData.DifficultyPercent) * 3,
                    BarColor = (Brush)FindResource("ErrorTextBrush")
                }
            };
        }
    }

    private int CalculateTotalResponses(ItemAnalysisData analysisData)
    {
        // Estimate based on difficulty percentage
        // In a real implementation, this would come from actual submission count
        return 29; // Placeholder
    }

    private List<ResponseDistributionItem> GenerateResponseDistribution(ExamContent questionContent, ItemAnalysisData analysisData)
    {
        var distribution = new List<ResponseDistributionItem>();
        var options = questionContent.Options;
        var correctAnswer = questionContent.CorrectAnswer;
        
        // Simulate distribution (in real implementation, this would come from actual data)
        var random = new Random();
        var remaining = 100.0;
        
        for (int i = 0; i < options.Count; i++)
        {
            var isCorrect = options[i] == correctAnswer;
            double percentage;
            
            if (isCorrect)
            {
                percentage = analysisData.DifficultyPercent;
            }
            else if (i == options.Count - 1)
            {
                percentage = remaining;
            }
            else
            {
                percentage = random.Next(5, (int)Math.Max(5, remaining - 10));
                remaining -= percentage;
            }
            
            distribution.Add(new ResponseDistributionItem
            {
                Option = $"Option {options[i]}",
                Percentage = $"{percentage:F1}%",
                BarWidth = percentage * 3,
                BarColor = isCorrect 
                    ? (Brush)FindResource("SuccessTextBrush") 
                    : (Brush)FindResource("TextSecondaryBrush")
            });
        }
        
        return distribution.OrderByDescending(d => d.BarWidth).ToList();
    }

    private void ExportPdf_Click(object sender, RoutedEventArgs e)
    {
        MessageBox.Show("PDF export functionality coming soon!", "Info", 
            MessageBoxButton.OK, MessageBoxImage.Information);
    }

    private void Close_Click(object sender, RoutedEventArgs e)
    {
        Close();
    }
}

public class ResponseDistributionItem
{
    public string Option { get; set; } = "";
    public string Percentage { get; set; } = "";
    public double BarWidth { get; set; }
    public Brush BarColor { get; set; } = Brushes.Gray;
}
