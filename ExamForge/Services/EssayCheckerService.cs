using System;
using System.Collections.Generic;
using System.Linq;
using System.Net.Http;
using System.Text;
using System.Text.Json;
using System.Threading.Tasks;
using ExamForge.Models;

namespace ExamForge.Services
{
    /// <summary>
    /// Service that evaluates student essays using an LLM API.
    /// Uses C# HttpClient for direct API calls — the recommended approach for this .NET WPF application.
    /// 
    /// API Invocation Recommendation:
    ///   Among curl, Python, and Node.js, **Node.js** is the best fit for this repo because
    ///   the project already has a Cloudflare Worker (ExamForge-Worker) using JavaScript. However,
    ///   since the WPF desktop app natively supports HttpClient, we use C# directly — avoiding
    ///   external runtime dependencies while keeping the option to move the LLM call to the
    ///   Cloudflare Worker for production (keeping API keys server-side).
    /// </summary>
    public class EssayCheckerService
    {
        private static readonly HttpClient _httpClient = new();
        private string _apiKey;
        private string _apiEndpoint;
        private string _modelName;

        public EssayCheckerService()
        {
            _apiKey = "";
            _apiEndpoint = "https://api.openai.com/v1/chat/completions";
            _modelName = "gpt-4o-mini";

            LoadConfiguration();
        }

        private void LoadConfiguration()
        {
            try
            {
                var configPath = System.IO.Path.Combine(AppDomain.CurrentDomain.BaseDirectory, "appsettings.json");
                if (System.IO.File.Exists(configPath))
                {
                    var json = System.IO.File.ReadAllText(configPath);
                    using var doc = JsonDocument.Parse(json);
                    if (doc.RootElement.TryGetProperty("EssayChecker", out var essayConfig))
                    {
                        if (essayConfig.TryGetProperty("ApiKey", out var apiKey))
                            _apiKey = apiKey.GetString() ?? "";
                        if (essayConfig.TryGetProperty("ApiEndpoint", out var endpoint))
                            _apiEndpoint = endpoint.GetString() ?? _apiEndpoint;
                        if (essayConfig.TryGetProperty("Model", out var model))
                            _modelName = model.GetString() ?? _modelName;
                    }
                }
            }
            catch (Exception ex)
            {
                System.Diagnostics.Debug.WriteLine($"Error loading Essay Checker config: {ex.Message}");
            }
        }

        /// <summary>
        /// Sets the API key at runtime (e.g., from a UI input field).
        /// </summary>
        public void SetApiKey(string apiKey)
        {
            _apiKey = apiKey;
        }

        /// <summary>
        /// Sets the API endpoint at runtime.
        /// </summary>
        public void SetApiEndpoint(string endpoint)
        {
            _apiEndpoint = endpoint;
        }

        /// <summary>
        /// Sets the model name at runtime.
        /// </summary>
        public void SetModel(string model)
        {
            _modelName = model;
        }

        public bool IsConfigured => !string.IsNullOrWhiteSpace(_apiKey);

        /// <summary>
        /// Evaluates a student essay against a rubric using the configured LLM API.
        /// </summary>
        public async Task<EssayEvaluationResult> EvaluateEssayAsync(
            string essayText,
            string questionPrompt,
            Rubric rubric)
        {
            if (string.IsNullOrWhiteSpace(_apiKey))
                throw new InvalidOperationException("API key is not configured. Please set your LLM API key in Settings.");

            if (string.IsNullOrWhiteSpace(essayText))
                throw new ArgumentException("Essay text cannot be empty.", nameof(essayText));

            var systemPrompt = BuildSystemPrompt(rubric);
            var userPrompt = BuildUserPrompt(essayText, questionPrompt, rubric);

            var requestBody = new
            {
                model = _modelName,
                messages = new[]
                {
                    new { role = "system", content = systemPrompt },
                    new { role = "user", content = userPrompt }
                },
                temperature = 0.3,
                max_tokens = 2000
            };

            var jsonContent = JsonSerializer.Serialize(requestBody);
            var request = new HttpRequestMessage(HttpMethod.Post, _apiEndpoint)
            {
                Content = new StringContent(jsonContent, Encoding.UTF8, "application/json")
            };
            request.Headers.Add("Authorization", $"Bearer {_apiKey}");

            var response = await _httpClient.SendAsync(request);
            var responseContent = await response.Content.ReadAsStringAsync();

            if (!response.IsSuccessStatusCode)
            {
                throw new HttpRequestException($"LLM API returned {response.StatusCode}: {responseContent}");
            }

            return ParseLlmResponse(responseContent, rubric);
        }

