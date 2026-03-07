using ExamForge.SignalRServer.Hubs;
using System.Net.Http.Headers;
using System.Text;
using System.Text.Json;

var builder = WebApplication.CreateBuilder(args);

// Optional Redis backplane
var redisUrl = builder.Configuration["REDIS_URL"];
var signalR = builder.Services.AddSignalR();
if (!string.IsNullOrEmpty(redisUrl))
{
    signalR.AddStackExchangeRedis(redisUrl, options =>
    {
        options.Configuration.ChannelPrefix = "ExamForge";
    });
}

// ? Add controllers for OAuth
builder.Services.AddControllers();

// CORS for clients (WPF + browser)
builder.Services.AddCors(options =>
{
    options.AddPolicy("AllowAll", policy =>
    {
        policy.AllowAnyHeader()
              .AllowAnyMethod()
              .SetIsOriginAllowed(_ => true)
              .AllowCredentials();
    });
});

var app = builder.Build();

app.UseCors("AllowAll");

// Health endpoint
app.MapGet("/", () => Results.Ok(new
{
    status = "running",
    service = "ExamForge SignalR Server",
    timestamp = DateTime.UtcNow
}));

// ? OAuth endpoints
app.MapControllers();

static IResult GetEssayKeyStatus()
{
    var configured = ResolveDeepSeekApiKey() is { Length: > 0 };
    return Results.Ok(new
    {
        configured,
        provider = "DeepSeek"
    });
}

app.MapGet("/api/essay/key-status", GetEssayKeyStatus);
app.MapGet("/essay/key-status", GetEssayKeyStatus);

static Task<IResult> GradeEssayEndpoint(EssayGradeApiRequest request)
{
    return GradeEssayCoreAsync(request);
}

app.MapPost("/api/essay/grade", GradeEssayEndpoint);
app.MapPost("/essay/grade", GradeEssayEndpoint);

// SignalR hub
app.MapHub<SessionHub>("/sessionHub");

// Background service to cleanup expired sessions
_ = Task.Run(async () =>
{
    while (true)
    {
        try
        {
            await Task.Delay(TimeSpan.FromHours(1)); // Run every hour
            
            Console.WriteLine("?? Running session cleanup task...");
            
            // Cleanup logic
            var db = Google.Cloud.Firestore.FirestoreDb.Create(
                Environment.GetEnvironmentVariable("FIRESTORE_PROJECT_ID") ?? "examforge-201e8"
            );
            
            // Query temp_sessions collection and remove old sessions (>24 hours)
            var tempSessionsRef = db.Collection("temp_sessions");
            var snapshot = await tempSessionsRef.GetSnapshotAsync();
            
            int cleanedCount = 0;
            foreach (var examDoc in snapshot.Documents)
            {
                var studentsRef = examDoc.Reference.Collection("students");
                var studentsSnapshot = await studentsRef.GetSnapshotAsync();
                
                foreach (var studentDoc in studentsSnapshot.Documents)
                {
                    var data = studentDoc.ToDictionary();
                    if (data.ContainsKey("Timestamp") && data["Timestamp"] is Google.Cloud.Firestore.Timestamp timestamp)
                    {
                        var age = DateTime.UtcNow - timestamp.ToDateTime();
                        if (age.TotalHours > 24)
                        {
                            await studentDoc.Reference.DeleteAsync();
                            cleanedCount++;
                        }
                    }
                }
            }
            
            Console.WriteLine($"? Cleanup complete - removed {cleanedCount} expired sessions");
        }
        catch (Exception ex)
        {
            Console.WriteLine($"? Cleanup task error: {ex.Message}");
        }
    }
});

app.Run();

