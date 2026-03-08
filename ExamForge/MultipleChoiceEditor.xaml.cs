using System.Windows;
using System.Windows.Controls;

namespace ExamForge
{
    public partial class MultipleChoiceEditor : UserControl
    {
        private ExamItem item;
        private bool isLoading = false;

        public MultipleChoiceEditor(ExamItem examItem)
        {
            InitializeComponent();
            item = examItem;
            LoadData();
        }

        private void LoadData()
        {
            isLoading = true;
            OptionATextBox.Text = item.OptionA;
            OptionBTextBox.Text = item.OptionB;
            OptionCTextBox.Text = item.OptionC;
            OptionDTextBox.Text = item.OptionD;

            switch (item.CorrectAnswer)
            {
                case "A": CorrectA.IsChecked = true; break;
                case "B": CorrectB.IsChecked = true; break;
                case "C": CorrectC.IsChecked = true; break;
                case "D": CorrectD.IsChecked = true; break;
            }

            CustomPointsTextBox.Text = item.CustomPoints;
            isLoading = false;
        }

        private void OptionTextBox_TextChanged(object sender, TextChangedEventArgs e)
        {
            if (isLoading) return;
            if (sender == OptionATextBox) item.OptionA = OptionATextBox.Text;
            else if (sender == OptionBTextBox) item.OptionB = OptionBTextBox.Text;
            else if (sender == OptionCTextBox) item.OptionC = OptionCTextBox.Text;
            else if (sender == OptionDTextBox) item.OptionD = OptionDTextBox.Text;
            UpdateItemStatus();
        }

        private void CorrectAnswer_Checked(object sender, RoutedEventArgs e)
        {
            if (isLoading) return;
            if (sender == CorrectA) item.CorrectAnswer = "A";
            else if (sender == CorrectB) item.CorrectAnswer = "B";
            else if (sender == CorrectC) item.CorrectAnswer = "C";
            else if (sender == CorrectD) item.CorrectAnswer = "D";
            UpdateItemStatus();
        }

        private void CustomPoints_TextChanged(object sender, TextChangedEventArgs e)
        {
            if (isLoading) return;
            item.CustomPoints = CustomPointsTextBox.Text;
        }

        private void UpdateItemStatus()
        {
            var isComplete = !string.IsNullOrWhiteSpace(item.Question) && item.HasValidAnswer();
            item.Status = isComplete ? "Complete" : "Incomplete";
        }
    }
}