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
            ["AutoResumeSession"] = antiCheat?.AutoResumeSession ?? false
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
        sb.AppendLine("                    if (typeof currentQuestionIndex !== 'undefined' && typeof showQuestion === 'function') {");
        sb.AppendLine("                        currentQuestionIndex = data.currentQuestionIndex || 0;");
        sb.AppendLine("                        showQuestion(currentQuestionIndex);");
        sb.AppendLine("                    }");
        sb.AppendLine("                    startTimer();");
        sb.AppendLine("                    setupAutoSave();");
        sb.AppendLine("                    alert('✅ Session Restored\\n\\nYour exam has been restored from ' + new Date(data.timestamp).toLocaleString() + '.\\nYou may continue where you left off.');");
        sb.AppendLine("                }, 500);");
        sb.AppendLine("            } catch(e) { console.error('[Auto-Resume] Failed:', e); alert('Failed to restore session. Starting fresh.'); }");
        sb.AppendLine("        }");
        sb.AppendLine();
        sb.AppendLine("        function setupAutoSave() {");
        sb.AppendLine("            if (!antiCheatConfig.AutoResumeSession) return;");
        sb.AppendLine("            const form = document.getElementById('examForm');");
        sb.AppendLine("            if (form) {");
        sb.AppendLine("                form.addEventListener('change', saveSessionProgress);");
        sb.AppendLine("                form.addEventListener('input', saveSessionProgress);");
        sb.AppendLine("            }");
        sb.AppendLine("            window.addEventListener('beforeunload', function() {");
        sb.AppendLine("                saveSessionProgress();");
        sb.AppendLine("                reportViolation('session_disconnected', { timeRemaining: timeRemaining, timestamp: new Date().toISOString() });");
        sb.AppendLine("            });");
        sb.AppendLine("            console.log('[Auto-Save] Setup complete - saving on answer change');");
        sb.AppendLine("        }");
        sb.AppendLine();
        sb.AppendLine("        function clearSavedSession() {");
        sb.AppendLine("            try { localStorage.removeItem(SESSION_SAVE_KEY); console.log('[Auto-Save] Session cleared'); } catch(e) {}");
        sb.AppendLine("        }");
        sb.AppendLine();
        
        // Google Sign-In Handler
        if (loginConfig?.IsGoogleSignIn == true)
        {
            sb.AppendLine("        // Handle Google Sign-In");
            sb.AppendLine("        function handleGoogleSignIn(response) {");
            sb.AppendLine("            const idToken = response.credential;");
            sb.AppendLine("            ");
            sb.AppendLine("            // Decode JWT token to get user info");
            sb.AppendLine("            const base64Url = idToken.split('.')[1];");
            sb.AppendLine("            const base64 = base64Url.replace(/-/g, '+').replace(/_/g, '/');");
            sb.AppendLine("            const jsonPayload = decodeURIComponent(atob(base64).split('').map(c => {");
            sb.AppendLine("                return '%' + ('00' + c.charCodeAt(0).toString(16)).slice(-2);");
            sb.AppendLine("            }).join(''));");
            sb.AppendLine("            ");
            sb.AppendLine("            const userInfo = JSON.parse(jsonPayload);");
            sb.AppendLine("            ");
            sb.AppendLine("            window.studentInfo = {");
            sb.AppendLine("                name: userInfo.name || '',");
            sb.AppendLine("                email: userInfo.email || '',");
            sb.AppendLine("                studentId: userInfo.email ? userInfo.email.split('@')[0] : '',");
            sb.AppendLine("                yearSection: '',");
            sb.AppendLine("                googleId: userInfo.sub || ''");
            sb.AppendLine("            };");
            sb.AppendLine("            ");
            sb.AppendLine("            console.log('Google Sign-In successful:', window.studentInfo);");
            sb.AppendLine("            document.getElementById('studentInfoSection').style.display = 'none';");
            sb.AppendLine("            document.getElementById('examSection').style.display = 'block';");
            sb.AppendLine("            startTimer();");
            sb.AppendLine("            setupAutoSave();");
            sb.AppendLine("        }");
            sb.AppendLine();
        }
        
        // Start Exam Function
        sb.AppendLine("        function startExam() {");
        
        if (loginConfig?.IsGoogleSignIn == true)
        {
            sb.AppendLine("            // Google Sign-In is handled separately via handleGoogleSignIn callback");
            sb.AppendLine("            return;");
        }
        else
        {
            sb.AppendLine("            const studentName = document.getElementById('studentName');");
            sb.AppendLine("            const studentId = document.getElementById('studentId');");
            sb.AppendLine("            const yearSection = document.getElementById('yearSection');");
            sb.AppendLine();
            sb.AppendLine("            let missingFields = [];" );
            sb.AppendLine();
            sb.AppendLine("            if (studentName && !studentName.value.trim()) {");
            sb.AppendLine("                missingFields.push('Full Name');");
            sb.AppendLine("            }");
            sb.AppendLine("            if (studentId && !studentId.value.trim()) {");
            sb.AppendLine("                missingFields.push('Student Number');");
            sb.AppendLine("            }");
            sb.AppendLine("            if (yearSection && !yearSection.value.trim()) {");
            sb.AppendLine("                missingFields.push('Year and Section');");
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
            sb.AppendLine("                email: ''");
            sb.AppendLine("            };");
            sb.AppendLine();
            sb.AppendLine("            document.getElementById('studentInfoSection').style.display = 'none';");
            sb.AppendLine("            document.getElementById('examSection').style.display = 'block';");
            sb.AppendLine("            startTimer();");
            sb.AppendLine("            setupAutoSave();");
        }
        
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
        
        // Anti-cheat: Detect tabbing
        sb.AppendLine("        // Anti-cheat: Detect tabbing and copy/paste/screenshot prevention");
        sb.AppendLine("        document.addEventListener('visibilitychange', function() {");
        sb.AppendLine("            if (!antiCheatConfig.DetectTabbing) return;");
        sb.AppendLine("            if (document.hidden) {");
        sb.AppendLine("                tabSwitchCount++;");
        sb.AppendLine("                console.warn('Tab switched. Count: ' + tabSwitchCount);");
        sb.AppendLine("                ");
        sb.AppendLine("                // Calculate deductions if enabled");
        sb.AppendLine("                if (antiCheatConfig.DeductPoints) {");
        sb.AppendLine("                    const pts = parseInt(antiCheatConfig.DeductPointsValue || '0', 10) || 0;");
        sb.AppendLine("                    deductedPoints += pts;");
        sb.AppendLine("                    console.warn('Deducted points: ' + pts + '. Total deducted: ' + deductedPoints);");
        sb.AppendLine("                }");
        sb.AppendLine("                ");
        sb.AppendLine("                // Report violation to server");
        sb.AppendLine("                reportViolation('tab_switch', {");
        sb.AppendLine("                    count: tabSwitchCount,");
        sb.AppendLine("                    deductedPoints: deductedPoints,");
        sb.AppendLine("                    timestamp: new Date().toISOString()");
        sb.AppendLine("                });");
        sb.AppendLine("                ");
        sb.AppendLine("                // Show warning if enabled");
        sb.AppendLine("                if (antiCheatConfig.WarningOnly) {");
        sb.AppendLine("                    alert('Warning: You switched away from the exam window. This incident has been recorded.');");
        sb.AppendLine("                }");
        sb.AppendLine("                ");
        sb.AppendLine("                // Auto-submit if enabled");
        sb.AppendLine("                if (antiCheatConfig.AutoSubmit) {");
        sb.AppendLine("                    alert('You switched away from the exam. The exam will be submitted automatically.');");
        sb.AppendLine("                    document.getElementById('examForm').dispatchEvent(new Event('submit'));");
        sb.AppendLine("                }");
        sb.AppendLine("            }");
        sb.AppendLine("        });");
        sb.AppendLine();
        
        // Disable copy/paste if requested
        sb.AppendLine("        if (antiCheatConfig.DisableCopyPaste) {");
        sb.AppendLine("            document.addEventListener('copy', function(e) {");
        sb.AppendLine("                e.preventDefault();");
        sb.AppendLine("                copyPasteAttempts++;");
        sb.AppendLine("                alert('Copying is disabled during the exam. This attempt has been recorded.');");
        sb.AppendLine("                reportViolation('copy_attempt', { count: copyPasteAttempts, type: 'copy', timestamp: new Date().toISOString() });");
        sb.AppendLine("            });");
        sb.AppendLine("            document.addEventListener('cut', function(e) {");
        sb.AppendLine("                e.preventDefault();");
        sb.AppendLine("                copyPasteAttempts++;");
        sb.AppendLine("                alert('Cut is disabled during the exam. This attempt has been recorded.');");
        sb.AppendLine("                reportViolation('copy_attempt', { count: copyPasteAttempts, type: 'cut', timestamp: new Date().toISOString() });");
        sb.AppendLine("            });");
        sb.AppendLine("            document.addEventListener('paste', function(e) {");
        sb.AppendLine("                e.preventDefault();");
        sb.AppendLine("                copyPasteAttempts++;");
        sb.AppendLine("                alert('Pasting is disabled during the exam. This attempt has been recorded.');");
        sb.AppendLine("                reportViolation('copy_attempt', { count: copyPasteAttempts, type: 'paste', timestamp: new Date().toISOString() });");
        sb.AppendLine("            });");
        sb.AppendLine("            document.addEventListener('contextmenu', function(e) { e.preventDefault(); });");
        sb.AppendLine("            document.addEventListener('keydown', function(e) {");
        sb.AppendLine("                if (e.ctrlKey && (e.key === 'c' || e.key === 'v' || e.key === 'x')) {");
        sb.AppendLine("                    e.preventDefault();");
        sb.AppendLine("                    copyPasteAttempts++;");
        sb.AppendLine("                    alert('Keyboard copy/paste is disabled during the exam. This attempt has been recorded.');");
        sb.AppendLine("                    reportViolation('copy_attempt', { count: copyPasteAttempts, type: 'keyboard', key: e.key, timestamp: new Date().toISOString() });");
        sb.AppendLine("                }");
        sb.AppendLine("            });");
        sb.AppendLine("        }");
        sb.AppendLine();
        
        // Disable screenshot/printscreen best-effort
        sb.AppendLine("        // Detect screenshot/PrintScreen attempts (browser cannot prevent OS-level tools)");
        sb.AppendLine("        if (antiCheatConfig.DisableScreenshot) {");
        sb.AppendLine("            document.addEventListener('keyup', function(e) {");
        sb.AppendLine("                // Detect PrintScreen key (keyCode 44 or key 'PrintScreen')");
        sb.AppendLine("                if (e.key === 'PrintScreen' || e.keyCode === 44 || e.code === 'PrintScreen') {");
        sb.AppendLine("                    screenshotAttempts++;");
        sb.AppendLine("                    console.warn('[Anti-Cheat] Screenshot attempt detected. Count: ' + screenshotAttempts);");
        sb.AppendLine("                    alert('⚠️ Screenshot Detected\\n\\nScreenshot attempts are being monitored and recorded.\\n\\nAttempt #' + screenshotAttempts + ' has been logged.');");
        sb.AppendLine("                    reportViolation('screenshot_attempt', {");
        sb.AppendLine("                        count: screenshotAttempts,");
        sb.AppendLine("                        timestamp: new Date().toISOString(),");
        sb.AppendLine("                        note: 'PrintScreen key detected - OS-level screenshots cannot be prevented'");
        sb.AppendLine("                    });");
        sb.AppendLine("                }");
        sb.AppendLine("            });");
        sb.AppendLine("            // Also detect Windows Snipping Tool shortcuts (Win+Shift+S)");
        sb.AppendLine("            document.addEventListener('keydown', function(e) {");
        sb.AppendLine("                if (e.key === 's' && e.shiftKey && (e.metaKey || e.ctrlKey)) {");
        sb.AppendLine("                    screenshotAttempts++;");
        sb.AppendLine("                    console.warn('[Anti-Cheat] Snipping tool shortcut detected. Count: ' + screenshotAttempts);");
        sb.AppendLine("                    alert('⚠️ Screenshot Tool Detected\\n\\nScreenshot tool usage is being monitored and recorded.\\n\\nAttempt #' + screenshotAttempts + ' has been logged.');");
        sb.AppendLine("                    reportViolation('screenshot_attempt', {");
        sb.AppendLine("                        count: screenshotAttempts,");
        sb.AppendLine("                        timestamp: new Date().toISOString(),");
        sb.AppendLine("                        shortcut: 'Snipping tool',");
        sb.AppendLine("                        note: 'Screenshot tool shortcut detected'");
        sb.AppendLine("                    });");
        sb.AppendLine("                }");
        sb.AppendLine("            });");
        sb.AppendLine("        }");
        sb.AppendLine();
        
        // One question at a time: navigation and per-question timer
        sb.AppendLine("        if (antiCheatConfig.OneQuestionAtATime) {");
        sb.AppendLine("            const questions = Array.from(document.querySelectorAll('.question-container'));");
        sb.AppendLine("            let currentQuestionIndex = 0;");
        sb.AppendLine("            let perQuestionTimer = null;");
        sb.AppendLine("            let perQuestionTimeRemaining = parseInt(antiCheatConfig.TimeLimitValue || '60', 10) || 60;");
        sb.AppendLine("            let perQuestionPaused = false;");
        sb.AppendLine();
        sb.AppendLine("            function startPerQuestionTimer(display, onExpire) {");
        sb.AppendLine("                if (!antiCheatConfig.TimeLimitPerQuestion) return;");
        sb.AppendLine("                if (perQuestionTimer) clearInterval(perQuestionTimer);");
        sb.AppendLine("                perQuestionTimeRemaining = parseInt(antiCheatConfig.TimeLimitValue || '60', 10) || 60;");
        sb.AppendLine("                perQuestionPaused = false;");
        sb.AppendLine("                display.textContent = perQuestionTimeRemaining + 's';");
        sb.AppendLine("                perQuestionTimer = setInterval(() => {");
        sb.AppendLine("                    if (perQuestionPaused) return;");
        sb.AppendLine("                    perQuestionTimeRemaining--;");
        sb.AppendLine("                    display.textContent = perQuestionTimeRemaining + 's';");
        sb.AppendLine("                    if (perQuestionTimeRemaining === 10) {");
        sb.AppendLine("                        display.style.color = '#b91c1c';");
        sb.AppendLine("                    }");
        sb.AppendLine("                    if (perQuestionTimeRemaining <= 0) {");
        sb.AppendLine("                        clearInterval(perQuestionTimer);");
        sb.AppendLine("                        onExpire();");
        sb.AppendLine("                    }");
        sb.AppendLine("                }, 1000);");
        sb.AppendLine("            }");
        sb.AppendLine();
        sb.AppendLine("            function showQuestion(index) {");
        sb.AppendLine("                questions.forEach((q, i) => {");
        sb.AppendLine("                    q.style.display = (i === index) ? 'block' : 'none';");
        sb.AppendLine("                });");
        sb.AppendLine("                const nav = document.getElementById('questionNav');");
        sb.AppendLine("                if (nav) {");
        sb.AppendLine("                    nav.querySelector('#qIndex').textContent = (index + 1) + '/' + questions.length;");
        sb.AppendLine("                    nav.querySelector('#prevBtn').disabled = (index === 0) || antiCheatConfig.DisableBacktrack;");
        sb.AppendLine("                    nav.querySelector('#nextBtn').disabled = (index === questions.length - 1);");
        sb.AppendLine("                }");
        sb.AppendLine("                const display = document.getElementById('perQuestionTimer');");
        sb.AppendLine("                if (display) { display.style.color = ''; }");
        sb.AppendLine("                if (antiCheatConfig.TimeLimitPerQuestion) {");
        sb.AppendLine("                    startPerQuestionTimer(display, () => {");
        sb.AppendLine("                        if (currentQuestionIndex < questions.length - 1) {");
        sb.AppendLine("                            currentQuestionIndex++;");
        sb.AppendLine("                            showQuestion(currentQuestionIndex);");
        sb.AppendLine("                        } else {");
        sb.AppendLine("                            alert('Time for this question expired. Submitting exam.');");
        sb.AppendLine("                            document.getElementById('examForm').dispatchEvent(new Event('submit'));");
        sb.AppendLine("                        }");
        sb.AppendLine("                    });");
        sb.AppendLine("                }");
        sb.AppendLine("            }");
        sb.AppendLine();
        sb.AppendLine("            const navContainer = document.createElement('div');");
        sb.AppendLine("            navContainer.id = 'questionNav';");
        sb.AppendLine("            navContainer.style.display = 'flex';");
        sb.AppendLine("            navContainer.style.justifyContent = 'space-between';");
        sb.AppendLine("            navContainer.style.alignItems = 'center';");
        sb.AppendLine("            navContainer.style.padding = '12px 20px';");
        sb.AppendLine("            navContainer.style.background = '#f3f4f6';");
        sb.AppendLine("            navContainer.style.borderBottom = '1px solid #e5e7eb';");
        sb.AppendLine("            navContainer.innerHTML = `<div style=\"display:flex;gap:8px;align-items:center\"><button id=\"prevBtn\" style=\"padding:8px 12px;border-radius:6px;\">Previous</button><button id=\"nextBtn\" style=\"padding:8px 12px;border-radius:6px;\">Next</button><span id=\"qIndex\" style=\"font-weight:600; margin-left:8px\"></span></div><div style=\"display:flex;gap:12px;align-items:center;\"><div style=\"font-size:14px;color:#374151\">Time: <span id=\"perQuestionTimer\"></span></div><button id=\"pauseBtn\" style=\"padding:6px 10px;border-radius:6px;\">Pause</button></div>`;");
        sb.AppendLine("            const examContainer = document.querySelector('.exam-container');");
        sb.AppendLine("            const examSection = document.getElementById('examSection');");
        sb.AppendLine("            if (examSection && examContainer) {");
        sb.AppendLine("                examContainer.insertBefore(navContainer, examSection);");
        sb.AppendLine("            }");
        sb.AppendLine();
        sb.AppendLine("            navContainer.querySelector('#prevBtn').addEventListener('click', () => {");
        sb.AppendLine("                if (antiCheatConfig.DisableBacktrack) return;");
        sb.AppendLine("                if (currentQuestionIndex > 0) {");
        sb.AppendLine("                    currentQuestionIndex--;");
        sb.AppendLine("                    showQuestion(currentQuestionIndex);");
        sb.AppendLine("                }");
        sb.AppendLine("            });");
        sb.AppendLine("            navContainer.querySelector('#nextBtn').addEventListener('click', () => {");
        sb.AppendLine("                if (currentQuestionIndex < questions.length - 1) {");
        sb.AppendLine("                    currentQuestionIndex++;");
        sb.AppendLine("                    showQuestion(currentQuestionIndex);");
        sb.AppendLine("                }");
        sb.AppendLine("            });");
        sb.AppendLine();
        sb.AppendLine("            navContainer.querySelector('#pauseBtn').addEventListener('click', (e) => {");
        sb.AppendLine("                perQuestionPaused = !perQuestionPaused;");
        sb.AppendLine("                e.target.textContent = perQuestionPaused ? 'Resume' : 'Pause';");
        sb.AppendLine("            });");
        sb.AppendLine();
        sb.AppendLine("            if (questions.length > 0) {");
        sb.AppendLine("                showQuestion(0);");
        sb.AppendLine("            }");
        sb.AppendLine("        }");
        sb.AppendLine();
        
        // Submit Function  
        sb.AppendLine("        async function submitExam(event) {");
        sb.AppendLine("            event.preventDefault();");
        sb.AppendLine("            console.log('Submit button clicked');");
        sb.AppendLine();
        sb.AppendLine("            // Disable submit button to prevent double submission");
        sb.AppendLine("            const submitButton = document.querySelector('.submit-button');");
        sb.AppendLine("            submitButton.disabled = true;");
        sb.AppendLine("            submitButton.textContent = 'Submitting...';");
        sb.AppendLine();
        sb.AppendLine("            try {");
        sb.AppendLine("                // Collect all answers");
        sb.AppendLine("                const formData = new FormData(event.target);");
        sb.AppendLine("                const answers = {};");
        sb.AppendLine();
        sb.AppendLine("                for (let [key, value] of formData.entries()) {");
        sb.AppendLine("                    answers[key] = value;");
        sb.AppendLine("                }");
        sb.AppendLine();
        sb.AppendLine("                const submissionData = {");
        sb.AppendLine("                    examId: examId,");
        sb.AppendLine("                    studentName: window.studentInfo.name || '',");
        sb.AppendLine("                    studentEmail: window.studentInfo.email || '',");
        sb.AppendLine("                    studentId: window.studentInfo.studentId || '',");
        sb.AppendLine("                    yearSection: window.studentInfo.yearSection || '',");
        sb.AppendLine("                    answers: answers,");
        sb.AppendLine("                    submittedAt: new Date().toISOString(),");
        sb.AppendLine("                    timeElapsed: ((examDuration * 60) - timeRemaining),");
        sb.AppendLine("                    status: 'Submitted'");
        sb.AppendLine("                };");
        sb.AppendLine();
        sb.AppendLine("                console.log('Submitting exam data:', submissionData);");
        sb.AppendLine();
        sb.AppendLine("                // Submit to API endpoint");
        sb.AppendLine("                const response = await fetch(apiEndpoint + '/api/submit', {");
        sb.AppendLine("                    method: 'POST',");
        sb.AppendLine("                    headers: {");
        sb.AppendLine("                        'Content-Type': 'application/json'");
        sb.AppendLine("                    },");
        sb.AppendLine("                    body: JSON.stringify(submissionData)");
        sb.AppendLine("                });");
        sb.AppendLine();
                sb.AppendLine("                if (response.ok) {");
                sb.AppendLine("                    const result = await response.json();");
                sb.AppendLine("                    console.log('Submission successful:', result);");
                sb.AppendLine("                    clearInterval(timerInterval);");
                sb.AppendLine("                    clearSavedSession();");
                sb.AppendLine("                    showSuccessMessage();");
                sb.AppendLine("                } else {");
        sb.AppendLine("                    const error = await response.text();");
        sb.AppendLine("                    console.error('Submission failed:', error);");
        sb.AppendLine("                    throw new Error('Submission failed: ' + error);");
        sb.AppendLine("                }");
        sb.AppendLine("            } catch (error) {");
        sb.AppendLine("                console.error('Error submitting exam:', error);");
        sb.AppendLine("                alert('Error submitting exam: ' + error.message + '\\nYour answers have been saved locally. Please contact your instructor.');");
        sb.AppendLine("                // Save to local storage as backup");
        sb.AppendLine("                localStorage.setItem('examBackup_' + examId, JSON.stringify(submissionData));");
        sb.AppendLine("            } finally {");
        sb.AppendLine("                submitButton.disabled = false;");
        sb.AppendLine("                submitButton.textContent = 'Submit Exam';");
        sb.AppendLine("            }");
        sb.AppendLine("        }");
        sb.AppendLine();
        sb.AppendLine("        function showSuccessMessage() {");
        sb.AppendLine("            document.body.innerHTML = `");
        sb.AppendLine("                <div style='display: flex; justify-content: center; align-items: center; height: 100vh; background: linear-gradient(135deg, #667eea 0%, #764ba2 100%);'>");
        sb.AppendLine("                    <div style='background: white; padding: 40px; border-radius: 15px; box-shadow: 0 10px 40px rgba(0, 0, 0, 0.2); text-align: center; max-width: 500px;'>");
        sb.AppendLine("                        <h1 style='color: #10b981; margin-bottom: 20px;'>✅ Exam Submitted Successfully!</h1>");
        sb.AppendLine("                        <p style='color: #6b7280; font-size: 18px; margin-bottom: 30px;'>Thank you for completing the exam. Your answers have been recorded.</p>");
        sb.AppendLine("                        <p style='color: #374151; font-size: 14px;'>You may now close this window.</p>");
        sb.AppendLine("                    </div>");
        sb.AppendLine("                </div>`;");
        sb.AppendLine("        }");
        sb.AppendLine();
        sb.AppendLine("        // Add event listeners");
        sb.AppendLine("        document.addEventListener('DOMContentLoaded', function() {");
        sb.AppendLine("            // Check for saved session");
        sb.AppendLine("            if (antiCheatConfig.AutoResumeSession) {");
        sb.AppendLine("                const savedSession = checkForSavedSession();");
        sb.AppendLine("                if (savedSession && confirm('Resume your previous exam session from ' + new Date(savedSession.timestamp).toLocaleString() + '?')) {");
        sb.AppendLine("                    restoreSession(savedSession);");
        sb.AppendLine("                    return;");
        sb.AppendLine("                } else if (savedSession) {");
        sb.AppendLine("                    clearSavedSession();");
        sb.AppendLine("                }");
        sb.AppendLine("            }");
        sb.AppendLine("            ");
        sb.AppendLine("            const examForm = document.getElementById('examForm');");
        sb.AppendLine("            if (examForm) {");
        sb.AppendLine("                examForm.addEventListener('submit', submitExam);");
        sb.AppendLine("            }");
        sb.AppendLine();
        sb.AppendLine("            const startButton = document.getElementById('startExamButton');");
        sb.AppendLine("            if (startButton) {");
        sb.AppendLine("                startButton.addEventListener('click', startExam);");
        sb.AppendLine("            }");
        sb.AppendLine("        });");
        
        return sb.ToString();
    }
}