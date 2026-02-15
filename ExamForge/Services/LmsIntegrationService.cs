using System;
using System.Collections.Generic;
using System.Linq;
using System.Net.Http;
using System.Net.Http.Headers;
using System.Text;
using System.Text.Json;
using System.Threading.Tasks;
using ExamForge.Models;

namespace ExamForge.Services
{
    public class LmsIntegrationService
    {
        private readonly HttpClient _httpClient;

        public LmsIntegrationService()
        {
            _httpClient = new HttpClient();
        }

        // Canvas LMS Integration
        public async Task<bool> SyncToCanvasAsync(
            string canvasApiUrl,
            string accessToken,
            string courseId,
            ExamSubmission submission)
        {
            try
            {
                _httpClient.DefaultRequestHeaders.Authorization =
                    new AuthenticationHeaderValue("Bearer", accessToken);

                var submissionPayload = new
                {
                    submission = new
                    {
                        user_id = submission.StudentId,
                        submission_type = "online_text_entry",
                        body = $"ExamForge Submission - Score: {submission.TotalScore}/{submission.TotalPossiblePoints}",
                        score = submission.TotalScore,
                        grade = CalculateLetterGrade(submission)
                    }
                };

                var json = JsonSerializer.Serialize(submissionPayload);
                var content = new StringContent(json, Encoding.UTF8, "application/json");

                var response = await _httpClient.PostAsync(
                    $"{canvasApiUrl}/api/v1/courses/{courseId}/assignments/{submission.ExamId}/submissions",
                    content);

                return response.IsSuccessStatusCode;
            }
            catch (Exception ex)
            {
                System.Diagnostics.Debug.WriteLine($"Canvas sync failed: {ex.Message}");
                return false;
            }
        }

        // Blackboard Learn Integration
        public async Task<bool> SyncToBlackboardAsync(
            string blackboardUrl,
            string accessToken,
            string courseId,
            ExamSubmission submission)
        {
            try
            {
                _httpClient.DefaultRequestHeaders.Authorization =
                    new AuthenticationHeaderValue("Bearer", accessToken);

                var gradePayload = new
                {
                    text = CalculateLetterGrade(submission),
                    score = submission.TotalScore,
                    notes = "Grade synced from ExamForge"
                };

                var json = JsonSerializer.Serialize(gradePayload);
                var content = new StringContent(json, Encoding.UTF8, "application/json");

                var response = await _httpClient.PatchAsync(
                    $"{blackboardUrl}/learn/api/public/v1/courses/{courseId}/gradebook/columns/{submission.ExamId}/users/{submission.StudentId}",
                    content);

                return response.IsSuccessStatusCode;
            }
            catch (Exception ex)
            {
                System.Diagnostics.Debug.WriteLine($"Blackboard sync failed: {ex.Message}");
                return false;
            }
        }

        // Moodle Integration
        public async Task<bool> SyncToMoodleAsync(
            string moodleUrl,
            string token,
            string courseId,
            ExamSubmission submission)
        {
            try
            {
                var requestUrl = $"{moodleUrl}/webservice/rest/server.php?wstoken={token}" +
                    $"&wsfunction=mod_assign_save_grade" +
                    $"&assignmentid={submission.ExamId}" +
                    $"&userid={submission.StudentId}" +
                    $"&grade={submission.TotalScore}" +
                    $"&moodlewsrestformat=json";

                var response = await _httpClient.PostAsync(requestUrl, null);
                return response.IsSuccessStatusCode;
            }
            catch (Exception ex)
            {
                System.Diagnostics.Debug.WriteLine($"Moodle sync failed: {ex.Message}");
                return false;
            }
        }

