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
        public static SupabaseAuthService? SupabaseAuth { get; set; }

        protected override void OnStartup(StartupEventArgs e)
        {
            base.OnStartup(e);

            try
            {
                // Show authentication window first
                var authWindow = new Views.AuthWindow();
                authWindow.Show();
            }
            catch (Exception ex)
            {
                MessageBox.Show($"Failed to initialize application: {ex.Message}", 
                    "Initialization Error", MessageBoxButton.OK, MessageBoxImage.Error);
                Shutdown();
            }
        }

        /// <summary>
        /// Initialize publishing service using appsettings.json values.
        /// </summary>
        public static void ConfigurePublishingService(FirestoreService firestoreService)
        {
            try
            {
                var configuration = new ConfigurationBuilder()
                    .SetBasePath(AppDomain.CurrentDomain.BaseDirectory)
                    .AddJsonFile("appsettings.json", optional: true, reloadOnChange: true)
                    .Build();

                var firebaseSettings = configuration.GetSection("Firebase");
                var hostingUrl = firebaseSettings["HostingUrl"] ?? string.Empty;
                var apiEndpoint = firebaseSettings["ApiEndpoint"] ?? string.Empty;
                var publishingServerUrl = firebaseSettings["PublishingServerUrl"] ?? string.Empty;
                var signalRHubUrl = firebaseSettings["SignalRHubUrl"] ?? publishingServerUrl;

                PublishingService = new ExamPublishingService(
                    firestoreService,
                    hostingUrl,
                    apiEndpoint,
                    publishingServerUrl,
                    signalRHubUrl);
            }
            catch (Exception ex)
            {
                MessageBox.Show($"Failed to configure publishing service: {ex.Message}", "Error", MessageBoxButton.OK, MessageBoxImage.Error);
            }
        }

        /// <summary>
        /// Reset all services on logout
        /// </summary>
        public static void ResetServices()
        {
            FirestoreService = null;
            PublishingService = null;
            SupabaseAuth = null;
        }
    }
}
