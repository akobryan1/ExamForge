using System;
using System.Linq;
using System.Windows;
using ExamForge.Models;

namespace ExamForge.Views;

public partial class EditScheduleDialog : Window
{
    private PublishedExam _exam;
    public bool ChangesSaved { get; private set; }

    public EditScheduleDialog(PublishedExam exam)
    {
        InitializeComponent();
        _exam = exam;
        LoadScheduleData();
    }

    private void LoadScheduleData()
    {
        ExamTitleText.Text = _exam.Title;
        
        // Set start date/time
        StartDatePicker.SelectedDate = _exam.StartTime.Date;
        StartHourCombo.SelectedIndex = _exam.StartTime.Hour % 12;
        StartMinuteCombo.SelectedIndex = _exam.StartTime.Minute / 5;
        StartAmPmCombo.SelectedIndex = _exam.StartTime.Hour >= 12 ? 1 : 0;
        
        // Set end date/time
        EndDatePicker.SelectedDate = _exam.EndTime.Date;
        EndHourCombo.SelectedIndex = _exam.EndTime.Hour % 12;
        EndMinuteCombo.SelectedIndex = _exam.EndTime.Minute / 5;
        EndAmPmCombo.SelectedIndex = _exam.EndTime.Hour >= 12 ? 1 : 0;
        
        // Set duration
        DurationTextBox.Text = _exam.ExamDuration.ToString();
        
        // Populate hour combos
        for (int i = 1; i <= 12; i++)
        {
            StartHourCombo.Items.Add(i);
            EndHourCombo.Items.Add(i);
        }
        
        // Populate minute combos
        for (int i = 0; i < 60; i += 5)
        {
            StartMinuteCombo.Items.Add(i.ToString("00"));
            EndMinuteCombo.Items.Add(i.ToString("00"));
        }
    }

    private async void SaveChanges_Click(object sender, RoutedEventArgs e)
    {
        try
        {
            // Validate inputs
            if (!StartDatePicker.SelectedDate.HasValue || !EndDatePicker.SelectedDate.HasValue)
            {
                MessageBox.Show("Please select both start and end dates.", "Validation Error", 
                    MessageBoxButton.OK, MessageBoxImage.Warning);
                return;
            }

            if (!int.TryParse(DurationTextBox.Text, out int duration) || duration <= 0)
            {
                MessageBox.Show("Please enter a valid duration in minutes.", "Validation Error", 
                    MessageBoxButton.OK, MessageBoxImage.Warning);
                return;
            }

            // Parse time
            int startHour = int.Parse(StartHourCombo.SelectedItem.ToString());
            if (StartAmPmCombo.SelectedIndex == 1 && startHour != 12) startHour += 12;
            if (StartAmPmCombo.SelectedIndex == 0 && startHour == 12) startHour = 0;

            int endHour = int.Parse(EndHourCombo.SelectedItem.ToString());
            if (EndAmPmCombo.SelectedIndex == 1 && endHour != 12) endHour += 12;
            if (EndAmPmCombo.SelectedIndex == 0 && endHour == 12) endHour = 0;

            int startMinute = int.Parse(StartMinuteCombo.SelectedItem.ToString());
            int endMinute = int.Parse(EndMinuteCombo.SelectedItem.ToString());

            // Create new date times
            var newStartTime = StartDatePicker.SelectedDate.Value.Date.AddHours(startHour).AddMinutes(startMinute);
            var newEndTime = EndDatePicker.SelectedDate.Value.Date.AddHours(endHour).AddMinutes(endMinute);

            // Validate end time is after start time
            if (newEndTime <= newStartTime)
            {
                MessageBox.Show("End time must be after start time.", "Validation Error", 
                    MessageBoxButton.OK, MessageBoxImage.Warning);
                return;
            }

            // Update exam
            _exam.StartTime = newStartTime;
            _exam.EndTime = newEndTime;
            _exam.ExamDuration = duration;

            // Save to Firestore
            var firestoreService = App.FirestoreService;
            if (firestoreService != null)
            {
                await firestoreService.SavePublishedExamAsync(_exam);
                ChangesSaved = true;
                MessageBox.Show("Schedule updated successfully!", "Success", 
                    MessageBoxButton.OK, MessageBoxImage.Information);
                Close();
            }
        }
        catch (Exception ex)
        {
            MessageBox.Show($"Failed to save schedule: {ex.Message}", "Error", 
                MessageBoxButton.OK, MessageBoxImage.Error);
        }
    }

    private void Cancel_Click(object sender, RoutedEventArgs e)
    {
        Close();
    }
}
