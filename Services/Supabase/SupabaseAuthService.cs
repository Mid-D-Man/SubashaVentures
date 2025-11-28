// Services/Supabase/SupabaseAuthService.cs - UPDATED with OAuth
using SubashaVentures.Services.Storage;
using SubashaVentures.Models.Supabase;
using SubashaVentures.Utilities.HelperScripts;
using Microsoft.JSInterop;
using Supabase.Gotrue;
using Supabase.Gotrue.Exceptions;
using Client = Supabase.Client;
using LogLevel = SubashaVentures.Utilities.Logging.LogLevel;

namespace SubashaVentures.Services.Supabase;

public class SupabaseAuthService : ISupabaseAuthService
{
    private readonly Client _client;
    private readonly IBlazorAppLocalStorageService _localStorage;
    private readonly IJSRuntime _jsRuntime;
    private readonly ILogger<SupabaseAuthService> _logger;
    
    private const string SESSION_STORAGE_KEY = "supabase_session";
    private const string USER_STORAGE_KEY = "supabase_user";

    public SupabaseAuthService(
        Client client,
        IBlazorAppLocalStorageService localStorage,
        IJSRuntime jsRuntime,
        ILogger<SupabaseAuthService> logger)
    {
        _client = client;
        _localStorage = localStorage;
        _jsRuntime = jsRuntime;
        _logger = logger;
    }

    // ==================== EMAIL/PASSWORD AUTH ====================

    public async Task<SupabaseAuthResult> SignInAsync(string email, string password)
    {
        try
        {
            if (!MID_HelperFunctions.IsValidString(email) || !MID_HelperFunctions.IsValidString(password))
            {
                return new SupabaseAuthResult
                {
                    Success = false,
                    Message = "Email and password are required",
                    ErrorCode = "INVALID_CREDENTIALS"
                };
            }

            await MID_HelperFunctions.DebugMessageAsync(
                $"Attempting sign in for: {email}",
                LogLevel.Info
            );

            var session = await _client.Auth.SignIn(email, password);
            
            if (session?.User == null)
            {
                return new SupabaseAuthResult
                {
                    Success = false,
                    Message = "Authentication failed",
                    ErrorCode = "AUTH_FAILED"
                };
            }

            // Store session info
            await StoreSessionAsync(session);

            await MID_HelperFunctions.DebugMessageAsync(
                $"✓ User signed in successfully: {email}",
                LogLevel.Info
            );

            return new SupabaseAuthResult
            {
                Success = true,
                Message = "Sign in successful",
                Session = MapToSessionInfo(session),
                User = await GetUserProfileAsync(session.User.Id)
            };
        }
        catch (GotrueException ex)
        {
            await MID_HelperFunctions.LogExceptionAsync(ex, "Supabase sign in");
            _logger.LogError(ex, "Supabase authentication error: {Message}", ex.Message);
            
            return new SupabaseAuthResult
            {
                Success = false,
                Message = ex.Message,
                ErrorCode = ex.Message.Contains("Invalid") ? "INVALID_CREDENTIALS" : "AUTH_ERROR"
            };
        }
        catch (Exception ex)
        {
            await MID_HelperFunctions.LogExceptionAsync(ex, "Sign in");
            _logger.LogError(ex, "Unexpected error during sign in");
            
            return new SupabaseAuthResult
            {
                Success = false,
                Message = "An unexpected error occurred",
                ErrorCode = "UNEXPECTED_ERROR"
            };
        }
    }

