using System;
using System.Windows;

namespace ExamForge.Views
{
    public partial class AuthWindow : Window
    {
        private LoginUserControl? _loginControl;
        private SignUpUserControl? _signUpControl;
        private AccountRecoveryUserControl? _recoveryControl;

        public AuthWindow()
        {
            InitializeComponent();
            ShowLogin();
        }

        private void ShowLogin()
        {
            _loginControl = new LoginUserControl();
            _loginControl.NavigateToSignUp += (s, e) => ShowSignUp();
            _loginControl.NavigateToForgotPassword += (s, e) => ShowAccountRecovery();
            _loginControl.LoginSuccessful += (s, e) => OnAuthenticationSuccessful();
            
            ContentHost.Content = _loginControl;
        }

        private void ShowSignUp()
        {
            _signUpControl = new SignUpUserControl();
            _signUpControl.NavigateToLogin += (s, e) => ShowLogin();
            _signUpControl.SignUpSuccessful += (s, e) => OnAuthenticationSuccessful();
            
            ContentHost.Content = _signUpControl;
        }

        private void ShowAccountRecovery()
        {
            _recoveryControl = new AccountRecoveryUserControl();
            _recoveryControl.NavigateToLogin += (s, e) => ShowLogin();
            
            ContentHost.Content = _recoveryControl;
        }

        private void OnAuthenticationSuccessful()
        {
            // Open main window
            var mainWindow = new MainWindow();
            mainWindow.Show();
            
            // Close auth window
            this.Close();
        }

        private void TitleBar_MouseLeftButtonDown(object sender, System.Windows.Input.MouseButtonEventArgs e)
        {
            if (e.ClickCount == 1)
                this.DragMove();
        }

        private void Close_Click(object sender, RoutedEventArgs e)
        {
            Application.Current.Shutdown();
        }
    }
}
