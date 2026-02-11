using System;
using System.Collections.Generic;
using System.Linq;
using System.Net.Http;
using System.Net.Http.Json;
using System.Text;
using System.Threading.Tasks;
using ExamForge.Models;

namespace ExamForge.Services;

public class ExamPublishingService
{
    private readonly FirestoreService _firestoreService;
    private readonly string _hostingUrl;
    private readonly string _apiEndpoint;
    private readonly string _publishingServerUrl;
    private readonly HttpClient _httpClient;

    public ExamPublishingService(FirestoreService firestoreService, string hostingUrl, string apiEndpoint, string publishingServerUrl)
    {
        _firestoreService = firestoreService;
        _hostingUrl = hostingUrl;
        _apiEndpoint = apiEndpoint;
        _publishingServerUrl = publishingServerUrl;
        _httpClient = new HttpClient { Timeout = TimeSpan.FromMinutes(2) };
    }

    public async Task<(string htmlContent, string examId, string examUrl)> PublishExamAsync(ExamReviewData examData, string creatorEmail = "admin@examforge.com")
    {
        var examId = Guid.NewGuid().ToString();
        
        try
        {
            System.Diagnostics.Debug.WriteLine($"📝 Starting exam publication: {examId}");
            
            // ✅ SECURITY FIX: Create sanitized exam data WITHOUT correct answers
            var sanitizedExamData = new ExamReviewData
            {
                Title = examData.Title,
                Structures = examData.Structures,
                Contents = examData.Contents.Select(c => new ExamContent
                {
                    QuestionText = c.QuestionText,
                    QuestionType = c.QuestionType,
                    Options = c.Options,
                    Points = c.Points,
                    IsComplete = c.IsComplete
                    // ✅ CorrectAnswer is deliberately excluded here
                }).ToList(),
                StartTime = examData.StartTime,
                EndTime = examData.EndTime,
                ExamDuration = examData.ExamDuration
            };
            
            // Generate HTML with sanitized data
            var htmlContent = GenerateExamHtml(sanitizedExamData, examId);
            System.Diagnostics.Debug.WriteLine("✅ HTML generated");
            
            // Save FULL exam data (including correct answers) to Firestore
            var publishedExam = new PublishedExam
            {
                Id = examId,
                Title = examData.Title,
                Structures = examData.Structures,
                Contents = examData.Contents,
                StartTime = examData.StartTime,
                EndTime = examData.EndTime,
                ExamDuration = examData.ExamDuration,
                PublishedDate = DateTime.UtcNow,
                CreatedBy = creatorEmail,
                ExamUrl = "",
                Status = "Active",
                LoginConfig = examData.LoginConfig?.ToString() ?? ""
            };
            
            await _firestoreService.SavePublishedExamAsync(publishedExam);
            System.Diagnostics.Debug.WriteLine("✅ Saved to Firestore");
            
            // ✅ Upload to Render.com server
            var examUrl = await PublishToRenderAsync(examId, htmlContent);
            System.Diagnostics.Debug.WriteLine($"✅ Uploaded to Render: {examUrl}");
            
            // Update exam URL in Firestore
            publishedExam.ExamUrl = examUrl;
            await _firestoreService.SavePublishedExamAsync(publishedExam);
            System.Diagnostics.Debug.WriteLine("✅ Updated Firestore with URL");
            
            return (htmlContent, examId, examUrl);
        }
        catch (Exception ex)
        {
            System.Diagnostics.Debug.WriteLine($"❌ Publishing failed: {ex.Message}");
            throw;
        }
    }

