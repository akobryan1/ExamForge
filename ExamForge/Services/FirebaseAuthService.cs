using Google.Apis.Auth.OAuth2;
using Google.Apis.Auth.OAuth2.Flows;
using Google.Apis.Auth.OAuth2.Responses;
using Google.Apis.Util.Store;
using System;
using System.Threading;
using System.Threading.Tasks;

namespace ExamForge.Services
{
    /// <summary>
    /// Google OAuth Authentication Service for Desktop Apps
    /// Uses localhost redirect (standard for desktop OAuth)
    /// </summary>
    public class FirebaseAuthService
    {
        private UserCredential? _credential;
        private string? _userId;
        private string? _email;
        private string? _displayName;

        public bool IsAuthenticated => _credential != null;
        public string? UserId => _userId;
        public string? Email => _email;
        public string? DisplayName => _displayName;

        /// <summary>
        /// Sign in with Google using OAuth 2.0
        /// Opens browser ? Google auth ? Redirects to localhost ? App receives token
        /// </summary>
        public async Task<bool> SignInWithGoogleAsync()
        {
            try
            {
                // Load desktop OAuth credentials from environment (recommended) or fallback to placeholders
                // Create a Google OAuth client of type "Desktop app" in Google Cloud Console and set:
                // EXAMFORGE_GOOGLE_CLIENT_ID, EXAMFORGE_GOOGLE_CLIENT_SECRET
                var clientId = Environment.GetEnvironmentVariable("EXAMFORGE_GOOGLE_CLIENT_ID")?.Trim();
                var clientSecret = Environment.GetEnvironmentVariable("EXAMFORGE_GOOGLE_CLIENT_SECRET")?.Trim();

                if (string.IsNullOrWhiteSpace(clientId) || string.IsNullOrWhiteSpace(clientSecret))
                {
                    throw new InvalidOperationException("Google OAuth client ID/secret not set. Please create a Desktop app OAuth client in Google Cloud Console and set EXAMFORGE_GOOGLE_CLIENT_ID and EXAMFORGE_GOOGLE_CLIENT_SECRET environment variables.");
                }

                // Create OAuth client secrets (desktop app)
                var clientSecrets = new ClientSecrets
                {
                    ClientId = clientId,
                    ClientSecret = clientSecret
                };

                // Request Google OAuth with email and profile scopes
                // This will:
                // 1. Open default browser to Google sign-in
                // 2. User authenticates with Google
                // 3. Google redirects to http://localhost (user's own computer)
                // 4. App captures the auth code from localhost
                // 5. Exchanges code for access token
                _credential = await GoogleWebAuthorizationBroker.AuthorizeAsync(
                    clientSecrets,
                    new[] { 
                        "https://www.googleapis.com/auth/userinfo.email",
                        "https://www.googleapis.com/auth/userinfo.profile"
                    },
                    "user",
                    CancellationToken.None,
                    new FileDataStore("ExamForge.Auth"));

                if (_credential != null)
                {
                    // Get user info from Google
                    var token = _credential.Token;
                    _userId = await GetUserIdFromTokenAsync(token);
                    _email = await GetEmailFromTokenAsync(token);
                    _displayName = await GetDisplayNameFromTokenAsync(token);

                    System.Diagnostics.Debug.WriteLine($"? Signed in: {_email}");
                    return true;
                }

                return false;
            }
            catch (Exception ex)
            {
                System.Diagnostics.Debug.WriteLine($"? Sign-in error: {ex.Message}");
                return false;
            }
        }

        private async Task<string> GetUserIdFromTokenAsync(TokenResponse token)
        {
            try
            {
                using var client = new System.Net.Http.HttpClient();
                client.DefaultRequestHeaders.Authorization = 
                    new System.Net.Http.Headers.AuthenticationHeaderValue("Bearer", token.AccessToken);

                var response = await client.GetStringAsync("https://www.googleapis.com/oauth2/v2/userinfo");
                var json = System.Text.Json.JsonDocument.Parse(response);
                return json.RootElement.GetProperty("id").GetString() ?? Guid.NewGuid().ToString();
            }
            catch
            {
                return Guid.NewGuid().ToString();
            }
        }

        private async Task<string> GetEmailFromTokenAsync(TokenResponse token)
        {
            try
            {
                using var client = new System.Net.Http.HttpClient();
                client.DefaultRequestHeaders.Authorization = 
                    new System.Net.Http.Headers.AuthenticationHeaderValue("Bearer", token.AccessToken);

                var response = await client.GetStringAsync("https://www.googleapis.com/oauth2/v2/userinfo");
                var json = System.Text.Json.JsonDocument.Parse(response);
                return json.RootElement.GetProperty("email").GetString() ?? "";
            }
            catch
            {
                return "";
            }
        }

        private async Task<string> GetDisplayNameFromTokenAsync(TokenResponse token)
        {
            try
            {
                using var client = new System.Net.Http.HttpClient();
                client.DefaultRequestHeaders.Authorization = 
                    new System.Net.Http.Headers.AuthenticationHeaderValue("Bearer", token.AccessToken);

                var response = await client.GetStringAsync("https://www.googleapis.com/oauth2/v2/userinfo");
                var json = System.Text.Json.JsonDocument.Parse(response);
                return json.RootElement.GetProperty("name").GetString() ?? "User";
            }
            catch
            {
                return "User";
            }
        }

        /// <summary>
        /// Sign out and clear credentials
        /// </summary>
        public void SignOut()
        {
            _credential = null;
            _userId = null;
            _email = null;
            _displayName = null;
        }
    }
}