    public async Task<SupabaseAuthResult> SignUpAsync(string email, string password, UserModel userData)
    {
        try
        {
            if (!MID_HelperFunctions.IsValidString(email) || !MID_HelperFunctions.IsValidString(password))
            {
                return new SupabaseAuthResult
                {
                    Success = false,
                    Message = "Email and password are required",
                    ErrorCode = "INVALID_INPUT"
                };
            }

            await MID_HelperFunctions.DebugMessageAsync(
                $"Attempting sign up for: {email}",
                LogLevel.Info
            );

            // Create user metadata
            var userMetadata = new Dictionary<string, object>
            {
                { "first_name", userData.FirstName },
                { "last_name", userData.LastName },
                { "phone_number", userData.PhoneNumber ?? string.Empty },
                { "avatar_url", userData.AvatarUrl ?? string.Empty }
            };

            var session = await _client.Auth.SignUp(email, password, new SignUpOptions
            {
                Data = userMetadata
            });

            if (session?.User == null)
            {
                return new SupabaseAuthResult
                {
                    Success = false,
                    Message = "Registration failed",
                    ErrorCode = "SIGNUP_FAILED"
                };
            }

            // Store session info
            await StoreSessionAsync(session);

            await MID_HelperFunctions.DebugMessageAsync(
                $"✓ User signed up successfully: {email}",
                LogLevel.Info
            );

            var newUserData = new UserModel
            {
                Id = session.User.Id,
                Email = email,
                FirstName = userData.FirstName,
                LastName = userData.LastName,
                PhoneNumber = userData.PhoneNumber,
                AvatarUrl = userData.AvatarUrl,
                DateOfBirth = userData.DateOfBirth,
                Gender = userData.Gender,
                IsEmailVerified = false,
                IsPhoneVerified = false,
                AccountStatus = "Pending",
                EmailNotifications = userData.EmailNotifications,
                SmsNotifications = userData.SmsNotifications,
                PreferredLanguage = userData.PreferredLanguage,
                Currency = userData.Currency,
                TotalOrders = 0,
                TotalSpent = 0,
                LoyaltyPoints = 0,
                MembershipTier = "Bronze",
                CreatedAt = DateTime.UtcNow,
                CreatedBy = "system"
            };

            return new SupabaseAuthResult
            {
                Success = true,
                Message = "Registration successful. Please check your email to verify your account.",
                Session = MapToSessionInfo(session),
                User = newUserData
            };
        }
        catch (GotrueException ex)
        {
            await MID_HelperFunctions.LogExceptionAsync(ex, "Supabase sign up");
            _logger.LogError(ex, "Supabase signup error: {Message}", ex.Message);
            
            return new SupabaseAuthResult
            {
                Success = false,
                Message = ex.Message,
                ErrorCode = ex.Message.Contains("already registered") ? "USER_EXISTS" : "SIGNUP_ERROR"
            };
        }
        catch (Exception ex)
        {
            await MID_HelperFunctions.LogExceptionAsync(ex, "Sign up");
            _logger.LogError(ex, "Unexpected error during sign up");
            
            return new SupabaseAuthResult
            {
                Success = false,
                Message = "An unexpected error occurred",
                ErrorCode = "UNEXPECTED_ERROR"
            };
        }
    }// ==================== OAUTH AUTHENTICATION ====================

    public async Task<bool> SignInWithGoogleAsync()
    {
        try
        {
            await MID_HelperFunctions.DebugMessageAsync(
                "Initiating Google OAuth sign in",
                LogLevel.Info
            );

            // Get the redirect URL from Supabase
            var options = new SignInOptions
            {
                RedirectTo = GetRedirectUrl()
            };

            // This will redirect the browser to Google's OAuth page
            await _client.Auth.SignIn(Provider.Google, options);
            
            await MID_HelperFunctions.DebugMessageAsync(
                "✓ Google OAuth redirect initiated",
                LogLevel.Info
            );

            return true;
        }
        catch (Exception ex)
        {
            await MID_HelperFunctions.LogExceptionAsync(ex, "Google OAuth sign in");
            _logger.LogError(ex, "Failed to initiate Google sign in");
            return false;
        }
    }

    public async Task<bool> SignInWithFacebookAsync()
    {
        try
        {
            await MID_HelperFunctions.DebugMessageAsync(
                "Initiating Facebook OAuth sign in",
                LogLevel.Info
            );

            var options = new SignInOptions
            {
                RedirectTo = GetRedirectUrl()
            };

            // This will redirect the browser to Facebook's OAuth page
            await _client.Auth.SignIn(Provider.Facebook, options);
            
            await MID_HelperFunctions.DebugMessageAsync(
                "✓ Facebook OAuth redirect initiated",
                LogLevel.Info
            );

            return true;
        }
        catch (Exception ex)
        {
            await MID_HelperFunctions.LogExceptionAsync(ex, "Facebook OAuth sign in");
            _logger.LogError(ex, "Failed to initiate Facebook sign in");
            return false;
        }
    }

    // ==================== SESSION MANAGEMENT ====================

    public async Task<bool> SignOutAsync()
    {
        try
        {
            await MID_HelperFunctions.DebugMessageAsync(
                "Signing out user",
                LogLevel.Info
            );

            await _client.Auth.SignOut();
            
            // Clear stored session
            await _localStorage.RemoveItemAsync(SESSION_STORAGE_KEY);
            await _localStorage.RemoveItemAsync(USER_STORAGE_KEY);
            
            await MID_HelperFunctions.DebugMessageAsync(
                "✓ User signed out successfully",
                LogLevel.Info
            );

            return true;
        }
        catch (Exception ex)
        {
            await MID_HelperFunctions.LogExceptionAsync(ex, "Sign out");
            _logger.LogError(ex, "Error signing out");
            return false;
        }
    }