    /// <summary>
    /// Upload HTML to Render.com server
    /// </summary>
    private async Task<string> PublishToRenderAsync(string examId, string htmlContent)
    {
        try
        {
            System.Diagnostics.Debug.WriteLine($"🔍 Checking server health at: {_publishingServerUrl}");
            
            // Check if server is reachable
            var healthResponse = await _httpClient.GetAsync($"{_publishingServerUrl}/health");
            if (!healthResponse.IsSuccessStatusCode)
            {
                throw new Exception("Publishing server is not reachable. The server may be starting up (this takes ~30 seconds on first request). Please try again.");
            }
            
            System.Diagnostics.Debug.WriteLine("✅ Server is healthy");
            
            // Send publish request
            var payload = new
            {
                examId = examId,
                htmlContent = htmlContent
            };
            
            System.Diagnostics.Debug.WriteLine($"📤 Sending publish request for exam: {examId}");
            
            var response = await _httpClient.PostAsJsonAsync(
                $"{_publishingServerUrl}/api/publish", 
                payload
            );
            
            if (!response.IsSuccessStatusCode)
            {
                var errorContent = await response.Content.ReadAsStringAsync();
                System.Diagnostics.Debug.WriteLine($"❌ Server error: {errorContent}");
                throw new Exception($"Failed to publish exam: {errorContent}");
            }
            
            var result = await response.Content.ReadFromJsonAsync<PublishResult>();
            
            if (result == null || string.IsNullOrEmpty(result.ExamUrl))
            {
                throw new Exception("Server did not return exam URL");
            }
            
            System.Diagnostics.Debug.WriteLine($"✅ Exam published successfully: {result.ExamUrl}");
            return result.ExamUrl;
        }
        catch (HttpRequestException ex)
        {
            System.Diagnostics.Debug.WriteLine($"❌ Connection error: {ex.Message}");
            throw new Exception($"Cannot connect to publishing server at {_publishingServerUrl}. Please check your internet connection. If this is your first request, the server may be waking up (wait ~30 seconds and try again).", ex);
        }
        catch (TaskCanceledException ex)
        {
            System.Diagnostics.Debug.WriteLine($"❌ Timeout: {ex.Message}");
            throw new Exception("Request timed out. The server may be starting up. Please try again in a moment.", ex);
        }
    }

    private class PublishResult
    {
        public bool Success { get; set; }
        public string ExamUrl { get; set; } = "";
        public string ExamId { get; set; } = "";
    }

