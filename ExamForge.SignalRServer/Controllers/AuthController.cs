using Microsoft.AspNetCore.Mvc;
using System.IdentityModel.Tokens.Jwt;
using Microsoft.IdentityModel.Tokens;
using System.Security.Claims;
using System.Text;
using System.Collections.Concurrent;

namespace ExamForge.SignalRServer.Controllers;

[ApiController]
[Route("auth")]
public class AuthController : ControllerBase
{
    // In-memory store for auth requests (use Redis/database in production)
    private static readonly ConcurrentDictionary<string, AuthRequest> _authRequests = new();
    
    private readonly IConfiguration _configuration;
    private readonly ILogger<AuthController> _logger;

    public AuthController(IConfiguration configuration, ILogger<AuthController> logger)
    {
        _configuration = configuration;
        _logger = logger;
    }

    [HttpPost("google/start")]
    public IActionResult StartGoogleAuth()
    {
        try
        {
            var clientId = _configuration["Google:ClientId"];
            var clientSecret = _configuration["Google:ClientSecret"];
            var redirectUri = _configuration["Google:RedirectUri"];

            if (string.IsNullOrEmpty(clientId) || string.IsNullOrEmpty(clientSecret))
            {
                return BadRequest(new { error = "Google OAuth not configured" });
            }

            // Generate unique request ID
            var requestId = Guid.NewGuid().ToString();
            var state = Guid.NewGuid().ToString();

            // Store request
            _authRequests[requestId] = new AuthRequest
            {
                RequestId = requestId,
                State = state,
                CreatedAt = DateTime.UtcNow,
                Status = "pending"
            };

            // Build Google OAuth URL
            var authUrl = $"https://accounts.google.com/o/oauth2/v2/auth?" +
                $"client_id={Uri.EscapeDataString(clientId)}&" +
                $"redirect_uri={Uri.EscapeDataString(redirectUri)}&" +
                $"response_type=code&" +
                $"scope={Uri.EscapeDataString("openid email profile")}&" +
                $"state={state}&" +
                $"access_type=offline";

            return Ok(new { requestId, authUrl });
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error starting Google auth");
            return StatusCode(500, new { error = "Failed to start authentication" });
        }
    }

