using Microsoft.Extensions.Configuration;
using System;
using System.IO;
using System.Windows;
using ExamForge.Services;

namespace ExamForge
{
    public partial class App : Application
    {
        public static FirestoreService? FirestoreService { get; set; }
        public static ExamPublishingService? PublishingService { get; private set; }
        public static FirebaseAuthService? AuthService { get; set; }
        public static BackendAuthService? BackendAuthService { get; set; }
        public static string? BackendJwt { get; set; }
        public static string? CurrentUserId { get; set; }
        public static string? CurrentUserEmail { get; set; }
        public static string? CurrentUserName { get; set; }

        protected override void OnStartup(StartupEventArgs e)
        {
            base.OnStartup(e);

            try
            {
                // Load appsettings.json for publishing service config
                var configuration = new ConfigurationBuilder()
                    .SetBasePath(AppDomain.CurrentDomain.BaseDirectory)
                    .AddJsonFile("appsettings.json", optional: false, reloadOnChange: true)
                    .Build();

                var firebaseSettings = configuration.GetSection("Firebase");
                var projectId = firebaseSettings["ProjectId"];
                var hostingUrl = firebaseSettings["HostingUrl"];
                var apiEndpoint = firebaseSettings["ApiEndpoint"];
                var publishingServerUrl = firebaseSettings["PublishingServerUrl"];

                // Store config for later use after login
                // FirestoreService will be initialized after authentication with user-scoped collections

                // Show login window first (authentication required before accessing any data)
                var loginWindow = new Views.LoginWindow();
                loginWindow.Show();
            }
            catch (Exception ex)
            {
                MessageBox.Show($"Failed to initialize application: {ex.Message}", 
                    "Initialization Error", MessageBoxButton.OK, MessageBoxImage.Error);
                Shutdown();
            }
        }
    }
}
