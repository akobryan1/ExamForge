# PDF Export Enhancement Implementation Guide

## Step 1: Install iText7 NuGet Package
```bash
dotnet add package iText7
```

## Step 2: Create PDF Export Service

Add to `ExamForge/Services/PdfExportService.cs`:

```csharp
using iText.Kernel.Pdf;
using iText.Layout;
using iText.Layout.Element;
using iText.Layout.Properties;
using iText.Kernel.Colors;
using System;
using System.IO;
using System.Threading.Tasks;
using ExamForge.Models;

namespace ExamForge.Services
{
    public class PdfExportService
    {
        public async Task<string> ExportStudentReportToPdfAsync(
            string studentName,
            string examTitle,
            ExamSubmission submission,
            PublishedExam exam)
        {
            return await Task.Run(() =>
            {
                var timestamp = DateTime.Now.ToString("yyyyMMdd_HHmmss");
                var filename = $"StudentReport_{studentName.Replace(" ", "_")}_{timestamp}.pdf";
                var documentsPath = Environment.GetFolderPath(Environment.SpecialFolder.MyDocuments);
                var exportPath = Path.Combine(documentsPath, "ExamForge_Exports");
                Directory.CreateDirectory(exportPath);
                var filePath = Path.Combine(exportPath, filename);

                using (var writer = new PdfWriter(filePath))
                using (var pdf = new PdfDocument(writer))
                {
                    var document = new Document(pdf);

                    // Title
                    var title = new Paragraph("STUDENT PERFORMANCE REPORT")
                        .SetFontSize(20)
                        .SetBold()
                        .SetTextAlignment(TextAlignment.CENTER);
                    document.Add(title);

                    // Student Info
                    document.Add(new Paragraph($"Student: {studentName}").SetBold());
                    document.Add(new Paragraph($"Student ID: {submission.StudentId}"));
                    document.Add(new Paragraph($"Exam: {examTitle}").SetBold());
                    document.Add(new Paragraph($"Submitted: {submission.SubmittedAt:MMM dd, yyyy h:mm tt}"));

                    // Score Summary
                    var percentage = submission.TotalPossiblePoints > 0 
                        ? (submission.TotalScore / submission.TotalPossiblePoints) * 100 
                        : 0;

                    document.Add(new Paragraph($"\nTotal Score: {submission.TotalScore:F1}/{submission.TotalPossiblePoints:F1}")
                        .SetFontSize(16)
                        .SetBold());
                    document.Add(new Paragraph($"Percentage: {percentage:F1}%")
                        .SetFontSize(16)
                        .SetBold());
                    document.Add(new Paragraph($"Status: {(percentage >= 60 ? "PASSED" : "FAILED")}")
                        .SetFontSize(14)
                        .SetBold()
                        .SetFontColor(percentage >= 60 ? ColorConstants.GREEN : ColorConstants.RED));

                    // Questions
                    document.Add(new Paragraph("\nDETAILED QUESTION ANALYSIS")
                        .SetFontSize(14)
                        .SetBold()
                        .SetMarginTop(20));

                    int questionNum = 1;
                    foreach (var content in exam.Contents)
                    {
                        var response = submission.Responses.FirstOrDefault(r => r.QuestionId == content.ContentId);
                        
                        document.Add(new Paragraph($"\nQuestion {questionNum}")
                            .SetFontSize(12)
                            .SetBold());
                        document.Add(new Paragraph(content.Question));
                        document.Add(new Paragraph($"Your Answer: {response?.Answer ?? "(No answer)"}")
                            .SetItalic());
                        document.Add(new Paragraph($"Correct Answer: {content.Answer}")
                            .SetBold());
                        document.Add(new Paragraph($"Points: {response?.PointsEarned ?? 0}/{content.Points}")
                            .SetFontColor(response?.IsCorrect == true ? ColorConstants.GREEN : ColorConstants.RED));
                        
                        questionNum++;
                    }

                    document.Close();
                }

                return filePath;
            });
        }

        public async Task<string> ExportGradebookToPdfAsync(string examId, string examTitle)
        {
            // Similar implementation for gradebook
            return await Task.Run(() => "gradebook.pdf");
        }
    }
}
```

## Step 3: Update ViewSubmissionDialog

Replace the ExportPdf_Click method:

```csharp
private async void ExportPdf_Click(object sender, RoutedEventArgs e)
{
    try
    {
        var pdfService = new Services.PdfExportService();
        var path = await pdfService.ExportStudentReportToPdfAsync(
            _submission.StudentName,
            _exam.Title,
            _submission,
            _exam);

        MessageBox.Show($"PDF report exported successfully!\n\nSaved to: {path}",
            "Export Complete", MessageBoxButton.OK, MessageBoxImage.Information);
    }
    catch (Exception ex)
    {
        MessageBox.Show($"Failed to export PDF: {ex.Message}", "Export Error",
            MessageBoxButton.OK, MessageBoxImage.Error);
    }
}
```

## Implementation Time: 2-4 hours
## Complexity: Medium
## Dependencies: iText7 NuGet package