        private string BuildSystemPrompt(Rubric rubric)
        {
            return @"You are an expert academic essay grader. You evaluate student essays strictly according to the provided rubric.
You must return your evaluation as valid JSON with the following structure:
{
  ""totalScore"": <number>,
  ""letterGrade"": ""<A/B/C/D/F>"",
  ""criterionScores"": [
    {
      ""criterionName"": ""<name>"",
      ""score"": <number>,
      ""maxPoints"": <number>,
      ""level"": ""<Excellent/Good/Fair/Poor>"",
      ""feedback"": ""<specific feedback for this criterion>""
    }
  ],
  ""overallFeedback"": ""<2-3 sentence summary>"",
  ""strengths"": [""<strength 1>"", ""<strength 2>""],
  ""areasForImprovement"": [""<area 1>"", ""<area 2>""]
}
Return ONLY the JSON object, no additional text.";
        }

        private string BuildUserPrompt(string essayText, string questionPrompt, Rubric rubric)
        {
            var sb = new StringBuilder();
            sb.AppendLine("Please evaluate the following student essay using the rubric below.");
            sb.AppendLine();

            if (!string.IsNullOrWhiteSpace(questionPrompt))
            {
                sb.AppendLine("=== ESSAY PROMPT ===");
                sb.AppendLine(questionPrompt);
                sb.AppendLine();
            }

            sb.AppendLine("=== RUBRIC ===");
            sb.AppendLine(rubric.ToPromptText());
            sb.AppendLine();

            sb.AppendLine("=== STUDENT ESSAY ===");
            sb.AppendLine(essayText);

            return sb.ToString();
        }

        private EssayEvaluationResult ParseLlmResponse(string responseJson, Rubric rubric)
        {
            using var doc = JsonDocument.Parse(responseJson);
            var root = doc.RootElement;

            // Extract the assistant's message content
            var content = root
                .GetProperty("choices")[0]
                .GetProperty("message")
                .GetProperty("content")
                .GetString() ?? "";

            // Strip markdown code fences if present
            content = content.Trim();
            if (content.StartsWith("```"))
            {
                var firstNewline = content.IndexOf('\n');
                if (firstNewline >= 0)
                    content = content.Substring(firstNewline + 1);
                if (content.EndsWith("```"))
                    content = content.Substring(0, content.Length - 3);
                content = content.Trim();
            }

            using var evalDoc = JsonDocument.Parse(content);
            var evalRoot = evalDoc.RootElement;

            var result = new EssayEvaluationResult
            {
                TotalScore = evalRoot.GetProperty("totalScore").GetInt32(),
                MaxScore = rubric.TotalPoints,
                LetterGrade = evalRoot.GetProperty("letterGrade").GetString() ?? "",
                OverallFeedback = evalRoot.GetProperty("overallFeedback").GetString() ?? ""
            };

            if (evalRoot.TryGetProperty("criterionScores", out var scoresArray))
            {
                foreach (var item in scoresArray.EnumerateArray())
                {
                    result.CriterionScores.Add(new CriterionScore
                    {
                        CriterionName = item.GetProperty("criterionName").GetString() ?? "",
                        Score = item.GetProperty("score").GetInt32(),
                        MaxPoints = item.GetProperty("maxPoints").GetInt32(),
                        Level = item.GetProperty("level").GetString() ?? "",
                        Feedback = item.GetProperty("feedback").GetString() ?? ""
                    });
                }
            }

            if (evalRoot.TryGetProperty("strengths", out var strengths))
            {
                foreach (var s in strengths.EnumerateArray())
                    result.Strengths.Add(s.GetString() ?? "");
            }

            if (evalRoot.TryGetProperty("areasForImprovement", out var areas))
            {
                foreach (var a in areas.EnumerateArray())
                    result.AreasForImprovement.Add(a.GetString() ?? "");
            }

            return result;
        }

        /// <summary>
        /// Loads a rubric from a JSON file uploaded by the professor.
        /// </summary>
        public static Rubric? LoadRubricFromFile(string filePath)
        {
            try
            {
                var json = System.IO.File.ReadAllText(filePath);
                var options = new JsonSerializerOptions { PropertyNameCaseInsensitive = true };
                return JsonSerializer.Deserialize<Rubric>(json, options);
            }
            catch (Exception ex)
            {
                System.Diagnostics.Debug.WriteLine($"Error loading rubric from file: {ex.Message}");
                return null;
            }
        }

        /// <summary>
        /// Saves a rubric to a JSON file so the professor can share or reuse it.
        /// </summary>
        public static bool SaveRubricToFile(Rubric rubric, string filePath)
        {
            try
            {
                var options = new JsonSerializerOptions { WriteIndented = true };
                var json = JsonSerializer.Serialize(rubric, options);
                System.IO.File.WriteAllText(filePath, json);
                return true;
            }
            catch (Exception ex)
            {
                System.Diagnostics.Debug.WriteLine($"Error saving rubric to file: {ex.Message}");
                return false;
            }
        }
    }
}