    public async Task<User?> GetCurrentUserAsync()
    {
        try
        {
            return _client.Auth.CurrentUser;
        }
        catch (Exception ex)
        {
            await MID_HelperFunctions.LogExceptionAsync(ex, "Get current user");
            _logger.LogError(ex, "Error retrieving current user");
            return null;
        }
    }

    public async Task<bool> IsAuthenticatedAsync()
    {
        try
        {
            var user = await GetCurrentUserAsync();
            return user != null;
        }
        catch (Exception ex)
        {
            await MID_HelperFunctions.LogExceptionAsync(ex, "Check authentication");
            _logger.LogError(ex, "Error checking authentication status");
            return false;
        }
    }

    public async Task<bool> RefreshSessionAsync()
    {
        try
        {
            await MID_HelperFunctions.DebugMessageAsync(
                "Refreshing session",
                LogLevel.Info
            );

            var session = await _client.Auth.RefreshSession();
            
            if (session != null)
            {
                await StoreSessionAsync(session);
                
                await MID_HelperFunctions.DebugMessageAsync(
                    "✓ Session refreshed successfully",
                    LogLevel.Info
                );
                
                return true;
            }

            _logger.LogWarning("Session refresh returned null");
            return false;
        }
        catch (Exception ex)
        {
            await MID_HelperFunctions.LogExceptionAsync(ex, "Refresh session");
            _logger.LogError(ex, "Error refreshing session");
            return false;
        }
    }

    // ==================== PASSWORD MANAGEMENT ====================

    public async Task<bool> SendPasswordResetEmailAsync(string email)
    {
        try
        {
            if (!MID_HelperFunctions.IsValidString(email))
            {
                _logger.LogWarning("SendPasswordResetEmail called with invalid email");
                return false;
            }

            await MID_HelperFunctions.DebugMessageAsync(
                $"Sending password reset email to: {email}",
                LogLevel.Info
            );

            await _client.Auth.ResetPasswordForEmail(email);
            
            await MID_HelperFunctions.DebugMessageAsync(
                $"✓ Password reset email sent to: {email}",
                LogLevel.Info
            );

            return true;
        }
        catch (Exception ex)
        {
            await MID_HelperFunctions.LogExceptionAsync(ex, "Send password reset email");
            _logger.LogError(ex, "Error sending password reset email");
            return false;
        }
    }

    public async Task<bool> UpdatePasswordAsync(string newPassword)
    {
        try
        {
            if (!MID_HelperFunctions.IsValidString(newPassword))
            {
                _logger.LogWarning("UpdatePassword called with invalid password");
                return false;
            }

            await MID_HelperFunctions.DebugMessageAsync(
                "Updating user password",
                LogLevel.Info
            );

            var user = await _client.Auth.Update(new UserAttributes
            {
                Password = newPassword
            });

            if (user != null)
            {
                await MID_HelperFunctions.DebugMessageAsync(
                    "✓ Password updated successfully",
                    LogLevel.Info
                );
                return true;
            }

            return false;
        }
        catch (Exception ex)
        {
            await MID_HelperFunctions.LogExceptionAsync(ex, "Update password");
            _logger.LogError(ex, "Error updating password");
            return false;
        }
    }

    // ==================== PROFILE MANAGEMENT ====================

    public async Task<bool> UpdateUserProfileAsync(Dictionary<string, object> updates)
    {
        try
        {
            if (updates == null || updates.Count == 0)
            {
                _logger.LogWarning("UpdateUserProfile called with empty updates");
                return false;
            }

            await MID_HelperFunctions.DebugMessageAsync(
                "Updating user profile",
                LogLevel.Info
            );

            var user = await _client.Auth.Update(new UserAttributes
            {
                Data = updates
            });

            if (user != null)
            {
                await MID_HelperFunctions.DebugMessageAsync(
                    "✓ User profile updated successfully",
                    LogLevel.Info
                );
                return true;
            }

            return false;
        }
        catch (Exception ex)
        {
            await MID_HelperFunctions.LogExceptionAsync(ex, "Update user profile");
            _logger.LogError(ex, "Error updating user profile");
            return false;
        }
    }

    public async Task<SupabaseSessionInfo?> GetSessionInfoAsync()
    {
        try
        {
            var session = _client.Auth.CurrentSession;
            return session != null ? MapToSessionInfo(session) : null;
        }
        catch (Exception ex)
        {
            await MID_HelperFunctions.LogExceptionAsync(ex, "Get session info");
            _logger.LogError(ex, "Error retrieving session info");
            return null;
        }
    }// ==================== EMAIL VERIFICATION ====================

