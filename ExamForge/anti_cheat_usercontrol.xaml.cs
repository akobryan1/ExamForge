using System;
using System.Text.RegularExpressions;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;

namespace ExamForge
{
    /// <summary>
    /// Interaction logic for anti_cheat_usercontrol.xaml
    /// </summary>
    public partial class anti_cheat_usercontrol : UserControl
    {
        public anti_cheat_usercontrol()
        {
            InitializeComponent();
        }

        private void DetectTabbing_CheckedChanged(object sender, RoutedEventArgs e)
        {
            bool isChecked = detect_tabbing_checkbox.IsChecked == true;
            
            warning_only_checkbox.IsEnabled = isChecked;
            deduct_points_checkbox.IsEnabled = isChecked;
            auto_submit_checkbox.IsEnabled = isChecked;

            if (!isChecked)
            {
                warning_only_checkbox.IsChecked = false;
                deduct_points_checkbox.IsChecked = false;
                auto_submit_checkbox.IsChecked = false;
                deduct_points_textbox.IsEnabled = false;
            }
        }

        private void DeductPoints_CheckedChanged(object sender, RoutedEventArgs e)
        {
            deduct_points_textbox.IsEnabled = deduct_points_checkbox.IsChecked == true;
        }

        private void OneQuestionAtATime_CheckedChanged(object sender, RoutedEventArgs e)
        {
            bool isChecked = one_question_at_a_time_checkbox.IsChecked == true;
            
            disable_backtrack_checkbox.IsEnabled = isChecked;
            time_limit_per_question_checkbox.IsEnabled = isChecked;

            if (!isChecked)
            {
                disable_backtrack_checkbox.IsChecked = false;
                time_limit_per_question_checkbox.IsChecked = false;
                time_limit_textbox.IsEnabled = false;
            }
        }

        private void TimeLimitPerQuestion_CheckedChanged(object sender, RoutedEventArgs e)
        {
            time_limit_textbox.IsEnabled = time_limit_per_question_checkbox.IsChecked == true;
        }

        private void NumericOnly_PreviewTextInput(object sender, TextCompositionEventArgs e)
        {
            Regex regex = new Regex("[^0-9]+");
            e.Handled = regex.IsMatch(e.Text);
        }

        public object SaveState()
        {
            return new AntiCheatState
            {
                DetectTabbing = detect_tabbing_checkbox.IsChecked == true,
                WarningOnly = warning_only_checkbox.IsChecked == true,
                DeductPoints = deduct_points_checkbox.IsChecked == true,
                DeductPointsValue = deduct_points_textbox.Text,
                AutoSubmit = auto_submit_checkbox.IsChecked == true,
                OneQuestionAtATime = one_question_at_a_time_checkbox.IsChecked == true,
                DisableBacktrack = disable_backtrack_checkbox.IsChecked == true,
                TimeLimitPerQuestion = time_limit_per_question_checkbox.IsChecked == true,
                TimeLimitValue = time_limit_textbox.Text,
                DisableCopyPaste = disable_copy_paste_checkbox.IsChecked == true,
                DisableScreenshot = disable_screenshot_checkbox.IsChecked == true,
                AutoResumeSession = auto_resume_session_checkbox.IsChecked == true
            };
        }

        public void RestoreState(object state)
        {
            if (state is AntiCheatState savedState)
            {
                detect_tabbing_checkbox.IsChecked = savedState.DetectTabbing;
                warning_only_checkbox.IsChecked = savedState.WarningOnly;
                deduct_points_checkbox.IsChecked = savedState.DeductPoints;
                deduct_points_textbox.Text = savedState.DeductPointsValue;
                auto_submit_checkbox.IsChecked = savedState.AutoSubmit;
                one_question_at_a_time_checkbox.IsChecked = savedState.OneQuestionAtATime;
                disable_backtrack_checkbox.IsChecked = savedState.DisableBacktrack;
                time_limit_per_question_checkbox.IsChecked = savedState.TimeLimitPerQuestion;
                time_limit_textbox.Text = savedState.TimeLimitValue;
                disable_copy_paste_checkbox.IsChecked = savedState.DisableCopyPaste;
                disable_screenshot_checkbox.IsChecked = savedState.DisableScreenshot;
                auto_resume_session_checkbox.IsChecked = savedState.AutoResumeSession;
            }
        }
    }

    public class AntiCheatState
    {
        public bool DetectTabbing { get; set; }
        public bool WarningOnly { get; set; }
        public bool DeductPoints { get; set; }
        public string DeductPointsValue { get; set; } = "0";
        public bool AutoSubmit { get; set; }
        public bool OneQuestionAtATime { get; set; }
        public bool DisableBacktrack { get; set; }
        public bool TimeLimitPerQuestion { get; set; }
        public string TimeLimitValue { get; set; } = "60";
        public bool DisableCopyPaste { get; set; }
        public bool DisableScreenshot { get; set; }
        public bool AutoResumeSession { get; set; }
    }
}
