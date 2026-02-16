using System;
using System.Diagnostics;
using System.Net.Http;
using System.Net.Http.Json;
using System.Threading;
using System.Threading.Tasks;

namespace ExamForge.Services
{
    /// <summary>
    /// OAuth middleman client for desktop: delegates Google auth to a backend and polls for completion.
    /// </summary>
    public class BackendAuthService
    {
        private readonly HttpClient _httpClient;
        private readonly string _baseUrl;

        public string? JwtToken { get; private set; }
        public string? UserId { get; private set; }
        public string? Email { get; private set; }
        public string? DisplayName { get; private set; }

        public BackendAuthService(HttpClient? httpClient = null, string? baseUrl = null)
        {
            _httpClient = httpClient ?? new HttpClient();
            _baseUrl = baseUrl ?? Environment.GetEnvironmentVariable("EXAMFORGE_API_BASEURL")
                ?? throw new InvalidOperationException("EXAMFORGE_API_BASEURL not set");
        }

        /// <summary>
        /// Initiates Google sign-in via backend and polls for completion. Returns true when a JWT is received.
        /// </summary>
        public async Task<bool> SignInWithGoogleAsync(CancellationToken cancellationToken = default)
        {
            // 1) Start login on backend
            var startResponse = await _httpClient.PostAsync($"{_baseUrl}/auth/google/start", null, cancellationToken);
            startResponse.EnsureSuccessStatusCode();
            var startPayload = await startResponse.Content.ReadFromJsonAsync<AuthStartResponse>(cancellationToken: cancellationToken);
            if (startPayload == null || string.IsNullOrWhiteSpace(startPayload.AuthUrl) || string.IsNullOrWhiteSpace(startPayload.RequestId))
            {
                throw new InvalidOperationException("Invalid auth start response from backend");
            }

            // 2) Open system browser for user sign-in
            Process.Start(new ProcessStartInfo
            {
                FileName = startPayload.AuthUrl,
                UseShellExecute = true
            });

            // 3) Poll backend for completion (up to ~60 seconds)
            var requestId = startPayload.RequestId;
            for (var i = 0; i < 120; i++)
            {
                cancellationToken.ThrowIfCancellationRequested();
                await Task.Delay(500, cancellationToken);

                var statusResponse = await _httpClient.GetAsync($"{_baseUrl}/auth/google/status?requestId={requestId}", cancellationToken);
                statusResponse.EnsureSuccessStatusCode();
                var status = await statusResponse.Content.ReadFromJsonAsync<AuthStatusResponse>(cancellationToken: cancellationToken);
                if (status == null) continue;

                if (string.Equals(status.Status, "success", StringComparison.OrdinalIgnoreCase) &&
                    !string.IsNullOrWhiteSpace(status.Token) && status.Profile != null)
                {
                    JwtToken = status.Token;
                    UserId = status.Profile.UserId;
                    Email = status.Profile.Email;
                    DisplayName = status.Profile.DisplayName ?? "User";
                    return true;
                }

                if (string.Equals(status.Status, "error", StringComparison.OrdinalIgnoreCase))
                {
                    throw new InvalidOperationException(status.Error ?? "Authentication failed");
                }
            }

            throw new TimeoutException("Authentication timed out.");
        }

        public void SignOut()
        {
            JwtToken = null;
            UserId = null;
            Email = null;
            DisplayName = null;
        }

        private record AuthStartResponse(string RequestId, string AuthUrl);
        private record AuthStatusResponse(string Status, string? Token, string? Error, AuthProfile? Profile);
        private record AuthProfile(string UserId, string Email, string? DisplayName);
    }
}
