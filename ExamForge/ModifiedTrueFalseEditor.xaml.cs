using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;

namespace ExamForge
{
    public partial class ModifiedTrueFalseEditor : UserControl
    {
        private ExamItem _item;

        public ModifiedTrueFalseEditor(ExamItem item)
        {
            InitializeComponent();
            _item = item;
            LoadData();
        }

        private void LoadData()
        {
            TrueRadio.IsChecked = _item.TrueFalseAnswer;
            FalseRadio.IsChecked = !_item.TrueFalseAnswer;
            ModificationTextBox.Text = _item.ModifiedAnswer;
            CustomPointsTextBox.Text = _item.CustomPoints;
        }

        private void TrueOption_Click(object sender, MouseButtonEventArgs e)
        {
            TrueRadio.IsChecked = true;
            RadioButton_Checked(TrueRadio, null);
        }

        private void FalseOption_Click(object sender, MouseButtonEventArgs e)
        {
            FalseRadio.IsChecked = true;
            RadioButton_Checked(FalseRadio, null);
        }

        private void RadioButton_Checked(object sender, RoutedEventArgs e)
        {
            if (_item == null) return;

            _item.TrueFalseAnswer = TrueRadio.IsChecked == true;
            UpdateItemStatus();
        }

        private void Modification_TextChanged(object sender, TextChangedEventArgs e)
        {
            if (_item == null) return;
            
            _item.ModifiedAnswer = ModificationTextBox.Text;
            UpdateItemStatus();
        }

        private void CustomPoints_TextChanged(object sender, TextChangedEventArgs e)
        {
            if (_item == null) return;
            _item.CustomPoints = CustomPointsTextBox.Text;
        }

        private void UpdateItemStatus()
        {
            if (_item == null) return;

            // ✅ FIX: If TRUE is selected, mark as complete immediately
            // Only require correction text if FALSE is selected
            bool isComplete = _item.TrueFalseAnswer == true || 
                             (!_item.TrueFalseAnswer && !string.IsNullOrWhiteSpace(_item.ModifiedAnswer));

            _item.Status = isComplete ? "Complete" : "Incomplete";
        }
    }
}