using ExamForge.Models;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Net.Http;
using System.Net.Http.Headers;
using System.Text;
using System.Text.Json;
using System.Threading.Tasks;

namespace ExamForge.Services;

public class EssayAutoGradingService
{
    private const string DeepSeekApiKey = "DEEPSEEK_API_KEY";
    private const string DeepSeekEndpoint = "https://api.deepseek.com/v1/chat/completions";

    public async Task<EssayAutoGradeResult> GradeEssayAsync(EssayCheckerQueueItem item)
    {
        var apiKey = ResolveApiKey();

        if (string.IsNullOrWhiteSpace(apiKey) || apiKey.Contains("DEEPSEEK_API_KEY", StringComparison.Ordinal))
        {
            return GradeWithHeuristic(item, "DeepSeek key is missing or still placeholder.");
        }

        try
        {
            using var httpClient = new HttpClient();
            httpClient.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", apiKey);

            var prompt = BuildPrompt(item);
            var payload = new
            {
                model = "deepseek-chat",
                temperature = 0.2,
                messages = new[]
                {
                    new { role = "system", content = "You are an essay grading assistant. Return valid JSON only." },
                    new { role = "user", content = prompt }
                }
            };

            var content = new StringContent(JsonSerializer.Serialize(payload), Encoding.UTF8, "application/json");
            var response = await httpClient.PostAsync(DeepSeekEndpoint, content);
            if (!response.IsSuccessStatusCode)
            {
                System.Diagnostics.Debug.WriteLine($"DeepSeek request failed: {(int)response.StatusCode} {response.ReasonPhrase}");
                return GradeWithHeuristic(item, $"DeepSeek HTTP {(int)response.StatusCode}.");
            }

            var rawJson = await response.Content.ReadAsStringAsync();
            var aiJson = ExtractAiContent(rawJson);
            var result = JsonSerializer.Deserialize<EssayAutoGradeResult>(aiJson, new JsonSerializerOptions
            {
                PropertyNameCaseInsensitive = true
            });

            if (result == null)
            {
                return GradeWithHeuristic(item, "DeepSeek returned an empty response payload.");
            }

            result.UsedDeepSeek = true;
            result.ProviderStatus = "DeepSeek response parsed successfully.";
            return result;
        }
        catch (Exception ex)
        {
            System.Diagnostics.Debug.WriteLine($"DeepSeek grading failed, using heuristic fallback: {ex.Message}");
            return GradeWithHeuristic(item, $"DeepSeek exception: {ex.Message}");
        }
    }

    private static string ResolveApiKey()
    {
        var key = Environment.GetEnvironmentVariable("DEEPSEEK_API_KEY");
        if (string.IsNullOrWhiteSpace(key))
            key = DeepSeekApiKey;

        key = key.Trim();

        // Support accidental "DEEPSEEK_API_KEY=..." format in config.
        if (key.StartsWith("DEEPSEEK_API_KEY=", StringComparison.OrdinalIgnoreCase))
        {
            key = key.Substring("DEEPSEEK_API_KEY=".Length).Trim();
        }

        return key;
    }

    private static string BuildPrompt(EssayCheckerQueueItem item)
    {
        return $@"Grade this essay using rubric and return JSON matching EssayAutoGradeResult fields.
Exam: {item.ExamTitle}
Subject: {item.Subject}
Question: {item.EssayQuestion}
Rubric: {item.RubricText}
ModelAnswer: {item.ModelAnswer}
KeyPoints: {item.KeyPoints}
Weights: Thesis={item.ThesisWeight}, Evidence={item.EvidenceWeight}, Clarity={item.ClarityWeight}
StudentAnswer: {item.EssayAnswer}
MaxPoints: {item.MaxPoints}";
    }

    private static string ExtractAiContent(string rawResponse)
    {
        using var doc = JsonDocument.Parse(rawResponse);
        var root = doc.RootElement;
        var content = root.GetProperty("choices")[0].GetProperty("message").GetProperty("content").GetString();
        return content ?? "{}";
    }

    private static EssayAutoGradeResult GradeWithHeuristic(EssayCheckerQueueItem item, string providerStatus)
    {
        var keyPoints = (item.KeyPoints ?? string.Empty)
            .Split(new[] { '\n', ',', ';' }, StringSplitOptions.RemoveEmptyEntries)
            .Select(p => p.Trim().ToLowerInvariant())
            .Where(p => !string.IsNullOrWhiteSpace(p))
            .Distinct()
            .ToList();

        var answerLower = (item.EssayAnswer ?? string.Empty).ToLowerInvariant();
        var hitCount = keyPoints.Count == 0 ? 0 : keyPoints.Count(k => answerLower.Contains(k));
        var coverage = keyPoints.Count == 0 ? 0.6 : (double)hitCount / keyPoints.Count;

        var baseScore = Math.Round(Math.Clamp(coverage, 0, 1) * item.MaxPoints, 2);
        var confidence = Math.Round(55 + (coverage * 35), 1);
        var flag = confidence < 70 || answerLower.Length < 80;

        var breakdown = new List<EssayRubricScoreRow>
        {
            new()
            {
                Criterion = "Thesis",
                Weight = item.ThesisWeight,
                MaxPoints = Math.Round(item.MaxPoints * (item.ThesisWeight / 100.0), 2),
                PointsAwarded = Math.Round(baseScore * (item.ThesisWeight / 100.0), 2),
                Reason = "Evaluated for central argument clarity against provided prompt and rubric."
            },
            new()
            {
                Criterion = "Evidence",
                Weight = item.EvidenceWeight,
                MaxPoints = Math.Round(item.MaxPoints * (item.EvidenceWeight / 100.0), 2),
                PointsAwarded = Math.Round(baseScore * (item.EvidenceWeight / 100.0), 2),
                Reason = keyPoints.Count == 0
                    ? "No explicit key points were provided; evidence scored using generic completeness."
                    : $"Matched {hitCount}/{keyPoints.Count} key points in the response."
            },
            new()
            {
                Criterion = "Clarity",
                Weight = item.ClarityWeight,
                MaxPoints = Math.Round(item.MaxPoints * (item.ClarityWeight / 100.0), 2),
                PointsAwarded = Math.Round(baseScore * (item.ClarityWeight / 100.0), 2),
                Reason = "Assessed coherence, sentence flow, and overall response organization."
            }
        };

        return new EssayAutoGradeResult
        {
            AiScore = baseScore,
            MaxScore = item.MaxPoints,
            ConfidencePercent = confidence,
            FlagForReview = flag,
            Justification = flag
                ? "Auto-grade generated with low confidence. Recommended for instructor review."
                : "Auto-grade generated from rubric weighting and key-point coverage.",
            RubricBreakdown = breakdown,
            UsedDeepSeek = false,
            ProviderStatus = providerStatus
        };
    }
}
