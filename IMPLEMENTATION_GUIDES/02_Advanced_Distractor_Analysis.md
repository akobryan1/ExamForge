# Advanced Distractor Analysis Implementation Guide

## Overview
Analyze the effectiveness of incorrect answer options (distractors) in multiple-choice questions to improve exam quality.

## Step 1: Create Distractor Analysis Model

Add to `ExamForge/Models/DistractorAnalysis.cs`:

```csharp
namespace ExamForge.Models
{
    public class DistractorAnalysis
    {
        public string QuestionId { get; set; } = "";
        public int QuestionNumber { get; set; }
        public string QuestionText { get; set; } = "";
        public string CorrectAnswer { get; set; } = "";
        public List<DistractorOption> Options { get; set; } = new();
        public double Difficulty { get; set; }
        public double Discrimination { get; set; }
        public string Quality { get; set; } = "";
    }

    public class DistractorOption
    {
        public string Option { get; set; } = "";
        public int SelectionCount { get; set; }
        public double SelectionPercentage { get; set; }
        public bool IsCorrectAnswer { get; set; }
        public double PointBiserial { get; set; }
        public string Effectiveness { get; set; } = ""; // "Good", "Poor", "Non-functioning"
        public string Recommendation { get; set; } = "";
    }
}
```

## Step 2: Add Analysis Method to AnalyticsService

Add to `ExamForge/Services/AnalyticsService.cs`:

```csharp
public async Task<List<DistractorAnalysis>> AnalyzeDistractorsAsync(
    string examId,
    List<ExamSubmission> submissions)
{
    var exam = await _firestoreService.GetPublishedExamAsync(examId);
    if (exam == null) return new List<DistractorAnalysis>();

    var analyses = new List<DistractorAnalysis>();

    foreach (var question in exam.Contents.Where(c => c.QuestionType == "MCQ"))
    {
        var analysis = new DistractorAnalysis
        {
            QuestionId = question.ContentId,
            QuestionNumber = exam.Contents.IndexOf(question) + 1,
            QuestionText = question.Question,
            CorrectAnswer = question.Answer
        };

        // Count selections for each option
        var optionCounts = new Dictionary<string, int>();
        var correctResponders = new List<ExamSubmission>();
        var incorrectResponders = new List<ExamSubmission>();

        foreach (var option in question.Options)
        {
            optionCounts[option] = 0;
        }

        foreach (var submission in submissions)
        {
            var response = submission.Responses.FirstOrDefault(r => r.QuestionId == question.ContentId);
            if (response != null && !string.IsNullOrEmpty(response.Answer))
            {
                if (optionCounts.ContainsKey(response.Answer))
                {
                    optionCounts[response.Answer]++;
                }

                if (response.IsCorrect)
                    correctResponders.Add(submission);
                else
                    incorrectResponders.Add(submission);
            }
        }

        int totalResponses = optionCounts.Values.Sum();

        // Analyze each distractor
        foreach (var option in question.Options)
        {
            var count = optionCounts[option];
            var percentage = totalResponses > 0 ? (count * 100.0 / totalResponses) : 0;
            var isCorrect = option == question.Answer;

            // Calculate point biserial for this option
            var selectedThisOption = submissions.Where(s => 
                s.Responses.Any(r => r.QuestionId == question.ContentId && r.Answer == option)).ToList();
            
            var pointBiserial = CalculatePointBiserial(
                selectedThisOption,
                submissions,
                question.ContentId);

            // Determine effectiveness
            string effectiveness;
            string recommendation;

            if (isCorrect)
            {
                // Correct answer should be selected by majority of high scorers
                effectiveness = percentage >= 50 ? "Good" : "Poor";
                recommendation = percentage < 50 
                    ? "Too difficult - consider revising question or distractors"
                    : "Correct answer is well-differentiated";
            }
            else
            {
                // Distractor analysis
                if (percentage < 5)
                {
                    effectiveness = "Non-functioning";
                    recommendation = "This distractor is rarely chosen. Consider replacing with a more plausible option.";
                }
                else if (percentage > 30)
                {
                    effectiveness = "Too Attractive";
                    recommendation = "This distractor may be too similar to the correct answer or contain partial truth. Review for ambiguity.";
                }
                else if (pointBiserial > 0)
                {
                    effectiveness = "Problematic";
                    recommendation = "High scorers are selecting this option more than low scorers. Review for correctness.";
                }
                else
                {
                    effectiveness = "Good";
                    recommendation = "This distractor is functioning well.";
                }
            }

            analysis.Options.Add(new DistractorOption
            {
                Option = option,
                SelectionCount = count,
                SelectionPercentage = percentage,
                IsCorrectAnswer = isCorrect,
                PointBiserial = pointBiserial,
                Effectiveness = effectiveness,
                Recommendation = recommendation
            });
        }

        // Overall question metrics
        analysis.Difficulty = optionCounts[question.Answer] * 100.0 / totalResponses;
        analysis.Discrimination = CalculateDiscrimination(submissions, question.ContentId);
        analysis.Quality = DetermineQuestionQuality(analysis.Difficulty, analysis.Discrimination);

        analyses.Add(analysis);
    }

    return analyses;
}

private double CalculatePointBiserial(
    List<ExamSubmission> selectedOption,
    List<ExamSubmission> allSubmissions,
    string questionId)
{
    if (selectedOption.Count == 0 || allSubmissions.Count == 0)
        return 0;

    var selectedScores = selectedOption.Select(s => s.TotalScore).ToList();
    var allScores = allSubmissions.Select(s => s.TotalScore).ToList();

    var meanSelected = selectedScores.Average();
    var meanAll = allScores.Average();
    var stdDev = CalculateStandardDeviation(allScores);

    if (stdDev == 0) return 0;

    var p = selectedOption.Count / (double)allSubmissions.Count;
    var q = 1 - p;

    return (meanSelected - meanAll) / stdDev * Math.Sqrt(p * q);
}

private string DetermineQuestionQuality(double difficulty, double discrimination)
{
    if (discrimination >= 0.4)
        return "Excellent";
    else if (discrimination >= 0.3)
        return "Good";
    else if (discrimination >= 0.2)
        return "Fair";
    else if (discrimination >= 0.1)
        return "Poor";
    else
        return "Very Poor";
}
```

