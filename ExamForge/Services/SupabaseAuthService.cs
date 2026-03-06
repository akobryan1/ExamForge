using System;
using System.Net.Http;
using System.Net.Http.Headers;
using System.Net.Http.Json;
using System.Text;
using System.Text.Json;
using System.Threading.Tasks;
using Supabase;
using Supabase.Gotrue;
using Supabase.Gotrue.Interfaces;
// (no admin HTTP calls from client)

namespace ExamForge.Services
{
    /// <summary>
    /// Supabase authentication service for user login, sign-up, and password recovery
    /// </summary>
    public class SupabaseAuthService
    {
        private readonly Supabase.Client _supabase;
        // Supabase configuration
        private const string SupabaseUrl = "https://poscguejitgziwppvmaa.supabase.co";
        private const string SupabasePublishableKey = "sb_publishable_BkPl7IllMvQXkp2AjQCI6A_uijmz04B";
        // Use anon/public key for client operations only.
        // This shit temp.
        private const string SupabaseKey = "eyJhbGciOiJIUzI1NiIsInR5cCI6IkpXVCJ9.eyJpc3MiOiJzdXBhYmFzZSIsInJlZiI6InBvc2NndWVqaXRneml3cHB2bWFhIiwicm9sZSI6ImFub24iLCJpYXQiOjE3NzE1MDY1MTIsImV4cCI6MjA4NzA4MjUxMn0.9Xg4iESEBhfaaGwq4omfPB4lGp1HKDoKCBLbyA9lwwY";

        public Session? CurrentSession { get; private set; }
        public User? CurrentUser { get; private set; }
        public string? UserId => CurrentUser?.Id;
        public string? Email => CurrentUser?.Email;

        public SupabaseAuthService()
        {
            var options = new SupabaseOptions
            {
                AutoRefreshToken = true,
                AutoConnectRealtime = false
            };

            _supabase = new Supabase.Client(SupabaseUrl, SupabaseKey, options);
        }

        /// <summary>
        /// Initialize the Supabase client
        /// </summary>
        public async Task InitializeAsync()
        {
            await _supabase.InitializeAsync();
        }

        /// <summary>
        /// Sign up a new user
        /// </summary>
        public async Task<(bool Success, string Message)> SignUpAsync(string email, string password, string username)
        {
            try
            {
                // Check if username already exists
                var existingUser = await CheckUsernameExistsAsync(username);
                if (existingUser)
                {
                    return (false, "Username already exists");
                }

                // Create user via Supabase Auth (client). This assumes email confirmation is disabled in Supabase settings.
                var signupSession = await _supabase.Auth.SignUp(email, password);

                // Try to sign in immediately to obtain a session/user
                Session? session = null;
                try
                {
                    session = await _supabase.Auth.SignIn(email, password);
                }
                catch (Supabase.Gotrue.Exceptions.GotrueException gex)
                {
                    var gmsg = $"SignIn after sign-up threw GotrueException: {gex.Message}";
                    System.Diagnostics.Debug.WriteLine(gmsg);
                    // Attempt auto-confirmation if service role key is available (server/admin key)
                    var autoConfirmed = await TryAutoConfirmUserByEmailAsync(email);
                    if (autoConfirmed)
                    {
                        // Try signing in again
                        try
                        {
                            session = await _supabase.Auth.SignIn(email, password);
                        }
                        catch
                        {
                            return (false, gmsg + " - Auto-confirm attempted but sign-in still failed.");
                        }
                    }
                    else
                    {
                        return (false, gmsg + " - Ensure email confirmation is disabled in Supabase Auth settings or provide a service role key in SUPABASE_SERVICE_ROLE_KEY environment variable to auto-confirm users.");
                    }
                }
                if (session?.User == null)
                {
                    return (false, "Failed to sign in after account creation");
                }

                // Insert user profile into examforge_users table
                var userId = session.User.Id;
                await InsertUserProfileAsync(userId, username, email);

                CurrentSession = session;
                CurrentUser = session.User;

                return (true, "Account created successfully!");
            }
            catch (Exception ex)
            {
                return (false, $"Sign-up failed: {ex.Message}");
            }
        }

        /// <summary>
        /// Attempts to auto-confirm a newly-created user using the Supabase Admin API.
        /// Requires a service role key set in the environment variable SUPABASE_SERVICE_ROLE_KEY.
        /// </summary>
        private async Task<bool> TryAutoConfirmUserByEmailAsync(string email)
        {
            try
            {
                // Call a trusted backend endpoint to perform admin confirmation.
                // Set ADMIN_CONFIRM_URL environment variable to the backend base URL (e.g. https://my-backend.example.com)
                var adminBase = Environment.GetEnvironmentVariable("https://examforge-signalr.onrender.com");
                if (string.IsNullOrWhiteSpace(adminBase))
                {
                    System.Diagnostics.Debug.WriteLine("Auto-confirm: https://examforge-signalr.onrender.com not set");
                    return false;
                }

                var endpoint = new Uri(new Uri(adminBase), "/auth/admin/confirm");
                using var client = new HttpClient();
                var payload = new { Email = email };
                var resp = await client.PostAsJsonAsync(endpoint, payload);
                if (!resp.IsSuccessStatusCode)
                {
                    var err = await resp.Content.ReadAsStringAsync();
                    System.Diagnostics.Debug.WriteLine($"Auto-confirm backend failed: {resp.StatusCode} - {err}");
                    return false;
                }

                System.Diagnostics.Debug.WriteLine($"Auto-confirm backend succeeded for {email}");
                return true;
            }
            catch (Exception ex)
            {
                System.Diagnostics.Debug.WriteLine($"Auto-confirm backend exception: {ex.Message}");
                return false;
            }
        }