    [HttpGet("google/callback")]
    public async Task<IActionResult> GoogleCallback([FromQuery] string code, [FromQuery] string state)
    {
        try
        {
            // Find request by state
            var request = _authRequests.Values.FirstOrDefault(r => r.State == state);
            if (request == null)
            {
                return BadRequest("Invalid state parameter");
            }

            var clientId = _configuration["Google:ClientId"];
            var clientSecret = _configuration["Google:ClientSecret"];
            var redirectUri = _configuration["Google:RedirectUri"];

            // Exchange code for tokens
            using var httpClient = new HttpClient();
            var tokenRequest = new FormUrlEncodedContent(new[]
            {
                new KeyValuePair<string, string>("code", code),
                new KeyValuePair<string, string>("client_id", clientId!),
                new KeyValuePair<string, string>("client_secret", clientSecret!),
                new KeyValuePair<string, string>("redirect_uri", redirectUri!),
                new KeyValuePair<string, string>("grant_type", "authorization_code")
            });

            var tokenResponse = await httpClient.PostAsync("https://oauth2.googleapis.com/token", tokenRequest);
            var tokenJson = await tokenResponse.Content.ReadAsStringAsync();
            var tokens = System.Text.Json.JsonSerializer.Deserialize<GoogleTokenResponse>(tokenJson);

            if (tokens?.access_token == null)
            {
                request.Status = "error";
                request.Error = "Failed to get access token";
                return BadRequest("Failed to obtain tokens");
            }

            // Get user info
            httpClient.DefaultRequestHeaders.Authorization = 
                new System.Net.Http.Headers.AuthenticationHeaderValue("Bearer", tokens.access_token);
            var userInfoResponse = await httpClient.GetStringAsync("https://www.googleapis.com/oauth2/v2/userinfo");
            var userInfo = System.Text.Json.JsonSerializer.Deserialize<GoogleUserInfo>(userInfoResponse);

            if (userInfo == null)
            {
                request.Status = "error";
                request.Error = "Failed to get user info";
                return BadRequest("Failed to get user information");
            }

            // Generate JWT for our app
            var jwt = GenerateJwt(userInfo);

            // Update request with success
            request.Status = "success";
            request.Token = jwt;
            request.Profile = new AuthProfile
            {
                UserId = userInfo.id ?? Guid.NewGuid().ToString(),
                Email = userInfo.email ?? "",
                DisplayName = userInfo.name ?? "User"
            };

            // Return success page
            return Content(@"
                <!DOCTYPE html>
                <html>
                <head>
                    <title>Sign In Successful</title>
                    <style>
                        body { font-family: Arial, sans-serif; display: flex; justify-content: center; align-items: center; height: 100vh; margin: 0; background: #f3f5f7; }
                        .container { text-align: center; background: white; padding: 40px; border-radius: 12px; box-shadow: 0 4px 12px rgba(0,0,0,0.1); }
                        .success { color: #3aae9e; font-size: 48px; margin-bottom: 20px; }
                        h1 { color: #2e2f33; margin: 0 0 10px 0; }
                        p { color: #6b7280; margin: 0; }
                    </style>
                </head>
                <body>
                    <div class='container'>
                        <div class='success'>?</div>
                        <h1>Sign In Successful!</h1>
                        <p>You can close this window and return to ExamForge.</p>
                    </div>
                </body>
                </html>
            ", "text/html");
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error in Google callback");
            return StatusCode(500, "Authentication failed");
        }
    }

    [HttpGet("google/status")]
    public IActionResult GetAuthStatus([FromQuery] string requestId)
    {
        if (!_authRequests.TryGetValue(requestId, out var request))
        {
            return NotFound(new { status = "not_found" });
        }

        // Clean up old requests (>5 minutes)
        if ((DateTime.UtcNow - request.CreatedAt).TotalMinutes > 5)
        {
            _authRequests.TryRemove(requestId, out _);
            return Ok(new { status = "expired" });
        }

        return Ok(new
        {
            status = request.Status,
            token = request.Token,
            error = request.Error,
            profile = request.Profile
        });
    }

    private string GenerateJwt(GoogleUserInfo userInfo)
    {
        var jwtSecret = _configuration["Jwt:Secret"] ?? throw new InvalidOperationException("JWT secret not configured");
        var key = new SymmetricSecurityKey(Encoding.UTF8.GetBytes(jwtSecret));
        var credentials = new SigningCredentials(key, SecurityAlgorithms.HmacSha256);

        var claims = new[]
        {
            new Claim(JwtRegisteredClaimNames.Sub, userInfo.id ?? Guid.NewGuid().ToString()),
            new Claim(JwtRegisteredClaimNames.Email, userInfo.email ?? ""),
            new Claim(JwtRegisteredClaimNames.Name, userInfo.name ?? "User"),
            new Claim(JwtRegisteredClaimNames.Jti, Guid.NewGuid().ToString())
        };

        var token = new JwtSecurityToken(
            issuer: "ExamForge",
            audience: "ExamForge.Desktop",
            claims: claims,
            expires: DateTime.UtcNow.AddDays(7),
            signingCredentials: credentials
        );

        return new JwtSecurityTokenHandler().WriteToken(token);
    }

    [HttpPost("cleanup")]
    public IActionResult CleanupExpiredRequests()
    {
        var expiredKeys = _authRequests
            .Where(kvp => (DateTime.UtcNow - kvp.Value.CreatedAt).TotalMinutes > 5)
            .Select(kvp => kvp.Key)
            .ToList();

        foreach (var key in expiredKeys)
        {
            _authRequests.TryRemove(key, out _);
        }

        return Ok(new { removed = expiredKeys.Count });
    }

    [HttpPost("admin/confirm")]
    public async Task<IActionResult> ConfirmUser([FromBody] ConfirmUserRequest req)
    {
        try
        {
            if (string.IsNullOrWhiteSpace(req?.Email))
                return BadRequest(new { error = "Email is required" });

            var serviceKey = _configuration["Supabase:ServiceRoleKey"] ?? Environment.GetEnvironmentVariable("SUPABASE_SERVICE_ROLE_KEY");
            if (string.IsNullOrWhiteSpace(serviceKey))
            {
                _logger.LogWarning("Supabase service role key not configured for admin confirm");
                return StatusCode(500, new { error = "Admin key not configured on server" });
            }

            using var http = new HttpClient { BaseAddress = new Uri("https://poscguejitgziwppvmaa.supabase.co") };
            http.DefaultRequestHeaders.Authorization = new System.Net.Http.Headers.AuthenticationHeaderValue("Bearer", serviceKey);
            http.DefaultRequestHeaders.Accept.Add(new System.Net.Http.Headers.MediaTypeWithQualityHeaderValue("application/json"));

            // Find user by email
            var getResp = await http.GetAsync($"/auth/v1/admin/users?email={Uri.EscapeDataString(req.Email)}");
            if (!getResp.IsSuccessStatusCode)
            {
                var err = await getResp.Content.ReadAsStringAsync();
                _logger.LogError("Admin confirm: failed to query user: {code} {err}", getResp.StatusCode, err);
                return StatusCode(502, new { error = "Failed to query Supabase admin API" });
            }

            var json = await getResp.Content.ReadAsStringAsync();
            using var doc = System.Text.Json.JsonDocument.Parse(json);
            if (doc.RootElement.ValueKind != System.Text.Json.JsonValueKind.Array || doc.RootElement.GetArrayLength() == 0)
                return NotFound(new { error = "User not found" });

            var userElem = doc.RootElement[0];
            if (!userElem.TryGetProperty("id", out var idProp))
                return StatusCode(502, new { error = "Unexpected response from Supabase admin API" });

            var userId = idProp.GetString();
            if (string.IsNullOrWhiteSpace(userId))
                return StatusCode(502, new { error = "Invalid user id from Supabase" });

            var payload = new { email_confirm = true, email_confirmed_at = DateTime.UtcNow.ToString("o") };
            var content = new StringContent(System.Text.Json.JsonSerializer.Serialize(payload), System.Text.Encoding.UTF8, "application/json");

            var patchReq = new HttpRequestMessage(new HttpMethod("PATCH"), $"/auth/v1/admin/users/{userId}") { Content = content };
            var patchResp = await http.SendAsync(patchReq);
            if (!patchResp.IsSuccessStatusCode)
            {
                var err = await patchResp.Content.ReadAsStringAsync();
                _logger.LogError("Admin confirm: patch failed: {code} {err}", patchResp.StatusCode, err);
                return StatusCode(502, new { error = "Failed to update user via Supabase admin API" });
            }

            return Ok(new { success = true, userId });
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error in admin confirm");
            return StatusCode(500, new { error = "Internal error" });
        }
    }
}

public class ConfirmUserRequest { public string Email { get; set; } = ""; }

// DTOs
public class AuthRequest
{
    public string RequestId { get; set; } = "";
    public string State { get; set; } = "";
    public DateTime CreatedAt { get; set; }
    public string Status { get; set; } = "pending";
    public string? Token { get; set; }
    public string? Error { get; set; }
    public AuthProfile? Profile { get; set; }
}

public class AuthProfile
{
    public string UserId { get; set; } = "";
    public string Email { get; set; } = "";
    public string? DisplayName { get; set; }
}

public class GoogleTokenResponse
{
    public string? access_token { get; set; }
    public string? refresh_token { get; set; }
    public int expires_in { get; set; }
    public string? token_type { get; set; }
}

public class GoogleUserInfo
{
    public string? id { get; set; }
    public string? email { get; set; }
    public string? name { get; set; }
    public string? picture { get; set; }
}
