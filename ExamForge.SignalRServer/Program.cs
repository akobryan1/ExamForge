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

// SignalR hub
app.MapHub<SessionHub>("/sessionHub");

app.Run();

record WeatherForecast(DateOnly Date, int TemperatureC, string? Summary)
{
    public int TemperatureF => 32 + (int)(TemperatureC / 0.5556);
}
