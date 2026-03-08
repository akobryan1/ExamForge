using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;

namespace ExamForge
{
    public partial class TrueFalseEditor : UserControl
    {
        private ExamItem item;
        private bool isLoading = false;

        public TrueFalseEditor(ExamItem examItem)
        {
            InitializeComponent();
            item = examItem;
            LoadData();
        }

        private void LoadData()
        {
            isLoading = true;
            TrueRadio.IsChecked = item.TrueFalseAnswer;
            FalseRadio.IsChecked = !item.TrueFalseAnswer;
            CustomPointsTextBox.Text = item.CustomPoints;
            isLoading = false;
        }

        private void TrueOption_Click(object sender, MouseButtonEventArgs e)
        {
            TrueRadio.IsChecked = true;
            if (!isLoading)
            {
                item.TrueFalseAnswer = true;
                UpdateItemStatus();
            }
        }

        private void FalseOption_Click(object sender, MouseButtonEventArgs e)
        {
            FalseRadio.IsChecked = true;
            if (!isLoading)
            {
                item.TrueFalseAnswer = false;
                UpdateItemStatus();
            }
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