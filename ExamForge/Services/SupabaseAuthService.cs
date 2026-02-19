using System;
using System.Net.Http;
using System.Net.Http;
using System.Net.Http.Headers;
using System.Text;
using System.Text.Json;
using System.Threading.Tasks;
using Supabase;
using Supabase.Gotrue;
using Supabase.Gotrue.Interfaces;

namespace ExamForge.Services
{
    /// <summary>
    /// Supabase authentication service for user login, sign-up, and password recovery
    /// </summary>
    public class SupabaseAuthService
    {
        private readonly Supabase.Client _supabase;
        private const string SupabaseUrl = "https://fachmhcsfxjtgifexbyd.supabase.co";
        private const string SupabaseKey = "sb_secret_YG49vmI3ig96mdgWdVuJaw_-2jpzHob";

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

                // Admin-create user with email confirmed (uses service role key)
                var created = await AdminCreateUserAsync(email, password, username);
                if (!created)
                {
                    return (false, "Failed to create account (admin create)");
                }

                // Sign in immediately (email already confirmed)
                var session = await _supabase.Auth.SignIn(email, password);
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
            }
            catch (Exception ex)
            {
                System.Diagnostics.Debug.WriteLine($"Failed to insert user profile: {ex.Message}");
                throw;
            }
        }

        /// <summary>
        /// Admin-create a user with email confirmed using Supabase service role key
        /// </summary>
        private async Task<bool> AdminCreateUserAsync(string email, string password, string username)
        {
            try
            {
                using var client = new HttpClient();
                var url = $"{SupabaseUrl}/auth/v1/admin/users";

                client.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", SupabaseKey);
                client.DefaultRequestHeaders.Add("apikey", SupabaseKey);

                var payload = new
                {
                    email,
                    password,
                    email_confirm = true,
                    user_metadata = new
                    {
                        username
                    }
                };

                var json = JsonSerializer.Serialize(payload);
                var content = new StringContent(json, Encoding.UTF8, "application/json");
                var response = await client.PostAsync(url, content);

                if (response.IsSuccessStatusCode)
                {
                    return true;
                }

                var body = await response.Content.ReadAsStringAsync();
                System.Diagnostics.Debug.WriteLine($"AdminCreateUser failed: {response.StatusCode} - {body}");
                return false;
            }
            catch (Exception ex)
            {
                System.Diagnostics.Debug.WriteLine($"AdminCreateUser exception: {ex.Message}");
                return false;
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
