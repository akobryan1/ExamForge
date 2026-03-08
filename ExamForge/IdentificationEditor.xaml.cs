using System.Windows;
using System.Windows.Controls;

namespace ExamForge
{
    public partial class IdentificationEditor : UserControl
    {
        private ExamItem item;
        private bool isLoading = false;

        public IdentificationEditor(ExamItem examItem)
        {
            InitializeComponent();
            item = examItem;
            LoadData();
        }

        private void LoadData()
        {
            isLoading = true;
            AnswerTextBox.Text = item.TextAnswer;
            CustomPointsTextBox.Text = item.CustomPoints;
            isLoading = false;
        }

        private void Answer_TextChanged(object sender, TextChangedEventArgs e)
        {
            if (isLoading) return;
            item.TextAnswer = AnswerTextBox.Text;
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