## Step 3: Create Distractor Analysis Dialog

Create `ExamForge/Views/DistractorAnalysisDialog.xaml`:

```xaml
<Window x:Class="ExamForge.Views.DistractorAnalysisDialog"
        xmlns="http://schemas.microsoft.com/winfx/2006/xaml/presentation"
        xmlns:x="http://schemas.microsoft.com/winfx/2006/xaml"
        Title="Advanced Distractor Analysis"
        Height="700" Width="1000"
        WindowStartupLocation="CenterOwner"
        Background="{StaticResource AppBackgroundBrush}">
    
    <Grid>
        <Grid.RowDefinitions>
            <RowDefinition Height="Auto"/>
            <RowDefinition Height="*"/>
            <RowDefinition Height="Auto"/>
        </Grid.RowDefinitions>

        <!-- Header -->
        <Border Grid.Row="0" Background="{StaticResource CardBackgroundBrush}" 
                Padding="25,20" BorderBrush="{StaticResource BorderBrushLight}" 
                BorderThickness="0,0,0,1">
            <StackPanel>
                <TextBlock Text="Advanced Distractor Analysis" FontSize="20" 
                           FontWeight="SemiBold" Foreground="{StaticResource TextPrimaryBrush}"/>
                <TextBlock x:Name="ExamInfoText" FontSize="13" 
                           Foreground="{StaticResource TextSecondaryBrush}" Margin="0,5,0,0"/>
            </StackPanel>
        </Border>

        <!-- Content -->
        <ScrollViewer Grid.Row="1" VerticalScrollBarVisibility="Auto" Padding="25,20">
            <ItemsControl x:Name="AnalysisContainer">
                <ItemsControl.ItemTemplate>
                    <DataTemplate>
                        <Border Background="{StaticResource CardBackgroundBrush}" 
                                BorderBrush="{StaticResource BorderBrushLight}" 
                                BorderThickness="1" CornerRadius="8" Padding="20" Margin="0,0,0,20">
                            <StackPanel>
                                <!-- Question Header -->
                                <DockPanel Margin="0,0,0,15">
                                    <TextBlock DockPanel.Dock="Left" 
                                               Text="{Binding QuestionNumberText}" 
                                               FontSize="16" FontWeight="SemiBold"
                                               Foreground="{StaticResource TextPrimaryBrush}"/>
                                    <Border DockPanel.Dock="Right" 
                                            Background="{Binding QualityBackground}" 
                                            CornerRadius="4" Padding="8,4">
                                        <TextBlock Text="{Binding Quality}" 
                                                   FontSize="11" FontWeight="SemiBold"
                                                   Foreground="White"/>
                                    </Border>
                                </DockPanel>

                                <!-- Question Text -->
                                <TextBlock Text="{Binding QuestionText}" 
                                           FontSize="13" TextWrapping="Wrap"
                                           Foreground="{StaticResource TextPrimaryBrush}" 
                                           Margin="0,0,0,15"/>

                                <!-- Metrics -->
                                <Grid Margin="0,0,0,15">
                                    <Grid.ColumnDefinitions>
                                        <ColumnDefinition Width="*"/>
                                        <ColumnDefinition Width="*"/>
                                    </Grid.ColumnDefinitions>
                                    <TextBlock Grid.Column="0" 
                                               Text="{Binding DifficultyText}" 
                                               FontSize="12"/>
                                    <TextBlock Grid.Column="1" 
                                               Text="{Binding DiscriminationText}" 
                                               FontSize="12"/>
                                </Grid>

                                <!-- Distractor Options -->
                                <ItemsControl ItemsSource="{Binding Options}">
                                    <ItemsControl.ItemTemplate>
                                        <DataTemplate>
                                            <Border Background="{StaticResource HeaderBackgroundBrush}" 
                                                    CornerRadius="6" Padding="15" Margin="0,0,0,10">
                                                <StackPanel>
                                                    <DockPanel>
                                                        <TextBlock DockPanel.Dock="Left" 
                                                                   Text="{Binding OptionLabel}" 
                                                                   FontWeight="SemiBold"
                                                                   Foreground="{StaticResource TextPrimaryBrush}"/>
                                                        <Border DockPanel.Dock="Right" 
                                                                Background="{Binding EffectivenessBackground}" 
                                                                CornerRadius="3" Padding="6,2">
                                                            <TextBlock Text="{Binding Effectiveness}" 
                                                                       FontSize="10" 
                                                                       Foreground="White"/>
                                                        </Border>
                                                        <TextBlock Text="{Binding SelectionText}" 
                                                                   FontSize="12" Margin="10,0"
                                                                   Foreground="{StaticResource TextSecondaryBrush}"/>
                                                    </DockPanel>
                                                    <TextBlock Text="{Binding Recommendation}" 
                                                               FontSize="11" FontStyle="Italic"
                                                               Foreground="{StaticResource TextMutedBrush}" 
                                                               TextWrapping="Wrap" Margin="0,5,0,0"/>
                                                </StackPanel>
                                            </Border>
                                        </DataTemplate>
                                    </ItemsControl.ItemTemplate>
                                </ItemsControl>
                            </StackPanel>
                        </Border>
                    </DataTemplate>
                </ItemsControl.ItemTemplate>
            </ItemsControl>
        </ScrollViewer>

        <!-- Footer -->
        <Border Grid.Row="2" Background="{StaticResource CardBackgroundBrush}" 
                BorderBrush="{StaticResource BorderBrushLight}" 
                BorderThickness="0,1,0,0" Padding="25,15">
            <StackPanel Orientation="Horizontal" HorizontalAlignment="Right">
                <Button Content="Export Report" Width="130" Height="35" Margin="0,0,10,0"
                        Style="{StaticResource PrimaryButtonStyle}" Click="ExportReport_Click"/>
                <Button Content="Close" Width="100" Height="35" 
                        Style="{StaticResource SecondaryButtonStyle}" 
                        IsCancel="True" Click="Close_Click"/>
            </StackPanel>
        </Border>
    </Grid>
</Window>
```

## Step 4: Add Button to Item Analysis

In `student_analytics_usercontrol.xaml`, add a button:

```xaml
<Button Content="?? Advanced Distractor Analysis" 
        Margin="16,0,0,0" Padding="12,6" 
        Background="{StaticResource AccentBlue}" 
        Foreground="White" BorderThickness="0" 
        Style="{StaticResource RoundedButtonStyle}" 
        Click="DistractorAnalysis_Click"/>
```

## Implementation Time: 3-5 hours
## Complexity: High
## Benefits: Identifies weak questions and distractors for exam improvement