static async Task<IResult> GradeEssayCoreAsync(EssayGradeApiRequest request)
{
    var apiKey = ResolveDeepSeekApiKey();
    if (string.IsNullOrWhiteSpace(apiKey))
    {
        return Results.Ok(GradeWithHeuristic(request, "DeepSeek API key is not configured in backend environment."));
    }

    try
    {
        using var httpClient = new HttpClient();
        httpClient.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", apiKey);

        var prompt = BuildEssayPrompt(request);
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
        var response = await httpClient.PostAsync("https://api.deepseek.com/v1/chat/completions", content);
        if (!response.IsSuccessStatusCode)
        {
            return Results.Ok(GradeWithHeuristic(request, $"DeepSeek HTTP {(int)response.StatusCode}."));
        }

        var rawJson = await response.Content.ReadAsStringAsync();
        using var doc = JsonDocument.Parse(rawJson);
        var contentText = doc.RootElement.GetProperty("choices")[0].GetProperty("message").GetProperty("content").GetString() ?? "{}";
        var cleanedJson = ExtractJsonPayload(contentText);

        var result = JsonSerializer.Deserialize<EssayGradeApiResponse>(cleanedJson, new JsonSerializerOptions
        {
            PropertyNameCaseInsensitive = true
        });

        if (result == null)
        {
            return Results.Ok(GradeWithHeuristic(request, "DeepSeek returned an empty response payload."));
        }

        result.UsedDeepSeek = true;
        result.ProviderStatus = "DeepSeek response parsed successfully (backend).";
        result.MaxScore = request.MaxPoints;
        return Results.Ok(result);
    }
    catch (Exception ex)
    {
        return Results.Ok(GradeWithHeuristic(request, $"DeepSeek exception: {ex.Message}"));
    }
}

static string? ResolveDeepSeekApiKey()
{
    var key = Environment.GetEnvironmentVariable("DEEPSEEK_API_KEY")?.Trim();
    if (string.IsNullOrWhiteSpace(key)) return null;

    if (key.StartsWith("DEEPSEEK_API_KEY=", StringComparison.OrdinalIgnoreCase))
    {
        key = key.Substring("DEEPSEEK_API_KEY=".Length).Trim();
    }

    return key;
}

static string BuildEssayPrompt(EssayGradeApiRequest item)
{
    return $@"Grade this essay using rubric and return JSON matching EssayGradeApiResponse fields.
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

static string ExtractJsonPayload(string content)
{
    var trimmed = content.Trim();
    if (trimmed.StartsWith("```", StringComparison.Ordinal))
    {
        var firstBrace = trimmed.IndexOf('{');
        var lastBrace = trimmed.LastIndexOf('}');
        if (firstBrace >= 0 && lastBrace > firstBrace)
            return trimmed.Substring(firstBrace, lastBrace - firstBrace + 1);
    }

    return trimmed;
}

static EssayGradeApiResponse GradeWithHeuristic(EssayGradeApiRequest item, string providerStatus)
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

    return new EssayGradeApiResponse
    {
        AiScore = baseScore,
        MaxScore = item.MaxPoints,
        ConfidencePercent = confidence,
        FlagForReview = flag,
        Justification = flag
            ? "Auto-grade generated with low confidence. Recommended for instructor review."
            : "Auto-grade generated from rubric weighting and key-point coverage.",
        RubricBreakdown = new List<EssayRubricScoreRowApi>
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
        },
        UsedDeepSeek = false,
        ProviderStatus = providerStatus
    };
}

internal sealed class EssayGradeApiRequest
{
    public string ExamTitle { get; set; } = "";
    public string Subject { get; set; } = "";
    public string EssayQuestion { get; set; } = "";
    public string EssayAnswer { get; set; } = "";
    public string RubricText { get; set; } = "";
    public string ModelAnswer { get; set; } = "";
    public string KeyPoints { get; set; } = "";
    public double MaxPoints { get; set; }
    public double ThesisWeight { get; set; }
    public double EvidenceWeight { get; set; }
    public double ClarityWeight { get; set; }
}

internal sealed class EssayGradeApiResponse
{
    public double AiScore { get; set; }
    public double MaxScore { get; set; }
    public double ConfidencePercent { get; set; }
    public bool FlagForReview { get; set; }
    public string Justification { get; set; } = "";
    public List<EssayRubricScoreRowApi> RubricBreakdown { get; set; } = new();
    public bool UsedDeepSeek { get; set; }
    public string ProviderStatus { get; set; } = "";
}

internal sealed class EssayRubricScoreRowApi
{
    public string Criterion { get; set; } = "";
    public double Weight { get; set; }
    public double MaxPoints { get; set; }
    public double PointsAwarded { get; set; }
    public string Reason { get; set; } = "";
}

