using Google.Apis.Auth.OAuth2;
using Google.Apis.Auth.OAuth2.Flows;
using Google.Apis.Auth.OAuth2.Responses;
using Google.Apis.Util.Store;
using System;
using System.Threading;
using System.Threading.Tasks;

namespace ExamForge.Services
{
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

        public async Task<bool> SignInWithGoogleAsync()
        {
            try
            {
                // Google OAuth client configuration
                var clientSecrets = new ClientSecrets
                {
                    ClientId = "106820965441856797095-YOUR_CLIENT_ID_SUFFIX.apps.googleusercontent.com",
                    ClientSecret = "YOUR_CLIENT_SECRET_FROM_GOOGLE_CLOUD_CONSOLE"
                };

                // Request Google OAuth with email and profile scopes
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
                    // Get user info from token
                    var token = _credential.Token;
                    _userId = await GetUserIdFromTokenAsync(token);
                    _email = await GetEmailFromTokenAsync(token);
                    _displayName = await GetDisplayNameFromTokenAsync(token);

                    return true;
                }

                return false;
            }
            catch (Exception ex)
            {
                System.Diagnostics.Debug.WriteLine($"Sign-in error: {ex.Message}");
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
                return json.RootElement.GetProperty("id").GetString() ?? "";
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
                return json.RootElement.GetProperty("name").GetString() ?? "";
            }
            catch
            {
                return "";
            }
        }

        public void SignOut()
        {
            _credential = null;
            _userId = null;
            _email = null;
            _displayName = null;
        }
    }
}
