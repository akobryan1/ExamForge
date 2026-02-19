using System.Windows;
using System.Windows.Controls;
using ExamForge.Models;

namespace ExamForge.Views;

public partial class ReviewExamControl : UserControl
{
    private ExamReviewData? _currentExamData;
    
    public event EventHandler? PublishRequested;
    public event EventHandler? CancelRequested;

    public ReviewExamControl()
    {
        InitializeComponent();
    }

    public void LoadExamData(ExamReviewData reviewData)
    {
        _currentExamData = reviewData;
        TitleTextBlock.Text = $"Title: {reviewData.Title}";
        StructureItemsControl.ItemsSource = reviewData.Structures;
        ContentItemsControl.ItemsSource = reviewData.Contents;
        LiveExamTimeTextBlock.Text = $"Live Exam Time: {reviewData.StartTime:yyyy-MM-dd HH:mm} - {reviewData.EndTime:yyyy-MM-dd HH:mm}";
        DurationTextBlock.Text = $"Exam Duration: {reviewData.ExamDuration} minutes";
    }

    private async void PublishButton_Click(object sender, RoutedEventArgs e)
    {
        if (_currentExamData == null)
        {
            MessageBox.Show("No exam data loaded.", "Error", MessageBoxButton.OK, MessageBoxImage.Error);
            return;
        }

        // Disable button to prevent double-click
        PublishButton.IsEnabled = false;
        PublishButton.Content = "Publishing...";

        // ✅ Show loading dialog
        var loadingDialog = new LoadingDialog();
        var parentWindow = Window.GetWindow(this);
        if (parentWindow != null)
        {
            loadingDialog.Owner = parentWindow;
        }
        loadingDialog.Show();

        try
        {
            var publishingService = App.PublishingService;
            if (publishingService == null)
            {
                MessageBox.Show("Publishing service not initialized.", "Error", MessageBoxButton.OK, MessageBoxImage.Error);
                return;
            }

            // Update status in background
            loadingDialog.UpdateStatus("Saving exam to Firestore...");
            await Task.Delay(500); // Brief pause for UX

            var (htmlContent, examId, examUrl) = await Task.Run(async () =>
            {
                return await publishingService.PublishExamAsync(_currentExamData);
            });

            // Close loading dialog
            loadingDialog.Close();

            // Show success message
            var result = MessageBox.Show(
                $"Exam published successfully!\n\n" +
                $"Exam ID: {examId}\n" +
                $"URL: {examUrl}\n\n" +
                $"Would you like to copy the URL to clipboard?",
                "Success",
                MessageBoxButton.YesNo,
                MessageBoxImage.Information);

            if (result == MessageBoxResult.Yes)
            {
                Clipboard.SetText(examUrl);
                MessageBox.Show("URL copied to clipboard!", "Success", MessageBoxButton.OK, MessageBoxImage.Information);
            }

            PublishRequested?.Invoke(this, EventArgs.Empty);
        }
        catch (Exception ex)
        {
            loadingDialog.Close();
            MessageBox.Show(
                $"Failed to publish exam: {ex.Message}",
                "Error",
                MessageBoxButton.OK,
                MessageBoxImage.Error);
        }
        finally
        {
            PublishButton.IsEnabled = true;
            PublishButton.Content = "Publish Exam";
        }
    }

    private void CancelButton_Click(object sender, RoutedEventArgs e)
    {
        CancelRequested?.Invoke(this, EventArgs.Empty);
    }
}