    private string GenerateExamHtml(ExamReviewData examData, string examId)
    {
        var sb = new StringBuilder();
        
        sb.AppendLine("<!DOCTYPE html>");
        sb.AppendLine("<html lang=\"en\">");
        sb.AppendLine("<head>");
        sb.AppendLine("    <meta charset=\"UTF-8\">");
        sb.AppendLine("    <meta name=\"viewport\" content=\"width=device-width, initial-scale=1.0\">");
        sb.AppendLine($"    <title>{System.Security.SecurityElement.Escape(examData.Title)}</title>");
        sb.AppendLine("    <style>");
        sb.AppendLine(GetExamStyleSheet());
        sb.AppendLine("    </style>");
        sb.AppendLine("</head>");
        sb.AppendLine("<body>");
        
        sb.AppendLine("    <div class=\"exam-container\">");
        sb.AppendLine("        <div class=\"exam-header\">");
        sb.AppendLine($"            <h1>{System.Security.SecurityElement.Escape(examData.Title)}</h1>");
        sb.AppendLine("            <div class=\"timer-container\">");
        sb.AppendLine("                <div class=\"timer\" id=\"timer\">Time Remaining: <span id=\"time\">00:00:00</span></div>");
        sb.AppendLine("            </div>");
        sb.AppendLine("        </div>");
        
        // Student Info Form - Dynamic based on LoginConfig
        sb.AppendLine("        <div id=\"studentInfoSection\" class=\"exam-instructions\">");
        sb.AppendLine("            <h2>Student Information</h2>");
        sb.AppendLine("            <p>Please enter your details before starting the exam:</p>");
        sb.AppendLine("            <div style=\"margin: 20px 0;\">");
        
        // Generate fields based on LoginConfig
        var loginConfig = examData.LoginConfig;
        if (loginConfig != null && !loginConfig.IsGoogleSignIn)
        {
            // Guest Login - Show configured fields
            if (loginConfig.RequireFullName)
            {
                sb.AppendLine("                <input type=\"text\" id=\"studentName\" placeholder=\"Full Name\" style=\"width: 100%; padding: 10px; margin-bottom: 10px; font-size: 16px; border: 2px solid #ddd; border-radius: 5px;\" required>");
            }
            
            if (loginConfig.RequireStudentNumber)
            {
                sb.AppendLine("                <input type=\"text\" id=\"studentId\" placeholder=\"Student ID\" style=\"width: 100%; padding: 10px; margin-bottom: 10px; font-size: 16px; border: 2px solid #ddd; border-radius: 5px;\" required>");
            }
            
            if (loginConfig.RequireYearSection)
            {
                sb.AppendLine("                <input type=\"text\" id=\"yearSection\" placeholder=\"Year and Section\" style=\"width: 100%; padding: 10px; margin-bottom: 10px; font-size: 16px; border: 2px solid #ddd; border-radius: 5px;\" required>");
            }
        }
        else
        {
            // Google Sign-In or default fallback
            sb.AppendLine("                <input type=\"text\" id=\"studentName\" placeholder=\"Full Name\" style=\"width: 100%; padding: 10px; margin-bottom: 10px; font-size: 16px; border: 2px solid #ddd; border-radius: 5px;\" required>");
            sb.AppendLine("                <input type=\"email\" id=\"studentEmail\" placeholder=\"Email Address\" style=\"width: 100%; padding: 10px; margin-bottom: 10px; font-size: 16px; border: 2px solid #ddd; border-radius: 5px;\" required>");
        }
        
        sb.AppendLine("                <button onclick=\"startExam()\" style=\"width: 100%; padding: 15px; background: #27ae60; color: white; border: none; font-size: 18px; font-weight: bold; border-radius: 5px; cursor: pointer;\">Start Exam</button>");
        sb.AppendLine("            </div>");
        sb.AppendLine("        </div>");
        
        sb.AppendLine("        <div id=\"examSection\" style=\"display: none;\">");
        sb.AppendLine("        <div class=\"exam-instructions\">");
        sb.AppendLine("            <h2>Instructions</h2>");
        sb.AppendLine("            <ul>");
        sb.AppendLine($"                <li>Duration: {examData.ExamDuration} minutes</li>");
        sb.AppendLine("                <li>Answer all questions to the best of your ability</li>");
        sb.AppendLine("                <li>Click 'Submit Exam' when finished</li>");
        sb.AppendLine("                <li>Make sure to submit before the timer expires</li>");
        sb.AppendLine("            </ul>");
        sb.AppendLine("        </div>");
        
        sb.AppendLine("        <form id=\"examForm\" onsubmit=\"submitExam(event)\">");
        int questionNumber = 1;
        
        foreach (var content in examData.Contents)
        {
            sb.AppendLine("            <div class=\"question-container\">");
            sb.AppendLine($"                <div class=\"question-header\">");
            sb.AppendLine($"                    <span class=\"question-number\">Question {questionNumber}</span>");
            sb.AppendLine($"                    <span class=\"question-points\">{content.Points} points</span>");
            sb.AppendLine("                </div>");
            sb.AppendLine($"                <p class=\"question-text\">{System.Security.SecurityElement.Escape(content.QuestionText)}</p>");
            
            if (content.QuestionType == "Multiple Choice")
            {
                sb.AppendLine("                <div class=\"options\">");
                for (int i = 0; i < content.Options.Count; i++)
                {
                    var optionId = $"q{questionNumber}_opt{i}";
                    var escapedOption = System.Security.SecurityElement.Escape(content.Options[i]);
                    sb.AppendLine($"                    <label class=\"option-label\">");
                    sb.AppendLine($"                        <input type=\"radio\" name=\"question{questionNumber}\" value=\"{escapedOption}\" id=\"{optionId}\" required>");
                    sb.AppendLine($"                        <span>{escapedOption}</span>");
                    sb.AppendLine("                    </label>");
                }
                sb.AppendLine("                </div>");
            }
            else if (content.QuestionType == "True/False")
            {
                sb.AppendLine("                <div class=\"options\">");
                sb.AppendLine($"                    <label class=\"option-label\">");
                sb.AppendLine($"                        <input type=\"radio\" name=\"question{questionNumber}\" value=\"True\" required>");
                sb.AppendLine($"                        <span>True</span>");
                sb.AppendLine("                    </label>");
                sb.AppendLine($"                    <label class=\"option-label\">");
                sb.AppendLine($"                        <input type=\"radio\" name=\"question{questionNumber}\" value=\"False\" required>");
                sb.AppendLine($"                        <span>False</span>");
                sb.AppendLine("                    </label>");
                sb.AppendLine("                </div>");
            }
            // ✅ NEW: Modified True or False with conditional textbox
            else if (content.QuestionType == "Modified True/False")
            {
                sb.AppendLine($"                <div class=\"options\">");
                sb.AppendLine($"                    <label class=\"option-label\">");
                sb.AppendLine($"                        <input type=\"radio\" name=\"question{questionNumber}\" value=\"True\" onchange=\"toggleCorrection{questionNumber}(false)\" required>");
                sb.AppendLine($"                        <span>True</span>");
                sb.AppendLine("                    </label>");
                sb.AppendLine($"                    <label class=\"option-label\">");
                sb.AppendLine($"                        <input type=\"radio\" name=\"question{questionNumber}\" value=\"False\" onchange=\"toggleCorrection{questionNumber}(true)\" required>");
                sb.AppendLine($"                        <span>False</span>");
                sb.AppendLine("                    </label>");
                sb.AppendLine("                </div>");
                sb.AppendLine($"                <div id=\"correction{questionNumber}\" style=\"display: none; margin-top: 15px;\">");
                sb.AppendLine($"                    <label style=\"display: block; margin-bottom: 8px; color: #2c3e50; font-weight: 600;\">If false, what should it be? (Correction)</label>");
                sb.AppendLine($"                    <textarea name=\"question{questionNumber}_correction\" rows=\"3\" class=\"text-answer\" style=\"width: 100%;\"></textarea>");
                sb.AppendLine("                </div>");
                sb.AppendLine($"                <script>");
                sb.AppendLine($"                    function toggleCorrection{questionNumber}(show) {{");
                sb.AppendLine($"                        document.getElementById('correction{questionNumber}').style.display = show ? 'block' : 'none';");
                sb.AppendLine($"                        const textarea = document.querySelector('[name=\"question{questionNumber}_correction\"]');");
                sb.AppendLine($"                        if (!show) textarea.value = ''; // Clear correction if True is selected");
                sb.AppendLine("                    }");
                sb.AppendLine("                </script>");
            }
            else if (content.QuestionType == "Short Answer" || content.QuestionType == "Essay")
            {
                var rows = content.QuestionType == "Essay" ? 10 : 3;
                sb.AppendLine($"                <textarea name=\"question{questionNumber}\" rows=\"{rows}\" class=\"text-answer\" required></textarea>");
            }
            
            sb.AppendLine("            </div>");
            questionNumber++;
        }
        
        sb.AppendLine("            <div class=\"submit-container\">");
        sb.AppendLine("                <button type=\"submit\" class=\"submit-button\">Submit Exam</button>");
        sb.AppendLine("            </div>");
        sb.AppendLine("        </form>");
        sb.AppendLine("        </div>");
        sb.AppendLine("    </div>");
        
        sb.AppendLine("    <script>");
        sb.AppendLine(GetExamJavaScript(examData, examId));
        sb.AppendLine("    </script>");
        sb.AppendLine("</body>");
        sb.AppendLine("</html>");
        
        return sb.ToString();
    }

