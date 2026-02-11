using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using ExamForge.Models;

namespace ExamForge.Services
{
    public class ExportService
    {
        private readonly FirestoreService _firestoreService;

        public ExportService()
        {
            _firestoreService = App.FirestoreService ?? throw new InvalidOperationException("Firestore service not initialized");
        }

        /// <summary>
        /// Export grades to Excel format
        /// </summary>
        public async Task<string> ExportGradesToExcelAsync(string examId, string examTitle)
        {
            try
            {
                // Get exam submissions
                var submissions = await _firestoreService.GetExamSubmissionsAsync(examId);
                if (!submissions.Any())
                {
                    throw new InvalidOperationException("No submissions found for this exam.");
                }

                // Create export directory if it doesn't exist
                var exportsDir = Path.Combine(
                    Environment.GetFolderPath(Environment.SpecialFolder.MyDocuments),
                    "ExamForge_Exports"
                );
                Directory.CreateDirectory(exportsDir);

                // Generate filename with timestamp
                var fileName = $"Grades_{examTitle}_{DateTime.Now:yyyyMMdd_HHmmss}.csv";
                var filePath = Path.Combine(exportsDir, fileName);

                // Create CSV content
                var csv = new List<string>
        {
            "Student Name,Student ID,Score,Percentage,Grade,Submission Time,Time Taken"
        };

                foreach (var submission in submissions.OrderBy(s => s.StudentName))
                {
                    var percentage = submission.TotalPossiblePoints > 0
                        ? Math.Round((double)submission.TotalScore / submission.TotalPossiblePoints * 100, 1)
                        : 0;

                    var grade = GetLetterGrade(percentage);

                    var timeTaken =
                        submission.StartTime.HasValue &&
                        submission.EndTime.HasValue &&
                        submission.EndTime > submission.StartTime
                            ? (submission.EndTime.Value - submission.StartTime.Value)
                                .ToString(@"hh\:mm\:ss")
                        : "N/A";


                    csv.Add(
                        $"\"{submission.StudentName}\"," +
                        $"\"{submission.StudentId}\"," +
                        $"{submission.TotalScore}," +
                        $"{percentage}%," +
                        $"{grade}," +
                        $"\"{submission.SubmittedAt:yyyy-MM-dd HH:mm:ss}\"," +
                        $"{timeTaken}"
                    );
                }

                // Write to file
                await File.WriteAllLinesAsync(filePath, csv);
                return filePath;
            }
            catch (Exception ex)
            {
                throw new InvalidOperationException($"Failed to export grades: {ex.Message}", ex);
            }
        }


        /// <summary>
        /// Export detailed submissions report
        /// </summary>
        public async Task<string> ExportSubmissionsAsync(string examId, string examTitle)
        {
            try
            {
                // Get exam and submissions
                var exam = await _firestoreService.GetPublishedExamAsync(examId);
                var submissions = await _firestoreService.GetExamSubmissionsAsync(examId);
                
                if (!submissions.Any())
                {
                    throw new InvalidOperationException("No submissions found for this exam.");
                }

                // Create export directory
                var exportsDir = Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.MyDocuments), "ExamForge_Exports");
                Directory.CreateDirectory(exportsDir);

                // Generate filename with timestamp
                var fileName = $"Submissions_{examTitle}_{DateTime.Now:yyyyMMdd_HHmmss}.csv";
                var filePath = Path.Combine(exportsDir, fileName);

                // Create detailed CSV content
                var csv = new List<string>
                {
                    "Student Name,Student ID,Question Number,Question Type,Question Text,Student Answer,Correct Answer,Points Earned,Points Possible,Is Correct"
                };

