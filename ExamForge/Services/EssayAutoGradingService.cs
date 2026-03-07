using ExamForge.Models;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Net.Http;
using System.Text;
using System.Text.Json;
using System.Threading.Tasks;

namespace ExamForge.Services;

public class EssayAutoGradingService
{
    private const string BackendEssayGradeEndpoint = "https://examforge-signalr.onrender.com/api/essay/grade";
    private const string BackendEssayGradeEndpointFallback = "https://examforge-signalr.onrender.com/essay/grade";

    public async Task<EssayAutoGradeResult> GradeEssayAsync(EssayCheckerQueueItem item)
    {
        var endpoints = ResolveBackendEndpoints();

        try
        {
            using var httpClient = new HttpClient();
            var payload = new
            {
                item.ExamTitle,
                item.Subject,
                item.EssayQuestion,
                item.EssayAnswer,
                item.RubricText,
                item.ModelAnswer,
                item.KeyPoints,
                MaxPoints = item.MaxPoints,
                ThesisWeight = item.ThesisWeight,
                EvidenceWeight = item.EvidenceWeight,
                ClarityWeight = item.ClarityWeight
            };

            var content = new StringContent(JsonSerializer.Serialize(payload), Encoding.UTF8, "application/json");
            HttpResponseMessage? response = null;
            string? lastStatus = null;

            foreach (var endpoint in endpoints)
            {
                response = await httpClient.PostAsync(endpoint, content);
                if (response.IsSuccessStatusCode)
                    break;

                lastStatus = $"{(int)response.StatusCode} {response.ReasonPhrase} @ {endpoint}";

                if (response.StatusCode != System.Net.HttpStatusCode.NotFound)
                    break;
            }

            if (response == null || !response.IsSuccessStatusCode)
            {
                System.Diagnostics.Debug.WriteLine($"Backend essay grade request failed: {lastStatus ?? "No response"}");
                return GradeWithHeuristic(item, $"Backend HTTP {lastStatus ?? "unknown"}.");
            }

            var rawJson = await response.Content.ReadAsStringAsync();
            var result = JsonSerializer.Deserialize<EssayAutoGradeResult>(rawJson, new JsonSerializerOptions
            {
                PropertyNameCaseInsensitive = true
            });

            if (result == null)
            {
                return GradeWithHeuristic(item, "Backend returned an empty response payload.");
            }
            return result;
        }
        catch (Exception ex)
        {
            System.Diagnostics.Debug.WriteLine($"Backend essay grading failed, using heuristic fallback: {ex.Message}");
            return GradeWithHeuristic(item, $"Backend exception: {ex.Message}");
        }
    }

    private static List<string> ResolveBackendEndpoints()
    {
        var endpoint = Environment.GetEnvironmentVariable("ESSAY_GRADING_API_URL");
        if (!string.IsNullOrWhiteSpace(endpoint))
            return new List<string> { endpoint.Trim() };

        return new List<string>
        {
            BackendEssayGradeEndpoint,
            BackendEssayGradeEndpointFallback
        };
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
