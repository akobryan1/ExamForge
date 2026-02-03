using System;
using System.Windows;
using System.Windows.Controls;
using ExamForge.Models; // Add this line

namespace ExamForge
{
    public partial class examinee_login_usercontrol : UserControl
    {
        public examinee_login_usercontrol()
        {
            InitializeComponent();
            google_signin_radiobutton.IsChecked = true;
        }

        private void GoogleSignIn_Checked(object sender, RoutedEventArgs e)
        {
            fullname_checkbox.IsEnabled = false;
            year_section_checkbox.IsEnabled = false;
            student_number_checkbox.IsEnabled = false;
        }

        private void AllowGuests_Checked(object sender, RoutedEventArgs e)
        {
            fullname_checkbox.IsEnabled = true;
            year_section_checkbox.IsEnabled = true;
            student_number_checkbox.IsEnabled = true;
        }

        private void AllowGuests_Unchecked(object sender, RoutedEventArgs e)
        {
            fullname_checkbox.IsEnabled = false;
            year_section_checkbox.IsEnabled = false;
            student_number_checkbox.IsEnabled = false;
        }

        public object SaveState()
        {
            return new LoginConfigState
            {
                IsGoogleSignIn = google_signin_radiobutton.IsChecked == true,
                RequireFullName = fullname_checkbox.IsChecked == true,
                RequireYearSection = year_section_checkbox.IsChecked == true,
                RequireStudentNumber = student_number_checkbox.IsChecked == true
            };
        }

        public void RestoreState(object state)
        {
            if (state is LoginConfigState savedState)
            {
                if (savedState.IsGoogleSignIn)
                {
                    google_signin_radiobutton.IsChecked = true;
                }
                else
                {
                    allow_guests_radiobutton.IsChecked = true;
                }
                
                fullname_checkbox.IsChecked = savedState.RequireFullName;
                year_section_checkbox.IsChecked = savedState.RequireYearSection;
                student_number_checkbox.IsChecked = savedState.RequireStudentNumber;
            }
        }
    }

    // ❌ REMOVE THIS - It's now in ExamReviewData.cs:
    // public class LoginConfigState { ... }
}