    private string GetExamStyleSheet()
    {
        return @"
        * {
            margin: 0;
            padding: 0;
            box-sizing: border-box;
        }

        body {
            font-family: 'Segoe UI', Tahoma, Geneva, Verdana, sans-serif;
            background: linear-gradient(135deg, #667eea 0%, #764ba2 100%);
            min-height: 100vh;
            padding: 20px;
        }

        .exam-container {
            max-width: 900px;
            margin: 0 auto;
            background: white;
            border-radius: 15px;
            box-shadow: 0 10px 40px rgba(0, 0, 0, 0.2);
            overflow: hidden;
        }

        .exam-header {
            background: linear-gradient(135deg, #2c3e50 0%, #34495e 100%);
            color: white;
            padding: 30px;
            text-align: center;
        }

        .exam-header h1 {
            font-size: clamp(24px, 5vw, 36px);
            margin-bottom: 15px;
        }

        .timer-container {
            margin-top: 15px;
        }

        .timer {
            display: inline-block;
            background: rgba(255, 255, 255, 0.2);
            padding: 12px 25px;
            border-radius: 25px;
            font-size: clamp(14px, 3vw, 18px);
            font-weight: bold;
        }

        .exam-instructions {
            background: #ecf0f1;
            padding: 25px;
            border-bottom: 3px solid #3498db;
        }

        .exam-instructions h2 {
            color: #2c3e50;
            margin-bottom: 15px;
            font-size: clamp(18px, 4vw, 24px);
        }

        .exam-instructions ul {
            list-style-position: inside;
            color: #34495e;
            line-height: 1.8;
        }

        .exam-instructions li {
            font-size: clamp(14px, 2.5vw, 16px);
        }

        .question-container {
            padding: 30px;
            border-bottom: 2px solid #ecf0f1;
        }

        .question-header {
            display: flex;
            justify-content: space-between;
            align-items: center;
            margin-bottom: 15px;
            flex-wrap: wrap;
            gap: 10px;
        }

        .question-number {
            font-weight: bold;
            color: #3498db;
            font-size: clamp(16px, 3vw, 20px);
        }

        .question-points {
            background: #27ae60;
            color: white;
            padding: 5px 15px;
            border-radius: 15px;
            font-size: clamp(12px, 2.5vw, 14px);
            font-weight: bold;
        }

        .question-text {
            font-size: clamp(14px, 3vw, 18px);
            color: #2c3e50;
            line-height: 1.6;
            margin-bottom: 20px;
        }

        .options {
            display: flex;
            flex-direction: column;
            gap: 12px;
        }

        .option-label {
            display: flex;
            align-items: center;
            padding: 15px;
            background: #f8f9fa;
            border: 2px solid #dee2e6;
            border-radius: 8px;
            cursor: pointer;
            transition: all 0.3s ease;
        }

        .option-label:hover {
            background: #e9ecef;
            border-color: #3498db;
        }

        .option-label input[type=""radio""] {
            margin-right: 12px;
            width: 20px;
            height: 20px;
            cursor: pointer;
        }

        .option-label span {
            font-size: clamp(14px, 2.5vw, 16px);
            color: #2c3e50;
        }

        .text-answer {
            width: 100%;
            padding: 15px;
            border: 2px solid #dee2e6;
            border-radius: 8px;
            font-size: clamp(14px, 2.5vw, 16px);
            font-family: inherit;
            resize: vertical;
            transition: border-color 0.3s ease;
        }

        .text-answer:focus {
            outline: none;
            border-color: #3498db;
        }

        .submit-container {
            padding: 30px;
            text-align: center;
            background: #f8f9fa;
        }

        .submit-button {
            background: linear-gradient(135deg, #27ae60 0%, #229954 100%);
            color: white;
            border: none;
            padding: 15px 50px;
            font-size: clamp(16px, 3vw, 20px);
            font-weight: bold;
            border-radius: 30px;
            cursor: pointer;
            box-shadow: 0 5px 15px rgba(39, 174, 96, 0.3);
            transition: all 0.3s ease;
        }

        .submit-button:hover {
            transform: translateY(-2px);
            box-shadow: 0 7px 20px rgba(39, 174, 96, 0.4);
        }

        .submit-button:active {
            transform: translateY(0);
        }

        @media (max-width: 768px) {
            body {
                padding: 10px;
            }

            .exam-container {
                border-radius: 10px;
            }

            .exam-header {
                padding: 20px;
            }

            .exam-instructions {
                padding: 20px;
            }

            .question-container {
                padding: 20px;
            }

            .submit-container {
                padding: 20px;
            }
        }

        @media (max-width: 480px) {
            .question-header {
                flex-direction: column;
                align-items: flex-start;
            }

            .submit-button {
                padding: 12px 30px;
            }
        }";
    }

    private string GetExamJavaScript(ExamReviewData examData, string examId)
    {
        var sb = new StringBuilder();
        
        sb.AppendLine("        // Exam Configuration");
        sb.AppendLine($"        const examId = '{examId}';");
        sb.AppendLine($"        const examDuration = {examData.ExamDuration};");
        sb.AppendLine($"        const apiEndpoint = '{_apiEndpoint}';");
        sb.AppendLine();
        sb.AppendLine("        let timeRemaining = examDuration * 60;");
        sb.AppendLine("        let timerInterval;");
        sb.AppendLine();
        
        // Start Exam Function
        sb.AppendLine("        function startExam() {");
        sb.AppendLine("            const studentName = document.getElementById('studentName');");
        sb.AppendLine("            const studentId = document.getElementById('studentId');");
        sb.AppendLine("            const yearSection = document.getElementById('yearSection');");
        sb.AppendLine("            const studentEmail = document.getElementById('studentEmail');");
        sb.AppendLine();
        sb.AppendLine("            let missingFields = [];" );
        sb.AppendLine();
        sb.AppendLine("            if (studentName && !studentName.value.trim()) {");
        sb.AppendLine("                missingFields.push('Full Name');");
        sb.AppendLine("            }");
        sb.AppendLine("            if (studentId && !studentId.value.trim()) {");
        sb.AppendLine("                missingFields.push('Student ID');");
        sb.AppendLine("            }");
        sb.AppendLine("            if (yearSection && !yearSection.value.trim()) {");
        sb.AppendLine("                missingFields.push('Year and Section');");
        sb.AppendLine("            }");
        sb.AppendLine("            if (studentEmail && !studentEmail.value.trim()) {");
        sb.AppendLine("                missingFields.push('Email');");
        sb.AppendLine("            }");
        sb.AppendLine();
        sb.AppendLine("            if (missingFields.length > 0) {");
        sb.AppendLine("                alert('Please fill in: ' + missingFields.join(', '));");
        sb.AppendLine("                return;");
        sb.AppendLine("            }" );
        sb.AppendLine();
        sb.AppendLine("            window.studentInfo = {");
        sb.AppendLine("                name: studentName ? studentName.value : '',");
        sb.AppendLine("                studentId: studentId ? studentId.value : '',");
        sb.AppendLine("                yearSection: yearSection ? yearSection.value : '',");
        sb.AppendLine("                email: studentEmail ? studentEmail.value : ''");
        sb.AppendLine("            };");
        sb.AppendLine();
        sb.AppendLine("            document.getElementById('studentInfoSection').style.display = 'none';");
        sb.AppendLine("            document.getElementById('examSection').style.display = 'block';");
        sb.AppendLine("            startTimer();");
        sb.AppendLine("        }");
        sb.AppendLine();
        
        // Timer Function
        sb.AppendLine("        function startTimer() {");
        sb.AppendLine("            timerInterval = setInterval(() => {");
        sb.AppendLine("                if (timeRemaining <= 0) {");
        sb.AppendLine("                    clearInterval(timerInterval);");
        sb.AppendLine("                    alert('Time is up! Submitting your exam...');");
        sb.AppendLine("                    document.getElementById('examForm').dispatchEvent(new Event('submit'));");
        sb.AppendLine("                    return;");
        sb.AppendLine("                }");
        sb.AppendLine();
        sb.AppendLine("                timeRemaining--;");
        sb.AppendLine("                const hours = Math.floor(timeRemaining / 3600);");
        sb.AppendLine("                const minutes = Math.floor((timeRemaining % 3600) / 60);");
        sb.AppendLine("                const seconds = timeRemaining % 60;");
        sb.AppendLine();
        sb.AppendLine("                document.getElementById('time').textContent = ");
        sb.AppendLine("                    String(hours).padStart(2, '0') + ':' +");
        sb.AppendLine("                    String(minutes).padStart(2, '0') + ':' +");
        sb.AppendLine("                    String(seconds).padStart(2, '0');");
        sb.AppendLine("            }, 1000);");
        sb.AppendLine("        }");
        sb.AppendLine();
        
        // Submit Function (rest of the JavaScript...)
        sb.AppendLine("        async function submitExam(event) {");
        sb.AppendLine("            event.preventDefault();");
        sb.AppendLine("            // Submit logic here...");
        sb.AppendLine("        }");
        
        return sb.ToString();
    }
}