        // Export gradebook in Common Cartridge format
        public async Task<string> ExportCommonCartridgeAsync(
            string examId,
            string examTitle,
            List<ExamSubmission> submissions)
        {
            try
            {
                var timestamp = DateTime.Now.ToString("yyyyMMdd_HHmmss");
                var filename = $"CommonCartridge_{examTitle.Replace(" ", "_")}_{timestamp}.xml";
                var documentsPath = Environment.GetFolderPath(Environment.SpecialFolder.MyDocuments);
                var exportPath = System.IO.Path.Combine(documentsPath, "ExamForge_Exports");
                System.IO.Directory.CreateDirectory(exportPath);
                var filePath = System.IO.Path.Combine(exportPath, filename);

                var xml = GenerateCommonCartridgeXml(examId, examTitle, submissions);
                await System.IO.File.WriteAllTextAsync(filePath, xml);

                return filePath;
            }
            catch (Exception ex)
            {
                throw new InvalidOperationException($"Failed to export Common Cartridge: {ex.Message}", ex);
            }
        }

        // Import roster from LMS
        public async Task<List<StudentRoster>> ImportRosterFromCanvasAsync(
            string canvasApiUrl,
            string accessToken,
            string courseId)
        {
            try
            {
                _httpClient.DefaultRequestHeaders.Authorization =
                    new AuthenticationHeaderValue("Bearer", accessToken);

                var response = await _httpClient.GetAsync(
                    $"{canvasApiUrl}/api/v1/courses/{courseId}/students");

                if (response.IsSuccessStatusCode)
                {
                    var json = await response.Content.ReadAsStringAsync();
                    var students = JsonSerializer.Deserialize<List<CanvasStudent>>(json);

                    return students?.Select(s => new StudentRoster
                    {
                        StudentId = s.id.ToString(),
                        Name = s.name,
                        Email = s.email,
                        SisId = s.sis_user_id
                    }).ToList() ?? new List<StudentRoster>();
                }

                return new List<StudentRoster>();
            }
            catch (Exception ex)
            {
                System.Diagnostics.Debug.WriteLine($"Roster import failed: {ex.Message}");
                return new List<StudentRoster>();
            }
        }

        private string CalculateLetterGrade(ExamSubmission submission)
        {
            if (submission.TotalPossiblePoints == 0) return "F";

            var percentage = (submission.TotalScore / submission.TotalPossiblePoints) * 100;

            return percentage switch
            {
                >= 90 => "A",
                >= 80 => "B",
                >= 70 => "C",
                >= 60 => "D",
                _ => "F"
            };
        }

        private string GenerateCommonCartridgeXml(
            string examId,
            string examTitle,
            List<ExamSubmission> submissions)
        {
            var xml = new StringBuilder();
            xml.AppendLine("<?xml version=\"1.0\" encoding=\"UTF-8\"?>");
            xml.AppendLine("<manifest identifier=\"ExamForge_" + examId + "\">");
            xml.AppendLine("  <metadata>");
            xml.AppendLine($"    <schema>IMS Common Cartridge</schema>");
            xml.AppendLine($"    <schemaversion>1.3.0</schemaversion>");
            xml.AppendLine($"  </metadata>");
            xml.AppendLine("  <organizations>");
            xml.AppendLine($"    <organization identifier=\"{examId}\">");
            xml.AppendLine($"      <item identifier=\"{examId}_item\" title=\"{examTitle}\">");

            foreach (var submission in submissions)
            {
                xml.AppendLine($"        <item identifier=\"{submission.Id}\">");
                xml.AppendLine($"          <title>{submission.StudentName}</title>");
                xml.AppendLine($"          <gradebook>");
                xml.AppendLine($"            <score>{submission.TotalScore}</score>");
                xml.AppendLine($"            <possible>{submission.TotalPossiblePoints}</possible>");
                xml.AppendLine($"          </gradebook>");
                xml.AppendLine($"        </item>");
            }

            xml.AppendLine("      </item>");
            xml.AppendLine("    </organization>");
            xml.AppendLine("  </organizations>");
            xml.AppendLine("</manifest>");

            return xml.ToString();
        }
    }

    // Supporting classes
    public class StudentRoster
    {
        public string StudentId { get; set; } = "";
        public string Name { get; set; } = "";
        public string Email { get; set; } = "";
        public string SisId { get; set; } = "";
    }

    public class CanvasStudent
    {
        public int id { get; set; }
        public string name { get; set; } = "";
        public string email { get; set; } = "";
        public string sis_user_id { get; set; } = "";
    }
}
