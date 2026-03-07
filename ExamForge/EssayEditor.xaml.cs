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
            RubricTextBox.Text = item.EssayRubric;
            ModelAnswerTextBox.Text = item.EssayModelAnswer;
            KeyPointsTextBox.Text = item.EssayKeyPoints;
            ThesisWeightSlider.Value = item.EssayWeightThesis;
            EvidenceWeightSlider.Value = item.EssayWeightEvidence;
            ClarityWeightSlider.Value = item.EssayWeightClarity;
            RefreshWeightLabels();
            CustomPointsTextBox.Text = item.CustomPoints;
            isLoading = false;
        }

        private void Rubric_TextChanged(object sender, TextChangedEventArgs e)
        {
            if (isLoading) return;
            item.EssayRubric = RubricTextBox.Text;
        }

        private void ModelAnswer_TextChanged(object sender, TextChangedEventArgs e)
        {
            if (isLoading) return;
            item.EssayModelAnswer = ModelAnswerTextBox.Text;
        }

        private void KeyPoints_TextChanged(object sender, TextChangedEventArgs e)
        {
            if (isLoading) return;
            item.EssayKeyPoints = KeyPointsTextBox.Text;
        }

        private void WeightSlider_ValueChanged(object sender, RoutedPropertyChangedEventArgs<double> e)
        {
            if (isLoading) return;

            item.EssayWeightThesis = ThesisWeightSlider.Value;
            item.EssayWeightEvidence = EvidenceWeightSlider.Value;
            item.EssayWeightClarity = ClarityWeightSlider.Value;
            RefreshWeightLabels();
        }

        private void RefreshWeightLabels()
        {
            ThesisWeightText.Text = $"{ThesisWeightSlider.Value:F0}%";
            EvidenceWeightText.Text = $"{EvidenceWeightSlider.Value:F0}%";
            ClarityWeightText.Text = $"{ClarityWeightSlider.Value:F0}%";
        }

        private void CustomPoints_TextChanged(object sender, TextChangedEventArgs e)
        {
            if (isLoading) return;
            item.CustomPoints = CustomPointsTextBox.Text;
        }
    }
}