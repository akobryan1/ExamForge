using System;
using System.Windows;
using System.Windows.Controls;
using ExamForge.Models;

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
            // No checkboxes to disable anymore
        }

        private void AllowGuests_Checked(object sender, RoutedEventArgs e)
        {
            // No checkboxes to enable anymore
        }

        private void AllowGuests_Unchecked(object sender, RoutedEventArgs e)
        {
            // No checkboxes to disable anymore
        }

        public object SaveState()
        {
            return new LoginConfigState
            {
                IsGoogleSignIn = google_signin_radiobutton.IsChecked == true,
                // ✅ When "Allow as Guests" is selected, all fields are required by default
                RequireFullName = allow_guests_radiobutton.IsChecked == true,
                RequireYearSection = allow_guests_radiobutton.IsChecked == true,
                RequireStudentNumber = allow_guests_radiobutton.IsChecked == true
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
            }
        }
    }
}
