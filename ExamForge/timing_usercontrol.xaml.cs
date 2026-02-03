using System;
using System.Text.RegularExpressions;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;

namespace ExamForge
{
    /// <summary>
    /// Interaction logic for timing_usercontrol.xaml
    /// </summary>
    public partial class timing_usercontrol : UserControl
    {
        public timing_usercontrol()
        {
            InitializeComponent();
        }

        private void NumericOnly_PreviewTextInput(object sender, TextCompositionEventArgs e)
        {
            Regex regex = new Regex("[^0-9]+");
            e.Handled = regex.IsMatch(e.Text);
        }

        public object SaveState()
        {
            return new TimingState
            {
                Hours = hours_textbox.Text,
                Minutes = minutes_textbox.Text,
                Seconds = seconds_textbox.Text,
                StartDateTime = start_datetime_picker.Value,
                EndDateTime = end_datetime_picker.Value
            };
        }

        public void RestoreState(object state)
        {
            if (state is TimingState savedState)
            {
                hours_textbox.Text = savedState.Hours;
                minutes_textbox.Text = savedState.Minutes;
                seconds_textbox.Text = savedState.Seconds;
                start_datetime_picker.Value = savedState.StartDateTime;
                end_datetime_picker.Value = savedState.EndDateTime;
            }
        }
    }

    public class TimingState
    {
        public string Hours { get; set; } = "";
        public string Minutes { get; set; } = "";
        public string Seconds { get; set; } = "";
        public DateTime? StartDateTime { get; set; }
        public DateTime? EndDateTime { get; set; }
    }
}
