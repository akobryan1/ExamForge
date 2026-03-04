using System;
using System.Collections.Generic;
using System.Linq;
using System.Windows;
using System.Windows.Controls;
using ExamForge.Models;
using ExamForge.Services;
using Microsoft.Win32;

namespace ExamForge
{
    /// <summary>
    /// Interaction logic for essay_checker_usercontrol.xaml
    /// </summary>
    public partial class essay_checker_usercontrol : UserControl
    {
        private readonly EssayCheckerService _essayCheckerService;
        private Rubric _currentRubric;

        public essay_checker_usercontrol()
        {
            InitializeComponent();
            _essayCheckerService = new EssayCheckerService();
            _currentRubric = Rubric.CreateDefaultEssayRubric();
            LoadRubricDisplay();
            UpdateApiStatus();
        }

        private void LoadRubricDisplay()
        {
            RubricTitleText.Text = _currentRubric.Title;
            RubricPointsText.Text = $"Total: {_currentRubric.TotalPoints} points · {_currentRubric.Criteria.Count} criteria";
            RubricCriteriaList.ItemsSource = _currentRubric.Criteria;
        }

        private void UpdateApiStatus()
        {
            if (_essayCheckerService.IsConfigured)
            {
                ApiStatusText.Text = "● Connected";
                ApiStatusText.Foreground = (System.Windows.Media.Brush)FindResource("SuccessTextBrush");
            }
            else
            {
                ApiStatusText.Text = "● Not configured";
                ApiStatusText.Foreground = (System.Windows.Media.Brush)FindResource("ErrorTextBrush");
            }
        }

        private void ApiKeyBox_PasswordChanged(object sender, RoutedEventArgs e)
        {
            _essayCheckerService.SetApiKey(ApiKeyBox.Password);
            UpdateApiStatus();
        }

        private void ModelComboBox_SelectionChanged(object sender, SelectionChangedEventArgs e)
        {
            if (ModelComboBox.SelectedItem is ComboBoxItem selected)
            {
                _essayCheckerService.SetModel(selected.Content.ToString() ?? "gpt-4o-mini");
            }
        }

        private void UploadRubric_Click(object sender, RoutedEventArgs e)
        {
            var dialog = new OpenFileDialog
            {
                Filter = "JSON Files (*.json)|*.json|All Files (*.*)|*.*",
                Title = "Upload Rubric File"
            };

            if (dialog.ShowDialog() == true)
            {
                var rubric = EssayCheckerService.LoadRubricFromFile(dialog.FileName);
                if (rubric != null && rubric.Criteria.Count > 0)
                {
                    _currentRubric = rubric;
                    LoadRubricDisplay();
                    MessageBox.Show($"Rubric \"{rubric.Title}\" loaded successfully with {rubric.Criteria.Count} criteria.",
                        "Rubric Loaded", MessageBoxButton.OK, MessageBoxImage.Information);
                }
                else
                {
                    MessageBox.Show("Failed to load rubric. Please ensure the JSON file has the correct format.\n\n" +
                        "Expected format:\n" +
                        "{\n  \"Title\": \"My Rubric\",\n  \"TotalPoints\": 100,\n  \"Criteria\": [\n    {\n      \"Name\": \"...\",\n      \"Description\": \"...\",\n      \"MaxPoints\": 25,\n      \"Levels\": [\"Excellent (...)\", \"Good (...)\", ...]\n    }\n  ]\n}",
                        "Invalid Rubric", MessageBoxButton.OK, MessageBoxImage.Warning);
                }
            }
        }

        private void ExportRubric_Click(object sender, RoutedEventArgs e)
        {
            var dialog = new SaveFileDialog
            {
                Filter = "JSON Files (*.json)|*.json",
                Title = "Export Rubric",
                FileName = $"{_currentRubric.Title.Replace(" ", "_")}.json"
            };

            if (dialog.ShowDialog() == true)
            {
                if (EssayCheckerService.SaveRubricToFile(_currentRubric, dialog.FileName))
                {
                    MessageBox.Show("Rubric exported successfully.", "Export Complete",
                        MessageBoxButton.OK, MessageBoxImage.Information);
                }
                else
                {
                    MessageBox.Show("Failed to export rubric.", "Export Error",
                        MessageBoxButton.OK, MessageBoxImage.Error);
                }
            }
        }

        private void ResetRubric_Click(object sender, RoutedEventArgs e)
        {
            var result = MessageBox.Show("Reset to the default essay rubric?", "Reset Rubric",
                MessageBoxButton.YesNo, MessageBoxImage.Question);

            if (result == MessageBoxResult.Yes)
            {
                _currentRubric = Rubric.CreateDefaultEssayRubric();
                LoadRubricDisplay();
            }
        }

        private async void EvaluateButton_Click(object sender, RoutedEventArgs e)
        {
            if (!_essayCheckerService.IsConfigured)
            {
                MessageBox.Show("Please enter your LLM API key to evaluate essays.",
                    "API Key Required", MessageBoxButton.OK, MessageBoxImage.Warning);
                return;
            }

            var essayText = EssayTextBox.Text?.Trim();
            if (string.IsNullOrWhiteSpace(essayText))
            {
                MessageBox.Show("Please enter the student essay text to evaluate.",
                    "Essay Required", MessageBoxButton.OK, MessageBoxImage.Warning);
                return;
            }

            // Show loading state
            EvaluateButton.IsEnabled = false;
            LoadingOverlay.Visibility = Visibility.Visible;
            ResultsCard.Visibility = Visibility.Collapsed;

            try
            {
                var questionPrompt = EssayPromptBox.Text?.Trim() ?? "";
                var result = await _essayCheckerService.EvaluateEssayAsync(essayText, questionPrompt, _currentRubric);
                DisplayResults(result);
            }
            catch (Exception ex)
            {
                MessageBox.Show($"Error evaluating essay:\n\n{ex.Message}",
                    "Evaluation Error", MessageBoxButton.OK, MessageBoxImage.Error);
            }
            finally
            {
                EvaluateButton.IsEnabled = true;
                LoadingOverlay.Visibility = Visibility.Collapsed;
            }
        }

        private void DisplayResults(EssayEvaluationResult result)
        {
            ResultsCard.Visibility = Visibility.Visible;

            TotalScoreText.Text = result.TotalScore.ToString();
            MaxScoreText.Text = $"/{result.MaxScore}";
            LetterGradeText.Text = result.LetterGrade;
            OverallFeedbackText.Text = result.OverallFeedback;

            // Map criterion scores for display
            var displayScores = result.CriterionScores.Select(cs => new CriterionScoreDisplay
            {
                CriterionName = cs.CriterionName,
                ScoreDisplay = $"{cs.Score}/{cs.MaxPoints}",
                Level = cs.Level,
                Feedback = cs.Feedback
            }).ToList();

            CriterionScoresList.ItemsSource = displayScores;
            StrengthsList.ItemsSource = result.Strengths;
            ImprovementsList.ItemsSource = result.AreasForImprovement;
        }
    }

    /// <summary>
    /// View model for displaying criterion scores in the UI.
    /// </summary>
    public class CriterionScoreDisplay
    {
        public string CriterionName { get; set; } = "";
        public string ScoreDisplay { get; set; } = "";
        public string Level { get; set; } = "";
        public string Feedback { get; set; } = "";
    }
}