        /// <summary>
        /// Sign in an existing user
        /// </summary>
        public async Task<(bool Success, string Message)> SignInAsync(string usernameOrEmail, string password)
        {
            try
            {
                // Check if input is email or username
                string email;
                if (usernameOrEmail.Contains("@"))
                {
                    email = usernameOrEmail;
                }
                else
                {
                    // Look up email by username
                    email = await GetEmailByUsernameAsync(usernameOrEmail);
                    if (string.IsNullOrEmpty(email))
                    {
                        return (false, "Username not found");
                    }
                }

                // Sign in with email and password
                var session = await _supabase.Auth.SignIn(email, password);
                
                if (session?.User == null)
                {
                    return (false, "Invalid credentials");
                }

                CurrentSession = session;
                CurrentUser = session.User;

                return (true, "Sign in successful!");
            }
            catch (Exception ex)
            {
                return (false, $"Sign-in failed: {ex.Message}");
            }
        }

        /// <summary>
        /// Send password reset email
        /// </summary>
        public async Task<(bool Success, string Message)> ResetPasswordAsync(string username, string email)
        {
            try
            {
                // Verify username and email match
                var storedEmail = await GetEmailByUsernameAsync(username);
                if (string.IsNullOrEmpty(storedEmail))
                {
                    return (false, "Username not found");
                }

                if (!storedEmail.Equals(email, StringComparison.OrdinalIgnoreCase))
                {
                    return (false, "Username and email do not match");
                }

                // Send password reset email
                await _supabase.Auth.ResetPasswordForEmail(email);

                return (true, $"Password reset email sent to {email}");
            }
            catch (Exception ex)
            {
                return (false, $"Password reset failed: {ex.Message}");
            }
        }

        /// <summary>
        /// Sign out the current user
        /// </summary>
        public async Task SignOutAsync()
        {
            try
            {
                await _supabase.Auth.SignOut();
                CurrentSession = null;
                CurrentUser = null;
            }
            catch (Exception ex)
            {
                System.Diagnostics.Debug.WriteLine($"Sign-out error: {ex.Message}");
            }
        }

        /// <summary>
        /// Check if a username already exists
        /// </summary>
        private async Task<bool> CheckUsernameExistsAsync(string username)
        {
            try
            {
                var response = await _supabase
                    .From<ExamForgeUser>()
                    .Select("username")
                    .Filter("username", Supabase.Postgrest.Constants.Operator.Equals, username)
                    .Get();

                return response?.Models?.Count > 0;
            }
            catch
            {
                return false;
            }
        }

        /// <summary>
        /// Get email by username
        /// </summary>
        private async Task<string> GetEmailByUsernameAsync(string username)
        {
            try
            {
                var response = await _supabase
                    .From<ExamForgeUser>()
                    .Select("email")
                    .Filter("username", Supabase.Postgrest.Constants.Operator.Equals, username)
                    .Single();

                return response?.Email ?? "";
            }
            catch
            {
                return "";
            }
        }

        /// <summary>
        /// Get username by user ID (public method for UI)
        /// </summary>
        public async Task<string> GetUsernameByUserIdAsync(string userId)
        {
            try
            {
                var response = await _supabase
                    .From<ExamForgeUser>()
                    .Select("username")
                    .Filter("userid", Supabase.Postgrest.Constants.Operator.Equals, userId)
                    .Single();

                return response?.Username ?? "";
            }
            catch
            {
                return "";
            }
        }

        /// <summary>
        /// Insert user profile into examforge_users table
        /// </summary>
        private async Task InsertUserProfileAsync(string userId, string username, string email)
        {
            try
            {
                var user = new ExamForgeUser
                {
                    UserId = userId,
                    Username = username,
                    Email = email,
                    CreatedAt = DateTime.UtcNow
                };

                await _supabase.From<ExamForgeUser>().Insert(user);
                // Initialize per-user Firestore collections (do not block sign-up if this fails)
                try
                {
                    var firestore = new FirestoreService(userId);
                    await firestore.InitializeUserCollectionsAsync();
                }
                catch (Exception ex)
                {
                    System.Diagnostics.Debug.WriteLine($"Failed to initialize Firestore collections for user {userId}: {ex.Message}");
                }
            }
            catch (Exception ex)
            {
                System.Diagnostics.Debug.WriteLine($"Failed to insert user profile: {ex.Message}");
                throw;
            }
        }

    }

    /// <summary>
    /// Model for examforge_users table
    /// </summary>
    [Supabase.Postgrest.Attributes.Table("examforge_users")]
    public class ExamForgeUser : Supabase.Postgrest.Models.BaseModel
    {
        [Supabase.Postgrest.Attributes.PrimaryKey("userid", true)]
        [Supabase.Postgrest.Attributes.Column("userid")]
        public string UserId { get; set; } = "";

        [Supabase.Postgrest.Attributes.Column("username")]
        public string Username { get; set; } = "";

        [Supabase.Postgrest.Attributes.Column("email")]
        public string Email { get; set; } = "";

        [Supabase.Postgrest.Attributes.Column("created_at")]
        public DateTime CreatedAt { get; set; }
    }
}
