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
    private readonly string _signalRHubUrl;
    private readonly HttpClient _httpClient;

    public ExamPublishingService(FirestoreService firestoreService, string hostingUrl, string apiEndpoint, string publishingServerUrl, string? signalRHubUrl = null)
    {
        _firestoreService = firestoreService;
        _hostingUrl = hostingUrl;
        _apiEndpoint = apiEndpoint;
        _publishingServerUrl = publishingServerUrl;
        _signalRHubUrl = signalRHubUrl ?? publishingServerUrl;
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
                ExamDuration = examData.ExamDuration,
                LoginConfig = examData.LoginConfig, // ✅ FIX: Pass LoginConfig for HTML generation
                AntiCheat = examData.AntiCheat // ✅ FIX: Pass AntiCheat config for HTML generation
            };
            
            // Generate HTML with sanitized data
            var htmlContent = GenerateExamHtml(sanitizedExamData, examId);
            System.Diagnostics.Debug.WriteLine("✅ HTML generated");
            
            // Save FULL exam data (including correct answers) to Firestore
            var publishedExam = new PublishedExam
            {
                Id = examId,
                Title = examData.Title,
                Subject = string.IsNullOrWhiteSpace(examData.Subject) ? "General" : examData.Subject, // ✅ Capture subject
                Structures = examData.Structures,
                Contents = examData.Contents,
                StartTime = examData.StartTime,
                EndTime = examData.EndTime,
                ExamDuration = examData.ExamDuration,
                PublishedDate = DateTime.UtcNow,
                CreatedBy = creatorEmail,
                ExamUrl = "",
                Status = "Active",
                LoginConfig = examData.LoginConfig,
                AntiCheat = examData.AntiCheat
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
        
        // Add Google Sign-In if needed
        var loginConfig = examData.LoginConfig;
        if (loginConfig != null && loginConfig.IsGoogleSignIn)
        {
            sb.AppendLine("    <script src=\"https://accounts.google.com/gsi/client\" async defer></script>");
        }
        
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
        if (loginConfig != null && loginConfig.IsGoogleSignIn)
        {
            // Google Sign-In Button
            sb.AppendLine("                <div style=\"text-align: center; margin: 30px 0;\">");
            sb.AppendLine("                    <div id=\"g_id_onload\"");
            sb.AppendLine("                         data-client_id=\"161243911904-9hebpq9f3c2r7gg9ih2uroimr155lbjn.apps.googleusercontent.com\"");
            sb.AppendLine("                         data-callback=\"handleGoogleSignIn\"");
            sb.AppendLine("                         data-auto_prompt=\"false\">");
            sb.AppendLine("                    </div>");
            sb.AppendLine("                    <div class=\"g_id_signin\"");
            sb.AppendLine("                         data-type=\"standard\"");
            sb.AppendLine("                         data-size=\"large\"");
            sb.AppendLine("                         data-theme=\"outline\"");
            sb.AppendLine("                         data-text=\"sign_in_with\"");
            sb.AppendLine("                         data-shape=\"rectangular\"");
            sb.AppendLine("                         data-logo_alignment=\"left\">");
            sb.AppendLine("                    </div>");
            sb.AppendLine("                    <p style=\"color: #666; margin-top: 15px; font-size: 14px;\">Sign in with your Google account to start the exam</p>");
            sb.AppendLine("                </div>");
        }
        else
        {
            // Guest Login - Show configured fields
            if (loginConfig != null && loginConfig.RequireFullName)
            {
                sb.AppendLine("                <input type=\"text\" id=\"studentName\" placeholder=\"Full Name\" style=\"width: 100%; padding: 12px; margin-bottom: 12px; font-size: 16px; border: 2px solid #ddd; border-radius: 8px;\" required>");
            }
            
            if (loginConfig != null && loginConfig.RequireStudentNumber)
            {
                sb.AppendLine("                <input type=\"text\" id=\"studentId\" placeholder=\"Student Number\" style=\"width: 100%; padding: 12px; margin-bottom: 12px; font-size: 16px; border: 2px solid #ddd; border-radius: 8px;\" required>");
            }
            
            if (loginConfig != null && loginConfig.RequireYearSection)
            {
                sb.AppendLine("                <input type=\"text\" id=\"yearSection\" placeholder=\"Year and Section (e.g., 3rd Year - Section A)\" style=\"width: 100%; padding: 12px; margin-bottom: 12px; font-size: 16px; border: 2px solid #ddd; border-radius: 8px;\" required>");
            }
            
            sb.AppendLine("                <button onclick=\"startExam()\" id=\"startExamBtn\" style=\"width: 100%; padding: 15px; background: linear-gradient(135deg, #667eea 0%, #764ba2 100%); color: white; border: none; font-size: 18px; font-weight: bold; border-radius: 8px; cursor: pointer; margin-top: 10px; transition: transform 0.2s;\">Start Exam</button>");
        }
        
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
        
        // Generate JavaScript and ensure any accidental </script> sequences are escaped
        var jsContent = GetExamJavaScript(examData, examId);
        // Add a lightweight initialization log and error boundary
        jsContent = "try{console.log('Exam JS initializing');}catch(e){console.error('Exam JS init error',e);}\n" + jsContent;
        // Prevent closing the script tag prematurely when embedding
        jsContent = jsContent.Replace("</script>", "<\\/script>");

        sb.AppendLine("    <script>");
        sb.AppendLine(jsContent);
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

        .navigation-buttons {
            display: flex;
            gap: 15px;
            margin-bottom: 20px;
            flex-wrap: wrap;
        }

        .nav-button {
            background: linear-gradient(135deg, #667eea 0%, #764ba2 100%);
            color: white;
            border: none;
            padding: 12px 30px;
            font-size: 16px;
            font-weight: bold;
            border-radius: 8px;
            cursor: pointer;
            box-shadow: 0 3px 10px rgba(102, 126, 234, 0.3);
            transition: all 0.3s ease;
        }

        .nav-button:hover:not(:disabled) {
            transform: translateY(-2px);
            box-shadow: 0 5px 15px rgba(102, 126, 234, 0.4);
        }

        .nav-button:disabled {
            opacity: 0.5;
            cursor: not-allowed;
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
        var loginConfig = examData.LoginConfig;
        var antiCheat = examData.AntiCheat;
        
        System.Diagnostics.Debug.WriteLine($"[ExamPublishingService] antiCheat is null: {antiCheat == null}");
        if (antiCheat != null)
        {
            System.Diagnostics.Debug.WriteLine($"[ExamPublishingService] DetectTabbing: {antiCheat.DetectTabbing}, WarningOnly: {antiCheat.WarningOnly}");
        }
        
        sb.AppendLine("        // Exam Configuration");
        sb.AppendLine($"        const examId = '{examId}';");
        sb.AppendLine($"        const examDuration = {examData.ExamDuration};");
        sb.AppendLine($"        const apiEndpoint = '{_apiEndpoint}';");
        sb.AppendLine($"        const useGoogleSignIn = {(loginConfig?.IsGoogleSignIn == true ? "true" : "false")};");
        sb.AppendLine("        let examStarted = false;");
        var antiDict = new System.Collections.Generic.Dictionary<string, object?>
        {
            ["DetectTabbing"] = antiCheat?.DetectTabbing ?? false,
            ["WarningOnly"] = antiCheat?.WarningOnly ?? false,
            ["DeductPoints"] = antiCheat?.DeductPoints ?? false,
            ["DeductPointsValue"] = antiCheat?.DeductPointsValue ?? "0",
            ["AutoSubmit"] = antiCheat?.AutoSubmit ?? false,
            ["OneQuestionAtATime"] = antiCheat?.OneQuestionAtATime ?? false,
            ["DisableBacktrack"] = antiCheat?.DisableBacktrack ?? false,
            ["TimeLimitPerQuestion"] = antiCheat?.TimeLimitPerQuestion ?? false,
            ["TimeLimitValue"] = antiCheat?.TimeLimitValue ?? "60",
            ["DisableCopyPaste"] = antiCheat?.DisableCopyPaste ?? false,
            ["DisableScreenshot"] = antiCheat?.DisableScreenshot ?? false,
            ["AutoResumeSession"] = antiCheat?.AutoResumeSession ?? false,
            ["LimitRetakeAttempts"] = antiCheat?.LimitRetakeAttempts ?? false,
            ["RetakeAttemptsValue"] = antiCheat?.RetakeAttemptsValue ?? "3"
        };

        var antiCheatJson = System.Text.Json.JsonSerializer.Serialize(antiDict);
        sb.AppendLine("        const antiCheatConfig = " + antiCheatJson + ";");
        sb.AppendLine("        console.log('Anti-cheat configuration loaded:', antiCheatConfig);");
        sb.AppendLine("        console.log('[Anti-Cheat] DetectTabbing:', antiCheatConfig.DetectTabbing);");
        sb.AppendLine("        console.log('[Anti-Cheat] WarningOnly:', antiCheatConfig.WarningOnly);");
        sb.AppendLine("        console.log('[Anti-Cheat] AutoResumeSession:', antiCheatConfig.AutoResumeSession);");
        sb.AppendLine();
        sb.AppendLine("        let timeRemaining = examDuration * 60;");
        sb.AppendLine("        let timerInterval;");
        sb.AppendLine("        // Anti-cheat state tracking");
        sb.AppendLine("        let tabSwitchCount = 0;");
        sb.AppendLine("        let deductedPoints = 0;");
        sb.AppendLine("        let screenshotAttempts = 0;");
        sb.AppendLine("        let copyPasteAttempts = 0;");
        sb.AppendLine();

        // SignalR hub URL (attempt to connect for real-time reporting)
        var hubUrl = _signalRHubUrl?.TrimEnd('/') + "/sessionHub";
        var hubUrlJson = System.Text.Json.JsonSerializer.Serialize(hubUrl);
        sb.AppendLine("        const signalRHubUrl = " + hubUrlJson + ";");
        sb.AppendLine("        console.log('[SignalR] Hub URL:', signalRHubUrl);");
        sb.AppendLine("        // Load SignalR client dynamically and connect (best-effort)");
        sb.AppendLine("        (function(){\n            try {\n                var script = document.createElement('script');\n                script.src = 'https://cdn.jsdelivr.net/npm/@microsoft/signalr@7.0.7/dist/browser/signalr.min.js';\n                script.onload = function() {\n                    try {\n                        if (typeof signalR === 'undefined') return;\n                        window.signalRConnection = new signalR.HubConnectionBuilder().withUrl(signalRHubUrl).withAutomaticReconnect().build();\n                        window.signalRConnection.start().then(function(){\n                            console.log('✅ Connected to SignalR hub');\n                            // Will join when student info is available after startExam/GoogleSignIn\n                        }).catch(function(err){ console.warn('SignalR start failed', err); });\n                    } catch (e) { console.warn('SignalR init error', e); }\n                };\n                script.onerror = function(e){ console.warn('Failed to load SignalR client', e); };\n                document.head.appendChild(script);\n            } catch (e) { console.warn('Failed to inject SignalR script', e); }\n        })();");
        sb.AppendLine();
        
        // Helper function to report violations
        sb.AppendLine("        // Helper function to report anti-cheat violations");
        sb.AppendLine("        function reportViolation(eventType, details) {");
        sb.AppendLine("            try {");
        sb.AppendLine("                console.warn('[Anti-Cheat] ' + eventType + ':', details);");
        sb.AppendLine("                if (window.signalRConnection && typeof window.signalRConnection.invoke === 'function') {");
        sb.AppendLine("                    window.signalRConnection.invoke('ReportEvent', ");
        sb.AppendLine("                        examId,");
        sb.AppendLine("                        (window.studentInfo && window.studentInfo.studentId) ? window.studentInfo.studentId : 'unknown',");
        sb.AppendLine("                        (window.studentInfo && window.studentInfo.name) ? window.studentInfo.name : 'Unknown Student',");
        sb.AppendLine("                        eventType,");
        sb.AppendLine("                        JSON.stringify(details)");
        sb.AppendLine("                    );");
        sb.AppendLine("                    console.log('[Anti-Cheat] Violation reported to server');");
        sb.AppendLine("                } else {");
        sb.AppendLine("                    console.warn('[Anti-Cheat] SignalR not available, violation not reported to server');");
        sb.AppendLine("                }");
        sb.AppendLine("            } catch(e) {");
        sb.AppendLine("                console.error('[Anti-Cheat] Failed to report violation:', e);");
        sb.AppendLine("            }");
        sb.AppendLine("        }");
        sb.AppendLine();
        
        // Auto-save and resume session functionality
        sb.AppendLine("        // Auto-save and resume session functionality");
        sb.AppendLine("        const SESSION_SAVE_KEY = 'examSession_' + examId;");
        sb.AppendLine();
        sb.AppendLine("        function saveSessionProgress() {");
        sb.AppendLine("            if (!antiCheatConfig.AutoResumeSession) return;");
        sb.AppendLine("            try {");
        sb.AppendLine("                const form = document.getElementById('examForm');");
        sb.AppendLine("                if (!form) return;");
        sb.AppendLine("                const formData = new FormData(form);");
        sb.AppendLine("                const answers = {};");
        sb.AppendLine("                for (let [key, value] of formData.entries()) { answers[key] = value; }");
        sb.AppendLine("                const sessionData = {");
        sb.AppendLine("                    examId: examId,");
        sb.AppendLine("                    studentInfo: window.studentInfo || {},");
        sb.AppendLine("                    answers: answers,");
        sb.AppendLine("                    timeRemaining: timeRemaining,");
        sb.AppendLine("                    currentQuestionIndex: typeof currentQuestionIndex !== 'undefined' ? currentQuestionIndex : 0,");
        sb.AppendLine("                    tabSwitchCount: tabSwitchCount,");
        sb.AppendLine("                    deductedPoints: deductedPoints,");
        sb.AppendLine("                    timestamp: new Date().toISOString()");
        sb.AppendLine("                };");
        sb.AppendLine("                localStorage.setItem(SESSION_SAVE_KEY, JSON.stringify(sessionData));");
        sb.AppendLine("                console.log('[Auto-Save] Progress saved');");
        sb.AppendLine("                if (window.signalRConnection && typeof window.signalRConnection.invoke === 'function') {");
        sb.AppendLine("                    window.signalRConnection.invoke('SaveSessionProgress', examId, sessionData, (window.studentInfo && window.studentInfo.studentId) ? window.studentInfo.studentId : '', (window.studentInfo && window.studentInfo.name) ? window.studentInfo.name : '', (window.studentInfo && window.studentInfo.email) ? window.studentInfo.email : '').catch(function(err) {");
        sb.AppendLine("                        console.warn('[Auto-Save] Server save failed:', err);");
        sb.AppendLine("                    });");
        sb.AppendLine("                }");
        sb.AppendLine("            } catch(e) { console.error('[Auto-Save] Failed:', e); }");
        sb.AppendLine("        }");
        sb.AppendLine();
        sb.AppendLine("        function checkForSavedSession() {");
        sb.AppendLine("            if (!antiCheatConfig.AutoResumeSession) return false;");
        sb.AppendLine("            try {");
        sb.AppendLine("                const saved = localStorage.getItem(SESSION_SAVE_KEY);");
        sb.AppendLine("                if (!saved) return false;");
        sb.AppendLine("                const data = JSON.parse(saved);");
        sb.AppendLine("                const hoursDiff = (new Date() - new Date(data.timestamp)) / (1000 * 60 * 60);");
        sb.AppendLine("                if (hoursDiff > 24) { localStorage.removeItem(SESSION_SAVE_KEY); return false; }");
        sb.AppendLine("                return data;");
        sb.AppendLine("            } catch(e) { return false; }");
        sb.AppendLine("        }");
        sb.AppendLine();
        sb.AppendLine("        function restoreSession(data) {");
        sb.AppendLine("            try {");
        sb.AppendLine("                console.log('[Auto-Resume] Restoring session...');");
        sb.AppendLine("                window.studentInfo = data.studentInfo;");
        sb.AppendLine("                timeRemaining = data.timeRemaining || timeRemaining;");
        sb.AppendLine("                tabSwitchCount = data.tabSwitchCount || 0;");
        sb.AppendLine("                deductedPoints = data.deductedPoints || 0;");
        sb.AppendLine("                examStarted = true;");
        sb.AppendLine("                document.getElementById('studentInfoSection').style.display = 'none';");
        sb.AppendLine("                document.getElementById('examSection').style.display = 'block';");
        sb.AppendLine("                setTimeout(function() {");
        sb.AppendLine("                    for (let [key, value] of Object.entries(data.answers)) {");
        sb.AppendLine("                        const el = document.querySelector('[name=\"' + key + '\"]');");
        sb.AppendLine("                        if (el) {");
        sb.AppendLine("                            if (el.type === 'radio') {");
        sb.AppendLine("                                const radio = document.querySelector('[name=\"' + key + '\"][value=\"' + value + '\"]');");
        sb.AppendLine("                                if (radio) radio.checked = true;");
        sb.AppendLine("                            } else { el.value = value; }");
        sb.AppendLine("                        }");
        sb.AppendLine("                    }");
        sb.AppendLine("                    if (antiCheatConfig.OneQuestionAtATime) {");
        sb.AppendLine("                        const submitContainer = document.querySelector('.submit-container');");
        sb.AppendLine("                        if (submitContainer) submitContainer.style.display = 'none';");
        sb.AppendLine("                        createNavigationBar();");
        sb.AppendLine("                        currentQuestionIndex = data.currentQuestionIndex || 0;");
        sb.AppendLine("                        showQuestion(currentQuestionIndex);");
        sb.AppendLine("                    }");
        sb.AppendLine("                    startTimer();");
        sb.AppendLine("                    console.log('[Auto-Resume] Session restored successfully');");
        sb.AppendLine("                }, 100);");
        sb.AppendLine("            } catch(e) { console.error('[Auto-Resume] Failed:', e); }");
        sb.AppendLine("        }");
        sb.AppendLine();
        
        // Google Sign-In Handler
        if (loginConfig?.IsGoogleSignIn == true)
        {
            sb.AppendLine("        function handleGoogleSignIn(response) {");
            sb.AppendLine("            try {");
            sb.AppendLine("                const payload = JSON.parse(atob(response.credential.split('.')[1]));");
            sb.AppendLine("                window.studentInfo = {");
            sb.AppendLine("                    studentId: payload.sub,");
            sb.AppendLine("                    name: payload.name,");
            sb.AppendLine("                    email: payload.email");
            sb.AppendLine("                };");
            sb.AppendLine("                console.log('Google Sign-In successful:', window.studentInfo);");
            sb.AppendLine("                if (!checkRetakeAttempts()) {");
            sb.AppendLine("                    return;");
            sb.AppendLine("                }");
            sb.AppendLine("                document.getElementById('studentInfoSection').style.display = 'none';");
            sb.AppendLine("                document.getElementById('examSection').style.display = 'block';");
            sb.AppendLine("                if (window.signalRConnection && typeof window.signalRConnection.invoke === 'function') {");
            sb.AppendLine("                    window.signalRConnection.invoke('JoinExamSession', examId, window.studentInfo.studentId, window.studentInfo.name, window.studentInfo.email);");
            sb.AppendLine("                }");
            sb.AppendLine("                examStarted = true;");
            sb.AppendLine("                if (antiCheatConfig.OneQuestionAtATime) {");
            sb.AppendLine("                    createNavigationBar();");
            sb.AppendLine("                    showQuestion(0);");
            sb.AppendLine("                } else {");
            sb.AppendLine("                    startTimer();");
            sb.AppendLine("                }");
            sb.AppendLine("            } catch(e) {");
            sb.AppendLine("                console.error('Google Sign-In error:', e);");
            sb.AppendLine("                alert('Sign-in failed. Please try again.');");
            sb.AppendLine("            }");
            sb.AppendLine("        }");
            sb.AppendLine();
        }
        
        // Start exam function (for guest login)
        sb.AppendLine("        function startExam() {");
        sb.AppendLine("            const studentId = document.getElementById('studentId');");
        sb.AppendLine("            const studentName = document.getElementById('studentName');");
        sb.AppendLine("            const yearSection = document.getElementById('yearSection');");
        sb.AppendLine("            window.studentInfo = {");
        sb.AppendLine("                studentId: studentId ? studentId.value.trim() : '',");
        sb.AppendLine("                name: studentName ? studentName.value.trim() : '',");
        sb.AppendLine("                yearSection: yearSection ? yearSection.value.trim() : ''");
        sb.AppendLine("            };");
            sb.AppendLine("            if (!window.studentInfo.studentId && !window.studentInfo.name) {");
            sb.AppendLine("                alert('Please enter your information.');");
            sb.AppendLine("                return;");
            sb.AppendLine("            }");
            sb.AppendLine("            if (!checkRetakeAttempts()) {");
            sb.AppendLine("                return;");
            sb.AppendLine("            }");
            sb.AppendLine("            console.log('Student info captured:', window.studentInfo);");
        sb.AppendLine("            const savedSession = checkForSavedSession();");
        sb.AppendLine("            if (savedSession && confirm('A previous session was found. Do you want to resume where you left off?')) {");
        sb.AppendLine("                restoreSession(savedSession);");
        sb.AppendLine("                return;");
        sb.AppendLine("            }");
        sb.AppendLine("            document.getElementById('studentInfoSection').style.display = 'none';");
        sb.AppendLine("            document.getElementById('examSection').style.display = 'block';");
        sb.AppendLine("            if (window.signalRConnection && typeof window.signalRConnection.invoke === 'function') {");
        sb.AppendLine("                window.signalRConnection.invoke('JoinExamSession', examId, window.studentInfo.studentId, window.studentInfo.name, window.studentInfo.email || '');");
        sb.AppendLine("            }");
        sb.AppendLine("            examStarted = true;");
        sb.AppendLine("            if (antiCheatConfig.OneQuestionAtATime) {");
        sb.AppendLine("                createNavigationBar();");
        sb.AppendLine("                showQuestion(0);");
        sb.AppendLine("            } else {");
        sb.AppendLine("                startTimer();");
        sb.AppendLine("            }");
        sb.AppendLine("        }");
        sb.AppendLine();
        
        // Retake attempts validation
        sb.AppendLine("        // Retake attempts validation");
        sb.AppendLine("        const RETAKE_KEY = 'examRetakes_' + examId;");
        sb.AppendLine();
        sb.AppendLine("        function checkRetakeAttempts() {");
        sb.AppendLine("            if (!antiCheatConfig.LimitRetakeAttempts) return true;");
        sb.AppendLine("            try {");
        sb.AppendLine("                const maxAttempts = parseInt(antiCheatConfig.RetakeAttemptsValue) || 3;");
        sb.AppendLine("                if (maxAttempts === 0) return true;");
        sb.AppendLine("                const retakeData = localStorage.getItem(RETAKE_KEY);");
        sb.AppendLine("                if (!retakeData) {");
        sb.AppendLine("                    const newData = { studentId: window.studentInfo.studentId || window.studentInfo.name, attempts: 1, lastAttempt: new Date().toISOString() };");
        sb.AppendLine("                    localStorage.setItem(RETAKE_KEY, JSON.stringify(newData));");
        sb.AppendLine("                    console.log('[Retake] First attempt recorded');");
        sb.AppendLine("                    return true;");
        sb.AppendLine("                }");
        sb.AppendLine("                const data = JSON.parse(retakeData);");
        sb.AppendLine("                const currentStudent = window.studentInfo.studentId || window.studentInfo.name;");
        sb.AppendLine("                if (data.studentId !== currentStudent) {");
        sb.AppendLine("                    const newData = { studentId: currentStudent, attempts: 1, lastAttempt: new Date().toISOString() };");
        sb.AppendLine("                    localStorage.setItem(RETAKE_KEY, JSON.stringify(newData));");
        sb.AppendLine("                    console.log('[Retake] New student, attempt 1 recorded');");
        sb.AppendLine("                    return true;");
        sb.AppendLine("                }");
        sb.AppendLine("                if (data.attempts >= maxAttempts) {");
        sb.AppendLine("                    alert('You have exceeded the maximum number of retake attempts (' + maxAttempts + ') for this exam.');");
        sb.AppendLine("                    console.warn('[Retake] Max attempts exceeded:', data.attempts, '/', maxAttempts);");
        sb.AppendLine("                    reportViolation('RETAKE_LIMIT_EXCEEDED', { attempts: data.attempts, max: maxAttempts });");
        sb.AppendLine("                    return false;");
        sb.AppendLine("                }");
        sb.AppendLine("                data.attempts++;");
        sb.AppendLine("                data.lastAttempt = new Date().toISOString();");
        sb.AppendLine("                localStorage.setItem(RETAKE_KEY, JSON.stringify(data));");
        sb.AppendLine("                console.log('[Retake] Attempt', data.attempts, 'of', maxAttempts);");
        sb.AppendLine("                reportViolation('RETAKE_ATTEMPT', { attempts: data.attempts, max: maxAttempts });");
        sb.AppendLine("                return true;");
        sb.AppendLine("            } catch(e) { console.error('[Retake] Check failed:', e); return true; }");
        sb.AppendLine("        }");
        sb.AppendLine();
        
        // Timer functions
        sb.AppendLine("        function updateTimer() {");
        sb.AppendLine("            const hours = Math.floor(timeRemaining / 3600);");
        sb.AppendLine("            const minutes = Math.floor((timeRemaining % 3600) / 60);");
        sb.AppendLine("            const seconds = timeRemaining % 60;");
        sb.AppendLine("            document.getElementById('time').textContent = ");
        sb.AppendLine("                String(hours).padStart(2, '0') + ':' +");
        sb.AppendLine("                String(minutes).padStart(2, '0') + ':' +");
        sb.AppendLine("                String(seconds).padStart(2, '0');");
        sb.AppendLine("            if (timeRemaining <= 0) {");
        sb.AppendLine("                clearInterval(timerInterval);");
        sb.AppendLine("                autoSubmitExam();");
        sb.AppendLine("                return;");
        sb.AppendLine("            }");
        sb.AppendLine("            if (timeRemaining === 300) {");
        sb.AppendLine("                alert('Warning: 5 minutes remaining!');");
        sb.AppendLine("            }");
        sb.AppendLine("            timeRemaining--;");
        sb.AppendLine("            if (antiCheatConfig.AutoResumeSession && timeRemaining % 30 === 0) {");
        sb.AppendLine("                saveSessionProgress();");
        sb.AppendLine("            }");
        sb.AppendLine("        }");
        sb.AppendLine();
        sb.AppendLine("        function startTimer() {");
        sb.AppendLine("            if (!examStarted) return;");
        sb.AppendLine("            updateTimer();");
        sb.AppendLine("            timerInterval = setInterval(updateTimer, 1000);");
        sb.AppendLine("        }");
        sb.AppendLine();
        
        // Tab detection
        sb.AppendLine("        document.addEventListener('visibilitychange', function() {");
        sb.AppendLine("            if (!examStarted || !antiCheatConfig.DetectTabbing) return;");
        sb.AppendLine("            if (document.hidden) {");
        sb.AppendLine("                tabSwitchCount++;");
        sb.AppendLine("                console.warn('[Anti-Cheat] Tab switch detected. Count:', tabSwitchCount);");
        sb.AppendLine("                reportViolation('TAB_SWITCH', { count: tabSwitchCount, timestamp: new Date().toISOString() });");
        sb.AppendLine("                if (antiCheatConfig.DeductPoints) {");
        sb.AppendLine("                    const points = parseInt(antiCheatConfig.DeductPointsValue) || 0;");
        sb.AppendLine("                    deductedPoints += points;");
        sb.AppendLine("                    console.warn('[Anti-Cheat] Points deducted:', points, 'Total deducted:', deductedPoints);");
        sb.AppendLine("                }");
        sb.AppendLine("                if (antiCheatConfig.AutoSubmit && tabSwitchCount >= 3) {");
        sb.AppendLine("                    alert('You have switched tabs too many times. Your exam will be submitted automatically.');");
        sb.AppendLine("                    autoSubmitExam();");
        sb.AppendLine("                    return;");
        sb.AppendLine("                }");
        sb.AppendLine("                if (antiCheatConfig.WarningOnly) {");
        sb.AppendLine("                    alert('Warning: Tab switching is being monitored. Count: ' + tabSwitchCount);");
        sb.AppendLine("                }");
        sb.AppendLine("            }");
        sb.AppendLine("        });");
        sb.AppendLine();
        
        // Copy/Paste prevention
        sb.AppendLine("        if (antiCheatConfig.DisableCopyPaste) {");
        sb.AppendLine("            document.addEventListener('copy', function(e) {");
        sb.AppendLine("                if (examStarted) {");
        sb.AppendLine("                    e.preventDefault();");
        sb.AppendLine("                    copyPasteAttempts++;");
        sb.AppendLine("                    reportViolation('COPY_ATTEMPT', { count: copyPasteAttempts });");
        sb.AppendLine("                    alert('Copying is disabled during the exam.');");
        sb.AppendLine("                }");
        sb.AppendLine("            });");
        sb.AppendLine("            document.addEventListener('paste', function(e) {");
        sb.AppendLine("                if (examStarted) {");
        sb.AppendLine("                    e.preventDefault();");
        sb.AppendLine("                    copyPasteAttempts++;");
        sb.AppendLine("                    reportViolation('PASTE_ATTEMPT', { count: copyPasteAttempts });");
        sb.AppendLine("                    alert('Pasting is disabled during the exam.');");
        sb.AppendLine("                }");
        sb.AppendLine("            });");
        sb.AppendLine("        }");
        sb.AppendLine();
        
        // Screenshot prevention
        sb.AppendLine("        if (antiCheatConfig.DisableScreenshot) {");
        sb.AppendLine("            document.addEventListener('keyup', function(e) {");
        sb.AppendLine("                if (examStarted && (e.key === 'PrintScreen' || e.keyCode === 44)) {");
        sb.AppendLine("                    screenshotAttempts++;");
        sb.AppendLine("                    reportViolation('SCREENSHOT_ATTEMPT', { count: screenshotAttempts });");
        sb.AppendLine("                    alert('Screenshot/PrintScreen is disabled during the exam.');");
        sb.AppendLine("                }");
        sb.AppendLine("            });");
        sb.AppendLine("        }");
        sb.AppendLine();
        
        // One question at a time functionality
        if (antiCheat?.OneQuestionAtATime == true)
        {
            var totalQuestions = examData.Contents.Count;
            sb.AppendLine($"        const totalQuestions = {totalQuestions};");
            sb.AppendLine("        let currentQuestionIndex = 0;");
            sb.AppendLine("        let questionStartTime = null;");
            sb.AppendLine("        let questionTimerInterval = null;");
            sb.AppendLine("        let questionTimeRemaining = 0;");
            sb.AppendLine();
            
            // Create navigation bar HTML dynamically (moved to after instructions)
            sb.AppendLine("        function createNavigationBar() {");
            sb.AppendLine("            const navBar = document.createElement('div');");
            sb.AppendLine("            navBar.id = 'navigationBar';");
            sb.AppendLine("            navBar.style.cssText = 'background: #ffffff; padding: 20px; border-bottom: 3px solid #3498db; display: flex; justify-content: space-between; align-items: center; flex-wrap: wrap; gap: 15px;';");
            sb.AppendLine("            navBar.innerHTML = `");
            sb.AppendLine("                <div style='display: flex; gap: 15px; align-items: center; flex-wrap: wrap;'>");
            sb.AppendLine("                    <button id='prevBtn' onclick='previousQuestion()' style='background: linear-gradient(135deg, #667eea 0%, #764ba2 100%); color: white; border: none; padding: 12px 30px; font-size: 16px; font-weight: bold; border-radius: 8px; cursor: pointer; box-shadow: 0 3px 10px rgba(102, 126, 234, 0.3); transition: all 0.3s ease;'>");
            sb.AppendLine("                        ← Previous");
            sb.AppendLine("                    </button>");
            sb.AppendLine("                    <button id='nextBtn' onclick='nextQuestion()' style='background: linear-gradient(135deg, #667eea 0%, #764ba2 100%); color: white; border: none; padding: 12px 30px; font-size: 16px; font-weight: bold; border-radius: 8px; cursor: pointer; box-shadow: 0 3px 10px rgba(102, 126, 234, 0.3); transition: all 0.3s ease;'>");
            sb.AppendLine("                        Next →");
            sb.AppendLine("                    </button>");
            sb.AppendLine("                    <span id='questionCounter' style='font-size: 18px; font-weight: bold; color: #2c3e50;'>Question 1/${totalQuestions}</span>");
            if (antiCheat?.TimeLimitPerQuestion == true)
            {
                sb.AppendLine("                    <span id='questionTimer' style='font-size: 18px; font-weight: bold; color: #e74c3c; margin-left: 20px;'>Time: " + (antiCheat?.TimeLimitValue ?? "60") + "s</span>");
            }
            sb.AppendLine("                </div>");
            sb.AppendLine("            `;");
            sb.AppendLine("            const examInstructions = document.querySelector('.exam-instructions');");
            sb.AppendLine("            examInstructions.parentNode.insertBefore(navBar, examInstructions.nextSibling);");
            sb.AppendLine("        }");
            sb.AppendLine();
            
            // Show question function
            sb.AppendLine("        function showQuestion(index) {");
            sb.AppendLine("            const questions = document.querySelectorAll('.question-container');");
            sb.AppendLine("            questions.forEach((q, i) => {");
            sb.AppendLine("                q.style.display = i === index ? 'block' : 'none';");
            sb.AppendLine("            });");
            sb.AppendLine("            document.getElementById('questionCounter').textContent = 'Question ' + (index + 1) + '/' + totalQuestions;");
            sb.AppendLine("            const prevBtn = document.getElementById('prevBtn');");
            sb.AppendLine("            const nextBtn = document.getElementById('nextBtn');");
            sb.AppendLine("            const submitContainer = document.querySelector('.submit-container');");
            sb.AppendLine("            if (index === totalQuestions - 1) {");
            sb.AppendLine("                if (submitContainer) submitContainer.style.display = 'block';");
            sb.AppendLine("            } else {");
            sb.AppendLine("                if (submitContainer) submitContainer.style.display = 'none';");
            sb.AppendLine("            }");
            if (antiCheat?.DisableBacktrack == true)
            {
                sb.AppendLine("            prevBtn.disabled = true;");
                sb.AppendLine("            prevBtn.style.opacity = '0.5';");
                sb.AppendLine("            prevBtn.style.cursor = 'not-allowed';");
            }
            else
            {
                sb.AppendLine("            prevBtn.disabled = index === 0;");
                sb.AppendLine("            prevBtn.style.opacity = index === 0 ? '0.5' : '1';");
                sb.AppendLine("            prevBtn.style.cursor = index === 0 ? 'not-allowed' : 'pointer';");
            }
            sb.AppendLine("            nextBtn.disabled = index === totalQuestions - 1;");
            sb.AppendLine("            nextBtn.style.opacity = index === totalQuestions - 1 ? '0.5' : '1';");
            sb.AppendLine("            nextBtn.style.cursor = index === totalQuestions - 1 ? 'not-allowed' : 'pointer';");
            
            if (antiCheat?.TimeLimitPerQuestion == true)
            {
                sb.AppendLine("            startQuestionTimer();");
            }
            sb.AppendLine("        }");
            sb.AppendLine();
            
            // Navigation functions
            sb.AppendLine("        function nextQuestion() {");
            sb.AppendLine("            if (currentQuestionIndex < totalQuestions - 1) {");
            if (antiCheat?.TimeLimitPerQuestion == true)
            {
                sb.AppendLine("                stopQuestionTimer();");
            }
            sb.AppendLine("                currentQuestionIndex++;");
            sb.AppendLine("                showQuestion(currentQuestionIndex);");
            sb.AppendLine("                saveSessionProgress();");
            sb.AppendLine("            }");
            sb.AppendLine("        }");
            sb.AppendLine();
            sb.AppendLine("        function previousQuestion() {");
            if (antiCheat?.DisableBacktrack == true)
            {
                sb.AppendLine("            return;");
            }
            else
            {
                sb.AppendLine("            if (currentQuestionIndex > 0) {");
                if (antiCheat?.TimeLimitPerQuestion == true)
                {
                    sb.AppendLine("                stopQuestionTimer();");
                }
                sb.AppendLine("                currentQuestionIndex--;");
                sb.AppendLine("                showQuestion(currentQuestionIndex);");
                sb.AppendLine("                saveSessionProgress();");
                sb.AppendLine("            }");
            }
            sb.AppendLine("        }");
            sb.AppendLine();
            
            // Per-question timer (only if enabled)
            if (antiCheat?.TimeLimitPerQuestion == true)
            {
                var timeLimit = int.TryParse(antiCheat?.TimeLimitValue, out var limit) ? limit : 60;
                sb.AppendLine("        function startQuestionTimer() {");
                sb.AppendLine("            if (!examStarted) return;");
                sb.AppendLine($"            questionTimeRemaining = {timeLimit};");
                sb.AppendLine("            questionStartTime = Date.now();");
                sb.AppendLine("            updateQuestionTimer();");
                sb.AppendLine("            if (questionTimerInterval) clearInterval(questionTimerInterval);");
                sb.AppendLine("            questionTimerInterval = setInterval(updateQuestionTimer, 1000);");
                sb.AppendLine("        }");
                sb.AppendLine();
                sb.AppendLine("        function stopQuestionTimer() {");
                sb.AppendLine("            if (questionTimerInterval) {");
                sb.AppendLine("                clearInterval(questionTimerInterval);");
                sb.AppendLine("                questionTimerInterval = null;");
                sb.AppendLine("            }");
                sb.AppendLine("        }");
                sb.AppendLine();
                sb.AppendLine("        function updateQuestionTimer() {");
                sb.AppendLine("            const timerEl = document.getElementById('questionTimer');");
                sb.AppendLine("            if (!timerEl) return;");
                sb.AppendLine("            timerEl.textContent = 'Time: ' + questionTimeRemaining + 's';");
                sb.AppendLine("            if (questionTimeRemaining <= 10) {");
                sb.AppendLine("                timerEl.style.color = '#e74c3c';");
                sb.AppendLine("                timerEl.style.fontWeight = 'bold';");
                sb.AppendLine("            } else {");
                sb.AppendLine("                timerEl.style.color = '#2c3e50';");
                sb.AppendLine("            }");
                sb.AppendLine("            if (questionTimeRemaining <= 0) {");
                sb.AppendLine("                stopQuestionTimer();");
                sb.AppendLine("                if (currentQuestionIndex < totalQuestions - 1) {");
                sb.AppendLine("                    alert('Time is up for this question! Moving to next question.');");
                sb.AppendLine("                    nextQuestion();");
                sb.AppendLine("                } else {");
                sb.AppendLine("                    alert('Time is up for the last question!');");
                sb.AppendLine("                }");
                sb.AppendLine("                return;");
                sb.AppendLine("            }");
                sb.AppendLine("            questionTimeRemaining--;");
                sb.AppendLine("        }");
                sb.AppendLine();
            }
        }
        
        // Submit functions
        sb.AppendLine("        function submitExam(event) {");
        sb.AppendLine("            event.preventDefault();");
        sb.AppendLine("            if (!confirm('Are you sure you want to submit your exam? This action cannot be undone.')) {");
        sb.AppendLine("                return;");
        sb.AppendLine("            }");
        sb.AppendLine("            clearInterval(timerInterval);");
        if (antiCheat?.OneQuestionAtATime == true && antiCheat?.TimeLimitPerQuestion == true)
        {
            sb.AppendLine("            if (questionTimerInterval) clearInterval(questionTimerInterval);");
        }
        sb.AppendLine("            const formData = new FormData(event.target);");
        sb.AppendLine("            const answers = {};");
        sb.AppendLine("            for (let [key, value] of formData.entries()) {");
        sb.AppendLine("                answers[key] = value;");
        sb.AppendLine("            }");
        sb.AppendLine("            localStorage.removeItem(SESSION_SAVE_KEY);");
        sb.AppendLine("            sendToApi(answers);");
        sb.AppendLine("        }");
        sb.AppendLine();
        sb.AppendLine("        function autoSubmitExam() {");
        sb.AppendLine("            alert('Time is up! Your exam will be submitted automatically.');");
        sb.AppendLine("            const form = document.getElementById('examForm');");
        sb.AppendLine("            const formData = new FormData(form);");
        sb.AppendLine("            const answers = {};");
        sb.AppendLine("            for (let [key, value] of formData.entries()) {");
        sb.AppendLine("                answers[key] = value;");
        sb.AppendLine("            }");
        sb.AppendLine("            localStorage.removeItem(SESSION_SAVE_KEY);");
        sb.AppendLine("            sendToApi(answers);");
        sb.AppendLine("        }");
        sb.AppendLine();
        
        // Submit to API
        sb.AppendLine("        async function sendToApi(answers) {");
        sb.AppendLine("            const submission = {");
        sb.AppendLine("                examId: examId,");
        sb.AppendLine("                answers: answers,");
        sb.AppendLine("                submittedAt: new Date().toISOString(),");
        sb.AppendLine("                timeSpent: (examDuration * 60 - timeRemaining),");
        sb.AppendLine("                studentInfo: window.studentInfo || {},");
        sb.AppendLine("                tabSwitchCount: tabSwitchCount,");
        sb.AppendLine("                deductedPoints: deductedPoints,");
        sb.AppendLine("                screenshotAttempts: screenshotAttempts,");
        sb.AppendLine("                copyPasteAttempts: copyPasteAttempts");
        sb.AppendLine("            };");
        sb.AppendLine("            try {");
        sb.AppendLine("                const response = await fetch(apiEndpoint, {");
        sb.AppendLine("                    method: 'POST',");
        sb.AppendLine("                    headers: { 'Content-Type': 'application/json' },");
        sb.AppendLine("                    body: JSON.stringify(submission)");
        sb.AppendLine("                });");
        sb.AppendLine("                if (response.ok) {");
        sb.AppendLine("                    const studentName = (window.studentInfo && window.studentInfo.name) ? window.studentInfo.name : 'Student';");
        sb.AppendLine("                    document.body.innerHTML = '<div style=\"display: flex; justify-content: center; align-items: center; height: 100vh; flex-direction: column; color: white; text-align: center; padding: 20px;\"><h1 style=\"font-size: clamp(24px, 5vw, 48px);\">✓ Exam Submitted Successfully!</h1><p style=\"font-size: clamp(16px, 3vw, 24px); margin-top: 20px;\">Thank you, ' + studentName + '!</p><p style=\"font-size: clamp(14px, 2.5vw, 18px); margin-top: 10px;\">Your answers have been recorded.</p></div>';");
        sb.AppendLine("                    if (window.signalRConnection && typeof window.signalRConnection.invoke === 'function') {");
        sb.AppendLine("                        window.signalRConnection.invoke('LeaveExamSession', examId, window.studentInfo.studentId || '', window.studentInfo.name || '');");
        sb.AppendLine("                    }");
        sb.AppendLine("                } else {");
        sb.AppendLine("                    const error = await response.json();");
        sb.AppendLine("                    alert('Error submitting exam: ' + (error.message || 'Unknown error') + '. Please contact your instructor.');");
        sb.AppendLine("                    console.error('Submission error:', error);");
        sb.AppendLine("                }");
        sb.AppendLine("            } catch (error) {");
        sb.AppendLine("                console.error('Submission error:', error);");
        sb.AppendLine("                alert('Error submitting exam. Please contact your instructor.');");
        sb.AppendLine("            }");
        sb.AppendLine("        }");
        sb.AppendLine();
        
        // Initialize on page load
        sb.AppendLine("        window.addEventListener('load', function() {");
        sb.AppendLine("            console.log('[Exam] Page loaded, initializing...');");
        if (antiCheat?.OneQuestionAtATime == true)
        {
            sb.AppendLine("            const submitContainer = document.querySelector('.submit-container');");
            sb.AppendLine("            if (submitContainer) submitContainer.style.display = 'none';");
            sb.AppendLine("            if (antiCheatConfig.OneQuestionAtATime && document.getElementById('examSection').style.display !== 'none') {");
            sb.AppendLine("                createNavigationBar();");
            sb.AppendLine("                showQuestion(0);");
            sb.AppendLine("            }");
        }
        sb.AppendLine("        });");
        
        return sb.ToString();
    }
}