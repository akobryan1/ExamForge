using iText.Kernel.Pdf;
using iText.Layout;
using iText.Layout.Element;
using iText.Layout.Properties;
using iText.Kernel.Colors;
using iText.Kernel.Font;
using iText.IO.Font.Constants;
using System;
using System.IO;
using System.Linq;
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
                var filePath = BuildExportFilePath("StudentReport", studentName);

                using (var stream = new FileStream(filePath, FileMode.Create, FileAccess.Write, FileShare.None))
                using (var writer = new PdfWriter(stream))
                using (var pdf = new PdfDocument(writer))
                {
                    var document = new Document(pdf);
                    var boldFont = PdfFontFactory.CreateFont(StandardFonts.HELVETICA_BOLD);
                    var regularFont = PdfFontFactory.CreateFont(StandardFonts.HELVETICA);

                    // Title
                    var title = new Paragraph("STUDENT PERFORMANCE REPORT")
                        .SetFont(boldFont)
                        .SetFontSize(20)
                        .SetTextAlignment(TextAlignment.CENTER)
                        .SetMarginBottom(20);
                    document.Add(title);

                    // Student Info Section
                    var infoTable = new Table(2);
                    infoTable.AddCell(new Cell().Add(new Paragraph("Student Name:").SetFont(boldFont)));
                    infoTable.AddCell(new Cell().Add(new Paragraph(studentName)));
                    infoTable.AddCell(new Cell().Add(new Paragraph("Student ID:").SetFont(boldFont)));
                    infoTable.AddCell(new Cell().Add(new Paragraph(submission.StudentId)));
                    infoTable.AddCell(new Cell().Add(new Paragraph("Exam:").SetFont(boldFont)));
                    infoTable.AddCell(new Cell().Add(new Paragraph(examTitle)));
                    infoTable.AddCell(new Cell().Add(new Paragraph("Submitted:").SetFont(boldFont)));
                    infoTable.AddCell(new Cell().Add(new Paragraph(submission.SubmittedAt.ToString("MMM dd, yyyy h:mm tt"))));
                    document.Add(infoTable);

                    // Score Summary
                    var percentage = submission.TotalPossiblePoints > 0
                        ? (submission.TotalScore / submission.TotalPossiblePoints) * 100
                        : 0;

                    document.Add(new Paragraph($"\nTotal Score: {submission.TotalScore:F1}/{submission.TotalPossiblePoints:F1}")
                        .SetFont(boldFont)
                        .SetFontSize(16)
                        .SetMarginTop(15));
                    document.Add(new Paragraph($"Percentage: {percentage:F1}%")
                        .SetFont(boldFont)
                        .SetFontSize(16));
                    
                    var statusText = percentage >= 60 ? "PASSED" : "FAILED";
                    var statusColor = percentage >= 60 ? ColorConstants.GREEN : ColorConstants.RED;
                    document.Add(new Paragraph($"Status: {statusText}")
                        .SetFont(boldFont)
                        .SetFontSize(14)
                        .SetFontColor(statusColor));

                    // Question-by-Question Analysis
                    document.Add(new Paragraph("\nDETAILED QUESTION ANALYSIS")
                        .SetFont(boldFont)
                        .SetFontSize(14)
                        .SetMarginTop(20));

                    int questionNum = 1;
                    foreach (var content in exam.Contents)
                    {
                        var response = submission.Responses.FirstOrDefault(r => r.QuestionId == content.ContentId);

                        document.Add(new Paragraph($"\nQuestion {questionNum}")
                            .SetFont(boldFont)
                            .SetFontSize(12)
                            .SetMarginTop(10));
                        document.Add(new Paragraph(content.Question)
                            .SetFontSize(11));
                        document.Add(new Paragraph($"Your Answer: {response?.Answer ?? "(No answer)"}")
                            .SetFontSize(10));
                        document.Add(new Paragraph($"Correct Answer: {content.Answer}")
                            .SetFont(boldFont)
                            .SetFontSize(10));
                        
                        var pointsText = $"Points: {response?.PointsEarned ?? 0}/{content.Points}";
                        var pointsColor = response?.IsCorrect == true ? ColorConstants.GREEN : ColorConstants.RED;
                        document.Add(new Paragraph(pointsText)
                            .SetFontSize(10)
                            .SetFontColor(pointsColor));

                        questionNum++;
                    }

                    document.Close();
                }

                return filePath;
            });
        }

        public async Task<string> ExportGradebookToPdfAsync(
            string examId,
            string examTitle,
            System.Collections.Generic.List<ExamSubmission> submissions)
        {
            return await Task.Run(() =>
            {
                var filePath = BuildExportFilePath("Gradebook", examTitle);

                using (var stream = new FileStream(filePath, FileMode.Create, FileAccess.Write, FileShare.None))
                using (var writer = new PdfWriter(stream))
                using (var pdf = new PdfDocument(writer))
                {
                    var document = new Document(pdf);
                    var boldFont = PdfFontFactory.CreateFont(StandardFonts.HELVETICA_BOLD);

                    // Title
                    var title = new Paragraph($"GRADEBOOK: {examTitle}")
                        .SetFont(boldFont)
                        .SetFontSize(18)
                        .SetTextAlignment(TextAlignment.CENTER)
                        .SetMarginBottom(20);
                    document.Add(title);

                    // Gradebook table
                    var table = new Table(new float[] { 3, 2, 2, 2, 2 });
                    table.SetWidth(UnitValue.CreatePercentValue(100));

                    // Headers
                    table.AddHeaderCell(new Cell().Add(new Paragraph("Student Name").SetFont(boldFont)));
                    table.AddHeaderCell(new Cell().Add(new Paragraph("Student ID").SetFont(boldFont)));
                    table.AddHeaderCell(new Cell().Add(new Paragraph("Score").SetFont(boldFont)));
                    table.AddHeaderCell(new Cell().Add(new Paragraph("Percentage").SetFont(boldFont)));
                    table.AddHeaderCell(new Cell().Add(new Paragraph("Grade").SetFont(boldFont)));

                    foreach (var submission in submissions.OrderBy(s => s.StudentName))
                    {
                        var percentage = submission.TotalPossiblePoints > 0
                            ? (submission.TotalScore / submission.TotalPossiblePoints) * 100
                            : 0;
                        var grade = GetLetterGrade(percentage);

                        table.AddCell(new Cell().Add(new Paragraph(submission.StudentName)));
                        table.AddCell(new Cell().Add(new Paragraph(submission.StudentId)));
                        table.AddCell(new Cell().Add(new Paragraph($"{submission.TotalScore:F1}/{submission.TotalPossiblePoints:F1}")));
                        table.AddCell(new Cell().Add(new Paragraph($"{percentage:F1}%")));
                        table.AddCell(new Cell().Add(new Paragraph(grade)));
                    }

                    document.Add(table);

                    // Statistics
                    var avgScore = submissions.Average(s => 
                        s.TotalPossiblePoints > 0 ? (s.TotalScore / s.TotalPossiblePoints) * 100 : 0);
                    var passRate = submissions.Count(s => 
                        s.TotalPossiblePoints > 0 && (s.TotalScore / s.TotalPossiblePoints) * 100 >= 60) * 100.0 / submissions.Count;

                    document.Add(new Paragraph($"\nClass Statistics:")
                        .SetFont(boldFont)
                        .SetFontSize(14)
                        .SetMarginTop(20));
                    document.Add(new Paragraph($"Average Score: {avgScore:F1}%"));
                    document.Add(new Paragraph($"Pass Rate: {passRate:F1}%"));
                    document.Add(new Paragraph($"Total Students: {submissions.Count}"));

                    document.Close();
                }

                return filePath;
            });
        }

        private string GetLetterGrade(double percentage)
        {
            return percentage switch
            {
                >= 90 => "A",
                >= 80 => "B",
                >= 70 => "C",
                >= 60 => "D",
                _ => "F"
            };
        }

        private static string BuildExportFilePath(string prefix, string title)
        {
            var timestamp = DateTime.Now.ToString("yyyyMMdd_HHmmss");
            var safeTitle = SanitizeFileSegment(title);

            var documentsPath = Environment.GetFolderPath(Environment.SpecialFolder.MyDocuments);
            if (string.IsNullOrWhiteSpace(documentsPath) || !Directory.Exists(documentsPath))
            {
                documentsPath = Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData);
            }

            var exportPath = Path.Combine(documentsPath, "ExamForge_Exports");
            Directory.CreateDirectory(exportPath);

            var fileName = $"{prefix}_{safeTitle}_{timestamp}.pdf";
            var filePath = Path.Combine(exportPath, fileName);

            var dir = Path.GetDirectoryName(filePath);
            if (!string.IsNullOrWhiteSpace(dir))
            {
                Directory.CreateDirectory(dir);
            }

            return filePath;
        }

        private static string SanitizeFileSegment(string value)
        {
            if (string.IsNullOrWhiteSpace(value)) return "Untitled";

            var invalid = Path.GetInvalidFileNameChars();
            var cleaned = new string(value
                .Trim()
                .Select(ch => invalid.Contains(ch) ? '_' : ch)
                .ToArray());

            cleaned = cleaned.Replace(' ', '_');
            return string.IsNullOrWhiteSpace(cleaned) ? "Untitled" : cleaned;
        }
    }
}
