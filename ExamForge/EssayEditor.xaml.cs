using System.Windows;
using System.Windows.Controls;

namespace ExamForge
{
    public partial class EssayEditor : UserControl
    {
        private ExamItem item;
        private bool isLoading = false;

        public EssayEditor(ExamItem examItem)
        {
            InitializeComponent();
            item = examItem;
            LoadData();
        }

        private void LoadData()
        {
            isLoading = true;
            RubricTextBox.Text = item.TextAnswer;
            CustomPointsTextBox.Text = item.CustomPoints;
            isLoading = false;
        }

        private void Rubric_TextChanged(object sender, TextChangedEventArgs e)
        {
            if (isLoading) return;
            item.TextAnswer = RubricTextBox.Text;
        }

        private void CustomPoints_TextChanged(object sender, TextChangedEventArgs e)
        {
            if (isLoading) return;
            item.CustomPoints = CustomPointsTextBox.Text;
        }
    }
}