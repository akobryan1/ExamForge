using System;
using System.Windows;

namespace ExamForge.Views
{
    public partial class GradingDialog : Window
    {
        public string GradingItemId { get; }
        public int MaxPoints { get; }
        public int? PointsAwarded { get; private set; }
        public string Feedback { get; private set; } = string.Empty;

        public GradingDialog(string gradingItemId, string studentName, int questionNumber, string questionText, string studentAnswer, int maxPoints)
        {
            InitializeComponent();
            GradingItemId = gradingItemId;
            MaxPoints = maxPoints;

            StudentNameText.Text = studentName;
            QuestionNumberText.Text = $"Question {questionNumber}";
            QuestionTextBlock.Text = questionText;
            StudentAnswerText.Text = string.IsNullOrWhiteSpace(studentAnswer) ? "(No response)" : studentAnswer;
            MaxPointsText.Text = $"/ {maxPoints} points";
        }

        private void CancelButton_Click(object sender, RoutedEventArgs e)
        {
            DialogResult = false;
            Close();
        }

        private void SaveButton_Click(object sender, RoutedEventArgs e)
        {
            if (!int.TryParse(PointsTextBox.Text, out int points))
            {
                MessageBox.Show("Please enter a valid numeric score.", "Validation", MessageBoxButton.OK, MessageBoxImage.Warning);
                return;
            }

            if (points < 0)
            {
                MessageBox.Show("Points cannot be negative.", "Validation", MessageBoxButton.OK, MessageBoxImage.Warning);
                return;
            }

            if (points > MaxPoints)
            {
                MessageBox.Show($"Points cannot exceed {MaxPoints}.", "Validation", MessageBoxButton.OK, MessageBoxImage.Warning);
                return;
            }

            PointsAwarded = points;
            Feedback = FeedbackTextBox.Text ?? string.Empty;
            DialogResult = true;
            Close();
        }
    }
}
