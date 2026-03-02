using ExamForge.SignalRServer.Hubs;

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