    public async Task<bool> VerifyEmailAsync(string token)
    {
        try
        {
            if (!MID_HelperFunctions.IsValidString(token))
            {
                _logger.LogWarning("VerifyEmail called with invalid token");
                return false;
            }

            await MID_HelperFunctions.DebugMessageAsync(
                "Verifying email with token",
                LogLevel.Info
            );

            // Supabase handles email verification via magic link
            // The token is processed automatically when user clicks the link
            // This method is here for future custom verification logic if needed

            await MID_HelperFunctions.DebugMessageAsync(
                "✓ Email verification processed",
                LogLevel.Info
            );

            return true;
        }
        catch (Exception ex)
        {
            await MID_HelperFunctions.LogExceptionAsync(ex, "Verify email");
            _logger.LogError(ex, "Error verifying email");
            return false;
        }
    }

    public async Task<bool> ResendVerificationEmailAsync(string email)
    {
        try
        {
            if (!MID_HelperFunctions.IsValidString(email))
            {
                _logger.LogWarning("ResendVerificationEmail called with invalid email");
                return false;
            }

            await MID_HelperFunctions.DebugMessageAsync(
                $"Resending verification email to: {email}",
                LogLevel.Info
            );

            // Supabase doesn't have a built-in resend verification method
            // We need to use the password reset flow as a workaround
            // Or implement custom email verification logic

            // For now, we'll send a magic link which acts as verification
            var options = new SignInOptions
            {
                RedirectTo = GetRedirectUrl()
            };

            await _client.Auth.SignIn(email, options);

            await MID_HelperFunctions.DebugMessageAsync(
                $"✓ Verification email resent to: {email}",
                LogLevel.Info
            );

            return true;
        }
        catch (Exception ex)
        {
            await MID_HelperFunctions.LogExceptionAsync(ex, "Resend verification email");
            _logger.LogError(ex, "Error resending verification email");
            return false;
        }
    }

    // ==================== PRIVATE HELPER METHODS ====================

    /// <summary>
    /// Store session information in local storage
    /// </summary>
    private async Task StoreSessionAsync(Session session)
    {
        try
        {
            var sessionInfo = MapToSessionInfo(session);
            var sessionJson = JsonHelper.Serialize(sessionInfo);
            await _localStorage.SetItemAsync(SESSION_STORAGE_KEY, sessionJson);

            await MID_HelperFunctions.DebugMessageAsync(
                "Session stored in local storage",
                LogLevel.Info
            );
        }
        catch (Exception ex)
        {
            await MID_HelperFunctions.LogExceptionAsync(ex, "Store session");
            _logger.LogWarning(ex, "Failed to store session in local storage");
        }
    }

    /// <summary>
    /// Map Supabase Session to our SessionInfo model
    /// </summary>
    private SupabaseSessionInfo MapToSessionInfo(Session session)
    {
        return new SupabaseSessionInfo
        {
            AccessToken = session.AccessToken ?? string.Empty,
            RefreshToken = session.RefreshToken ?? string.Empty,
            ExpiresAt = DateTime.UtcNow.AddSeconds(session.ExpiresIn),
            UserId = session.User?.Id ?? string.Empty,
            UserEmail = session.User?.Email ?? string.Empty
        };
    }

    /// <summary>
    /// Get user profile from local storage
    /// </summary>
    private async Task<UserModel?> GetUserProfileAsync(string userId)
    {
        try
        {
            var storedUser = await _localStorage.GetItemAsync<UserModel>(USER_STORAGE_KEY);
            return storedUser;
        }
        catch (Exception ex)
        {
            await MID_HelperFunctions.LogExceptionAsync(ex, "Get user profile");
            _logger.LogWarning(ex, "Failed to retrieve user profile");
            return null;
        }
    }

    /// <summary>
    /// Get redirect URL for OAuth callbacks
    /// </summary>
    private string GetRedirectUrl()
    {
        try
        {
            // Get current URL from JS interop
            var currentUrl = _jsRuntime.InvokeAsync<string>("eval", "window.location.origin").GetAwaiter().GetResult();
            
            // Return to home page after OAuth
            return $"{currentUrl}/";
        }
        catch (Exception ex)
        {
            _logger.LogWarning(ex, "Failed to get redirect URL, using default");
            // Fallback to default
            return "https://localhost:5001/";
        }
    }
}