                foreach (var submission in submissions.OrderBy(s => s.StudentName))
                {
                    if (submission.Responses != null)
                    {
                        foreach (var response in submission.Responses)
                        {
                            var question = exam?.Contents.FirstOrDefault(q => q.Id == response.QuestionId);
                            var questionText = question?.QuestionText?.Replace("\"", "\"\"") ?? "Unknown";
                            var correctAnswer = question?.CorrectAnswer?.Replace("\"", "\"\"") ?? "Unknown";
                            var studentAnswer = response.Answer?.Replace("\"", "\"\"") ?? "No Answer";

                            csv.Add($"\"{submission.StudentName}\",\"{submission.StudentId}\",{response.QuestionNumber},{question?.QuestionType ?? "Unknown"},\"{questionText}\",\"{studentAnswer}\",\"{correctAnswer}\",{response.PointsEarned},{response.PointsPossible},{response.IsCorrect}");
                        }
                    }
                }

                // Write to file
                await File.WriteAllLinesAsync(filePath, csv);
                return filePath;
            }
            catch (Exception ex)
            {
                throw new InvalidOperationException($"Failed to export submissions: {ex.Message}", ex);
            }
        }

        /// <summary>
        /// Get letter grade based on percentage
        /// </summary>
        private static string GetLetterGrade(double percentage)
        {
            return percentage switch
            {
                >= 97 => "A+",
                >= 93 => "A",
                >= 90 => "A-",
                >= 87 => "B+",
                >= 83 => "B",
                >= 80 => "B-",
                >= 77 => "C+",
                >= 73 => "C",
                >= 70 => "C-",
                >= 67 => "D+",
                >= 63 => "D",
                >= 60 => "D-",
                _ => "F"
            };
        }

        /// <summary>
        /// Export academic integrity report
        /// </summary>
        public async Task<string> ExportIntegrityReportAsync(string examId, string examTitle)
        {
            try
            {
                var incidents = await _firestoreService.GetIntegrityIncidentsAsync(examId);
                
                var exportsDir = Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.MyDocuments), "ExamForge_Exports");
                Directory.CreateDirectory(exportsDir);

                var fileName = $"Integrity_Report_{examTitle}_{DateTime.Now:yyyyMMdd_HHmmss}.csv";
                var filePath = Path.Combine(exportsDir, fileName);

                var csv = new List<string>
                {
                    "Student Name,Student ID,Incident Type,Severity,Timestamp,Details,Status"
                };

                foreach (var incident in incidents.OrderBy(i => i.Timestamp))
                {
                    csv.Add($"\"{incident.StudentName}\",\"{incident.StudentId}\",{incident.IncidentType},{incident.Severity},\"{incident.Timestamp:yyyy-MM-dd HH:mm:ss}\",\"{incident.Details?.Replace("\"", "\"\"")}\",{incident.Status}");
                }

                await File.WriteAllLinesAsync(filePath, csv);
                return filePath;
            }
            catch (Exception ex)
            {
                throw new InvalidOperationException($"Failed to export integrity report: {ex.Message}", ex);
            }
        }

        /// <summary>
        /// Generic CSV export method
        /// </summary>
        public async Task<string> ExportToCsvAsync(List<Dictionary<string, string>> data, string fileName)
        {
            try
            {
                var exportsDir = Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.MyDocuments), "ExamForge_Exports");
                Directory.CreateDirectory(exportsDir);

                var filePath = Path.Combine(exportsDir, fileName);

                if (!data.Any())
                {
                    await File.WriteAllTextAsync(filePath, "No data available");
                    return filePath;
                }

                var headers = data.First().Keys.ToList();
                var csv = new StringBuilder();

                // Add header row
                csv.AppendLine(string.Join(",", headers.Select(h => $"\"{h}\"")));

                // Add data rows
                foreach (var row in data)
                {
                    var values = headers.Select(h => $"\"{row.GetValueOrDefault(h, "")?.Replace("\"", "\"\"")}\"");
                    csv.AppendLine(string.Join(",", values));
                }

                await File.WriteAllTextAsync(filePath, csv.ToString());
                return filePath;
            }
            catch (Exception ex)
            {
                throw new InvalidOperationException($"Failed to export CSV: {ex.Message}", ex);
            }
        }
    }
}