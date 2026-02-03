using System.Collections.Generic;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Media;

namespace ExamForge
{
    public partial class EnumerationEditor : UserControl
    {
        private ExamItem item;
        private bool isLoading = false;
        private List<TextBox> answerTextBoxes = new List<TextBox>();

        public EnumerationEditor(ExamItem examItem)
        {
            InitializeComponent();
            item = examItem;
            LoadData();
        }

        private void LoadData()
        {
            isLoading = true;
            
            // Load existing answers or create default ones
            if (item.EnumerationAnswers.Count == 0)
            {
                // Start with 3 empty answers
                for (int i = 0; i < 3; i++)
                {
                    item.EnumerationAnswers.Add("");
                }
            }

            // Create UI for each answer
            foreach (var answer in item.EnumerationAnswers)
            {
                AddAnswerUI(answer);
            }

            CustomPointsTextBox.Text = item.CustomPoints;
            isLoading = false;
        }

        private void AddAnswerUI(string answerText = "")
        {
            var answerGrid = new Grid { Margin = new Thickness(0, 0, 0, 8) };
            answerGrid.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(30) });
            answerGrid.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(1, GridUnitType.Star) });
            answerGrid.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(40) });

            // Number label
            var numberLabel = new TextBlock
            {
                Text = $"{answerTextBoxes.Count + 1}.",
                FontSize = 12,
                Foreground = (Brush)Application.Current.Resources["TextSecondary"],
                VerticalAlignment = VerticalAlignment.Center,
                Margin = new Thickness(0, 0, 8, 0)
            };
            Grid.SetColumn(numberLabel, 0);

            // Answer textbox
            var textBox = new TextBox
            {
                Text = answerText,
                Background = (Brush)Application.Current.Resources["SurfaceDark"],
                Foreground = (Brush)Application.Current.Resources["TextPrimary"],
                BorderBrush = (Brush)Application.Current.Resources["BorderColor"],
                BorderThickness = new Thickness(1),
                Padding = new Thickness(8, 6, 8, 6),
                VerticalAlignment = VerticalAlignment.Center
            };
            
            // Add rounded corners
            var borderStyle = new Style(typeof(Border));
            borderStyle.Setters.Add(new Setter(Border.CornerRadiusProperty, new CornerRadius(6)));
            textBox.Resources.Add(typeof(Border), borderStyle);
            
            textBox.TextChanged += AnswerTextBox_TextChanged;
            Grid.SetColumn(textBox, 1);
            answerTextBoxes.Add(textBox);

            // Delete button
            var deleteButton = new Button
            {
                Content = "✕",
                Background = Brushes.Transparent,
                Foreground = (Brush)Application.Current.Resources["TextSecondary"],
                BorderThickness = new Thickness(0),
                FontSize = 16,
                Cursor = System.Windows.Input.Cursors.Hand,
                Margin = new Thickness(8, 0, 0, 0),
                Tag = answerGrid
            };
            deleteButton.Click += DeleteAnswer_Click;
            Grid.SetColumn(deleteButton, 2);

            answerGrid.Children.Add(numberLabel);
            answerGrid.Children.Add(textBox);
            answerGrid.Children.Add(deleteButton);

            AnswersPanel.Children.Add(answerGrid);
        }

        private void AddAnswer_Click(object sender, RoutedEventArgs e)
        {
            item.EnumerationAnswers.Add("");
            AddAnswerUI("");
            UpdateAnswerNumbers();
        }

        private void DeleteAnswer_Click(object sender, RoutedEventArgs e)
        {
            if (answerTextBoxes.Count <= 1) return; // Keep at least one answer

            var button = sender as Button;
            var grid = button?.Tag as Grid;
            if (grid != null)
            {
                int index = AnswersPanel.Children.IndexOf(grid);
                if (index >= 0 && index < answerTextBoxes.Count)
                {
                    answerTextBoxes.RemoveAt(index);
                    item.EnumerationAnswers.RemoveAt(index);
                    AnswersPanel.Children.RemoveAt(index);
                    UpdateAnswerNumbers();
                }
            }
        }

        private void AnswerTextBox_TextChanged(object sender, TextChangedEventArgs e)
        {
            if (isLoading) return;
            
            var textBox = sender as TextBox;
            int index = answerTextBoxes.IndexOf(textBox);
            if (index >= 0 && index < item.EnumerationAnswers.Count)
            {
                item.EnumerationAnswers[index] = textBox.Text;
            }
        }

        private void UpdateAnswerNumbers()
        {
            for (int i = 0; i < AnswersPanel.Children.Count; i++)
            {
                if (AnswersPanel.Children[i] is Grid grid && grid.Children[0] is TextBlock label)
                {
                    label.Text = $"{i + 1}.";
                }
            }
        }

        private void CustomPoints_TextChanged(object sender, TextChangedEventArgs e)
        {
            if (isLoading) return;
            item.CustomPoints = CustomPointsTextBox.Text;
        }
    }
}