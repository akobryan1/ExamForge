using Microsoft.Extensions.Configuration;
using System;
using System.IO;
using System.Windows;
using ExamForge.Services;

namespace ExamForge
{
    public partial class App : Application
    {
        public static FirestoreService? FirestoreService { get; private set; }
        public static ExamPublishingService? PublishingService { get; private set; }

        protected override void OnStartup(StartupEventArgs e)
        {
            base.OnStartup(e);

            try
            {
                // Load appsettings.json
                var configuration = new ConfigurationBuilder()
                    .SetBasePath(AppDomain.CurrentDomain.BaseDirectory)
                    .AddJsonFile("appsettings.json", optional: false, reloadOnChange: true)
                    .Build();

                var firebaseSettings = configuration.GetSection("Firebase");
                var credentialsPath = firebaseSettings["CredentialsPath"];
                var projectId = firebaseSettings["ProjectId"];
                var hostingUrl = firebaseSettings["HostingUrl"];
                var apiEndpoint = firebaseSettings["ApiEndpoint"];
                var publishingServerUrl = firebaseSettings["PublishingServerUrl"];

                if (string.IsNullOrEmpty(credentialsPath) || !File.Exists(credentialsPath))
                {
                    MessageBox.Show($"Firebase credentials file not found at: {credentialsPath}", 
                        "Configuration Error", MessageBoxButton.OK, MessageBoxImage.Error);
                    Shutdown();
                    return;
                }

                // ✅ FIX: Use GetInstance() instead of constructor
                FirestoreService = Services.FirestoreService.GetInstance(projectId ?? "", credentialsPath);
                PublishingService = new ExamPublishingService(
                    FirestoreService, 
                    hostingUrl ?? "", 
                    apiEndpoint ?? "",
                    publishingServerUrl ?? ""
                );
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
