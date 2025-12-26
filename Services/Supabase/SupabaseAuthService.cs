// Services/Supabase/SupabaseAuthService.cs - FIXED
using SubashaVentures.Models.Supabase;
using SubashaVentures.Utilities.HelperScripts;
using SubashaVentures.Services.Storage;
using Supabase.Gotrue;
using Supabase.Gotrue.Exceptions;
using Microsoft.AspNetCore.Components;
using System.Text.Json;
using LogLevel = SubashaVentures.Utilities.Logging.LogLevel;
using Client = Supabase.Client;

namespace SubashaVentures.Services.Supabase;

public class SupabaseAuthService : ISupabaseAuthService
{
    private const string AccessTokenKey = "supabase_access_token";
    private const string RefreshTokenKey = "supabase_refresh_token";
    private const string UserSessionKey = "supabase_user_session";

    private readonly Client _supabase;
    private readonly IBlazorAppLocalStorageService _localStorage;
    private readonly NavigationManager _navigationManager;
    private readonly ILogger<SupabaseAuthService> _logger;

    public SupabaseAuthService(
        Client supabase,
        IBlazorAppLocalStorageService localStorage,
        NavigationManager navigationManager,
        ILogger<SupabaseAuthService> logger)
    {
        _supabase = supabase;
        _localStorage = localStorage;
        _navigationManager = navigationManager;
        _logger = logger;
    }

    // ==================== SIGN IN WITH EMAIL/PASSWORD ====================
    
    public async Task<SupabaseAuthResult> SignInAsync(string email, string password)
    {
        try
        {
            await MID_HelperFunctions.DebugMessageAsync(
                $"Attempting sign in for: {email}",
                LogLevel.Info
            );

            var session = await _supabase.Auth.SignIn(email, password);

            if (session == null || string.IsNullOrEmpty(session.AccessToken))
            {
                return new SupabaseAuthResult
                {
                    Success = false,
                    Message = "Sign in failed. Please check your credentials.",
                    ErrorCode = "AUTH_ERROR"
                };
            }

            await StoreSessionAsync(session);

            await MID_HelperFunctions.DebugMessageAsync(
                $"✓ User signed in successfully: {email}",
                LogLevel.Info
            );

            return new SupabaseAuthResult
            {
                Success = true,
                Message = "Sign in successful",
                Session = CreateSessionInfo(session)
            };
        }
        catch (GotrueException ex)
        {
            var errorCode = GetErrorCode(ex.Message);
            var errorMessage = GetFriendlyErrorMessage(errorCode);
            
            await MID_HelperFunctions.LogExceptionAsync(ex, "Sign in");
            _logger.LogError(ex, "Sign in failed for {Email}", email);
            
            return new SupabaseAuthResult
            {
                Success = false,
                Message = errorMessage,
                ErrorCode = errorCode
            };
        }
        catch (Exception ex)
        {
            await MID_HelperFunctions.LogExceptionAsync(ex, "Sign in");
            _logger.LogError(ex, "Unexpected error during sign in");
            
            return new SupabaseAuthResult
            {
                Success = false,
                Message = "An unexpected error occurred. Please try again.",
                ErrorCode = "UNEXPECTED_ERROR"
            };
        }
    }

    // ==================== SIGN IN WITH GOOGLE OAUTH ====================

    public async Task<bool> SignInWithGoogleAsync(string? returnUrl = null)
    {
        try
        {
            await MID_HelperFunctions.DebugMessageAsync(
                "Initiating Google OAuth sign-in",
                LogLevel.Info
            );

            if (!string.IsNullOrEmpty(returnUrl))
            {
                await _localStorage.SetItemAsync("oauth_return_url", returnUrl);
            }

            var redirectUrl = $"{_navigationManager.BaseUri}auth/callback";

            var options = new SignInOptions
            {
                RedirectTo = redirectUrl
            };

            var result = await _supabase.Auth.SignIn(Constants.Provider.Google, options);

            if (result?.Uri != null)
            {
                // Convert System.Uri to string
                _navigationManager.NavigateTo(result.Uri.ToString(), true);
                return true;
            }

            await MID_HelperFunctions.DebugMessageAsync(
                "Google OAuth initiation failed - no redirect URL returned",
                LogLevel.Error
            );

            return false;
        }
        catch (Exception ex)
        {
            await MID_HelperFunctions.LogExceptionAsync(ex, "Google OAuth sign-in");
            _logger.LogError(ex, "Error initiating Google OAuth sign-in");
            return false;
        }
    }

    // ==================== HANDLE OAUTH CALLBACK ====================
    
    public async Task<SupabaseAuthResult> HandleOAuthCallbackAsync()
    {
        try
        {
            await MID_HelperFunctions.DebugMessageAsync(
                "Processing OAuth callback",
                LogLevel.Info
            );

            var session = _supabase.Auth.CurrentSession;

            if (session == null || string.IsNullOrEmpty(session.AccessToken))
            {
                return new SupabaseAuthResult
                {
                    Success = false,
                    Message = "OAuth authentication failed",
                    ErrorCode = "OAUTH_ERROR"
                };
            }

            await StoreSessionAsync(session);

            var user = session.User;
            if (user != null)
            {
                await MID_HelperFunctions.DebugMessageAsync(
                    $"✓ OAuth sign-in successful for: {user.Email}",
                    LogLevel.Info
                );

                await EnsureUserProfileExistsAsync(user);
            }

            return new SupabaseAuthResult
            {
                Success = true,
                Message = "Sign in successful",
                Session = CreateSessionInfo(session)
            };
        }
        catch (Exception ex)
        {
            await MID_HelperFunctions.LogExceptionAsync(ex, "OAuth callback");
            _logger.LogError(ex, "Error handling OAuth callback");
            
            return new SupabaseAuthResult
            {
                Success = false,
                Message = "Authentication failed",
                ErrorCode = "OAUTH_CALLBACK_ERROR"
            };
        }
    }

    // ==================== SIGN UP ====================
    
    public async Task<SupabaseAuthResult> SignUpAsync(string email, string password, UserModel userData)
    {
        try
        {
            await MID_HelperFunctions.DebugMessageAsync(
                $"Attempting sign up for: {email}",
                LogLevel.Info
            );

            var signUpOptions = new SignUpOptions
            {
                Data = new Dictionary<string, object>
                {
                    { "first_name", userData.FirstName },
                    { "last_name", userData.LastName },
                    { "phone_number", userData.PhoneNumber ?? "" },
                    { "avatar_url", userData.AvatarUrl ?? "" },
                    { "email_verified", false },
                    { "phone_verified", false }
                }
            };

            var session = await _supabase.Auth.SignUp(email, password, signUpOptions);

            if (session?.User == null)
            {
                return new SupabaseAuthResult
                {
                    Success = false,
                    Message = "Sign up failed. Please try again.",
                    ErrorCode = "SIGNUP_ERROR"
                };
            }

            // Create user profile in public.users table
            await CreateUserProfileAsync(session.User, userData);

            await MID_HelperFunctions.DebugMessageAsync(
                $"✓ User signed up successfully: {email}",
                LogLevel.Info
            );

            return new SupabaseAuthResult
            {
                Success = true,
                Message = "Registration successful! Please check your email to verify your account."
            };
        }
        catch (GotrueException ex)
        {
            var errorCode = GetErrorCode(ex.Message);
            var errorMessage = GetFriendlyErrorMessage(errorCode);
            
            await MID_HelperFunctions.LogExceptionAsync(ex, "Sign up");
            _logger.LogError(ex, "Sign up failed for {Email}", email);
            
            return new SupabaseAuthResult
            {
                Success = false,
                Message = errorMessage,
                ErrorCode = errorCode
            };
        }
        catch (Exception ex)
        {
            await MID_HelperFunctions.LogExceptionAsync(ex, "Sign up");
            _logger.LogError(ex, "Unexpected error during sign up");
            
            return new SupabaseAuthResult
            {
                Success = false,
                Message = "An unexpected error occurred. Please try again.",
                ErrorCode = "UNEXPECTED_ERROR"
            };
        }
    }

    // ==================== SIGN OUT ====================
    
    public async Task<bool> SignOutAsync()
    {
        try
        {
            await MID_HelperFunctions.DebugMessageAsync(
                "Signing out user",
                LogLevel.Info
            );

            await _supabase.Auth.SignOut();
            await ClearStoredSessionAsync();

            await MID_HelperFunctions.DebugMessageAsync(
                "✓ User signed out successfully",
                LogLevel.Info
            );

            _logger.LogInformation("User signed out successfully");
            return true;
        }
        catch (Exception ex)
        {
            await MID_HelperFunctions.LogExceptionAsync(ex, "Sign out");
            _logger.LogError(ex, "Error signing out");
            
            try
            {
                await ClearStoredSessionAsync();
            }
            catch { }
            
            return false;
        }
    }

    // ==================== SESSION MANAGEMENT ====================
    
    public async Task<User?> GetCurrentUserAsync()
    {
        try
        {
            var session = _supabase.Auth.CurrentSession;
            if (session?.User != null)
            {
                return session.User;
            }

            var accessToken = await _localStorage.GetItemAsync<string>(AccessTokenKey);
            var refreshToken = await _localStorage.GetItemAsync<string>(RefreshTokenKey);

            if (!string.IsNullOrEmpty(accessToken) && !string.IsNullOrEmpty(refreshToken))
            {
                var restoredSession = await _supabase.Auth.SetSession(accessToken, refreshToken);
                if (restoredSession?.User != null)
                {
                    await MID_HelperFunctions.DebugMessageAsync(
                        "Session restored from storage",
                        LogLevel.Info
                    );
                    return restoredSession.User;
                }
            }

            return null;
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error getting current user");
            return null;
        }
    }

    public async Task<Session?> GetCurrentSessionAsync()
    {
        try
        {
            var session = _supabase.Auth.CurrentSession;
            if (session != null && !string.IsNullOrEmpty(session.AccessToken))
            {
                return session;
            }

            var accessToken = await _localStorage.GetItemAsync<string>(AccessTokenKey);
            var refreshToken = await _localStorage.GetItemAsync<string>(RefreshTokenKey);

            if (!string.IsNullOrEmpty(accessToken) && !string.IsNullOrEmpty(refreshToken))
            {
                var restoredSession = await _supabase.Auth.SetSession(accessToken, refreshToken);
                if (restoredSession != null)
                {
                    await MID_HelperFunctions.DebugMessageAsync(
                        "Session restored from storage",
                        LogLevel.Info
                    );
                    return restoredSession;
                }
            }

            return null;
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error getting current session");
            return null;
        }
    }

    public async Task<bool> IsAuthenticatedAsync()
    {
        try
        {
            var user = await GetCurrentUserAsync();
            var isAuthenticated = user != null;
            
            await MID_HelperFunctions.DebugMessageAsync(
                $"Authentication check: {(isAuthenticated ? "Authenticated" : "Not authenticated")}",
                LogLevel.Debug
            );
            
            return isAuthenticated;
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error checking authentication");
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

            var session = await _supabase.Auth.RefreshSession();
            
            if (session != null && !string.IsNullOrEmpty(session.AccessToken))
            {
                await StoreSessionAsync(session);
                
                await MID_HelperFunctions.DebugMessageAsync(
                    "✓ Session refreshed successfully",
                    LogLevel.Info
                );
                
                return true;
            }

            await MID_HelperFunctions.DebugMessageAsync(
                "Session refresh failed",
                LogLevel.Warning
            );

            return false;
        }
        catch (Exception ex)
        {
            await MID_HelperFunctions.LogExceptionAsync(ex, "Refreshing session");
            _logger.LogError(ex, "Error refreshing session");
            return false;
        }
    }

    // ==================== PASSWORD MANAGEMENT ====================
    
    public async Task<bool> SendPasswordResetEmailAsync(string email)
    {
        try
        {
            await MID_HelperFunctions.DebugMessageAsync(
                $"Sending password reset email to: {email}",
                LogLevel.Info
            );

            await _supabase.Auth.ResetPasswordForEmail(email);
            
            await MID_HelperFunctions.DebugMessageAsync(
                "✓ Password reset email sent",
                LogLevel.Info
            );
            
            return true;
        }
        catch (Exception ex)
        {
            await MID_HelperFunctions.LogExceptionAsync(ex, "Sending password reset email");
            _logger.LogError(ex, "Error sending password reset email to {Email}", email);
            return false;
        }
    }

    public async Task<SupabaseAuthResult> UpdatePasswordAsync(string newPassword)
    {
        try
        {
            await MID_HelperFunctions.DebugMessageAsync(
                "Updating password",
                LogLevel.Info
            );

            var attributes = new UserAttributes
            {
                Password = newPassword
            };

            var user = await _supabase.Auth.Update(attributes);

            if (user == null)
            {
                return new SupabaseAuthResult
                {
                    Success = false,
                    Message = "Failed to update password",
                    ErrorCode = "UPDATE_ERROR"
                };
            }

            await MID_HelperFunctions.DebugMessageAsync(
                "✓ Password updated successfully",
                LogLevel.Info
            );

            return new SupabaseAuthResult
            {
                Success = true,
                Message = "Password updated successfully"
            };
        }
        catch (GotrueException ex)
        {
            var errorCode = GetErrorCode(ex.Message);
            var errorMessage = GetFriendlyErrorMessage(errorCode);
            
            await MID_HelperFunctions.LogExceptionAsync(ex, "Updating password");
            _logger.LogError(ex, "Error updating password");
            
            return new SupabaseAuthResult
            {
                Success = false,
                Message = errorMessage,
                ErrorCode = errorCode
            };
        }
        catch (Exception ex)
        {
            await MID_HelperFunctions.LogExceptionAsync(ex, "Updating password");
            _logger.LogError(ex, "Unexpected error updating password");
            
            return new SupabaseAuthResult
            {
                Success = false,
                Message = "An unexpected error occurred",
                ErrorCode = "UNEXPECTED_ERROR"
            };
        }
    }

    // ==================== EMAIL VERIFICATION ====================
    
    public async Task<bool> VerifyEmailAsync(string token)
    {
        try
        {
            await MID_HelperFunctions.DebugMessageAsync(
                "Verifying email with token",
                LogLevel.Info
            );

            var session = await _supabase.Auth.VerifyOTP(token, token, Constants.EmailOtpType.Email);
            
            if (session != null)
            {
                await StoreSessionAsync(session);
                
                await MID_HelperFunctions.DebugMessageAsync(
                    "✓ Email verified successfully",
                    LogLevel.Info
                );
                
                return true;
            }

            return false;
        }
        catch (Exception ex)
        {
            await MID_HelperFunctions.LogExceptionAsync(ex, "Verifying email");
            _logger.LogError(ex, "Error verifying email");
            return false;
        }
    }

    public async Task<bool> ResendVerificationEmailAsync(string email)
    {
        try
        {
            await MID_HelperFunctions.DebugMessageAsync(
                $"Resending verification email to: {email}",
                LogLevel.Info
            );

            await _supabase.Auth.SignUp(email, "");
            
            await MID_HelperFunctions.DebugMessageAsync(
                "✓ Verification email resent",
                LogLevel.Info
            );
            
            return true;
        }
        catch (Exception ex)
        {
            await MID_HelperFunctions.LogExceptionAsync(ex, "Resending verification email");
            _logger.LogError(ex, "Error resending verification email");
            return false;
        }
    }

    // ==================== USER PROFILE MANAGEMENT ====================
    
    public async Task<bool> UpdateUserProfileAsync(Dictionary<string, object> updates)
    {
        try
        {
            await MID_HelperFunctions.DebugMessageAsync(
                "Updating user profile metadata",
                LogLevel.Info
            );

            var attributes = new UserAttributes
            {
                Data = updates
            };

            var user = await _supabase.Auth.Update(attributes);
            
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
            await MID_HelperFunctions.LogExceptionAsync(ex, "Updating user profile");
            _logger.LogError(ex, "Error updating user profile");
            return false;
        }
    }

    // ==================== PRIVATE HELPER METHODS ====================
    
    private async Task StoreSessionAsync(Session session)
    {
        try
        {
            await _localStorage.SetItemAsync(AccessTokenKey, session.AccessToken);
            await _localStorage.SetItemAsync(RefreshTokenKey, session.RefreshToken ?? "");
            
            var sessionInfo = CreateSessionInfo(session);
            await _localStorage.SetItemAsync(UserSessionKey, sessionInfo);
            
            await MID_HelperFunctions.DebugMessageAsync(
                "Session stored in local storage",
                LogLevel.Debug
            );
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error storing session");
            throw;
        }
    }

    private async Task ClearStoredSessionAsync()
    {
        try
        {
            await _localStorage.RemoveItemAsync(AccessTokenKey);
            await _localStorage.RemoveItemAsync(RefreshTokenKey);
            await _localStorage.RemoveItemAsync(UserSessionKey);
            await _localStorage.RemoveItemAsync("oauth_return_url");
            
            await MID_HelperFunctions.DebugMessageAsync(
                "Stored session cleared",
                LogLevel.Debug
            );
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error clearing stored session");
        }
    }

    private SupabaseSessionInfo CreateSessionInfo(Session session)
    {
        return new SupabaseSessionInfo
        {
            AccessToken = session.AccessToken ?? "",
            RefreshToken = session.RefreshToken ?? "",
            ExpiresAt = session.ExpiresAt(),
            UserId = session.User?.Id ?? "",
            UserEmail = session.User?.Email ?? ""
        };
    }

    private async Task CreateUserProfileAsync(User authUser, UserModel userData)
    {
        try
        {
            // Check if profile already exists (might be created by trigger)
            var existingProfile = await _supabase
                .From<UserModel>()
                .Where(u => u.Id == authUser.Id)
                .Single();

            if (existingProfile != null)
            {
                await MID_HelperFunctions.DebugMessageAsync(
                    $"✓ User profile already exists (created by trigger): {authUser.Email}",
                    LogLevel.Info
                );
                return;
            }

            // Create profile if it doesn't exist
            var userProfile = new UserModel
            {
                Id = authUser.Id,
                Email = authUser.Email ?? "",
                FirstName = userData.FirstName,
                LastName = userData.LastName,
                PhoneNumber = userData.PhoneNumber,
                AvatarUrl = userData.AvatarUrl,
                IsEmailVerified = false,
                IsPhoneVerified = false,
                AccountStatus = "Active",
                EmailNotifications = true,
                SmsNotifications = false,
                PreferredLanguage = "en",
                Currency = "NGN",
                MembershipTier = "Bronze",
                CreatedAt = DateTime.UtcNow,
                CreatedBy = authUser.Id
            };

            await _supabase.From<UserModel>().Insert(userProfile);
            
            // Assign default "user" role
            var userRole = new UserRoleModel
            {
                Id = Guid.NewGuid().ToString(),
                UserId = authUser.Id,
                Role = "user",
                CreatedAt = DateTime.UtcNow,
                CreatedBy = authUser.Id
            };

            await _supabase.From<UserRoleModel>().Insert(userRole);
            
            await MID_HelperFunctions.DebugMessageAsync(
                $"✓ User profile created for: {authUser.Email}",
                LogLevel.Info
            );
        }
        catch (Exception ex)
        {
            await MID_HelperFunctions.LogExceptionAsync(ex, "Creating user profile");
            _logger.LogError(ex, "Error creating user profile");
        }
    }

    private async Task EnsureUserProfileExistsAsync(User authUser)
    {
        try
        {
            // Check if user profile exists
            var existingUser = await _supabase
                .From<UserModel>()
                .Where(u => u.Id == authUser.Id)
                .Single();

            if (existingUser == null)
            {
                // Create profile for OAuth user
                var userProfile = new UserModel
                {
                    Id = authUser.Id,
                    Email = authUser.Email ?? "",
                    FirstName = authUser.UserMetadata?.GetValueOrDefault("first_name")?.ToString() ?? "",
                    LastName = authUser.UserMetadata?.GetValueOrDefault("last_name")?.ToString() ?? "",
                    AvatarUrl = authUser.UserMetadata?.GetValueOrDefault("avatar_url")?.ToString(),
                    IsEmailVerified = authUser.EmailConfirmedAt != null,
                    AccountStatus = "Active",
                    EmailNotifications = true,
                    SmsNotifications = false,
                    PreferredLanguage = "en",
                    Currency = "NGN",
                    MembershipTier = "Bronze",
                    CreatedAt = DateTime.UtcNow,
                    CreatedBy = authUser.Id
                };

                await _supabase.From<UserModel>().Insert(userProfile);
                
                // Assign default "user" role
                var userRole = new UserRoleModel
                {
                    Id = Guid.NewGuid().ToString(),
                    UserId = authUser.Id,
                    Role = "user",
                    CreatedAt = DateTime.UtcNow,
                    CreatedBy = authUser.Id
                };

                await _supabase.From<UserRoleModel>().Insert(userRole);
                
                await MID_HelperFunctions.DebugMessageAsync(
                    $"✓ OAuth user profile created for: {authUser.Email}",
                    LogLevel.Info
                );
            }
        }
        catch (Exception ex)
        {
            await MID_HelperFunctions.LogExceptionAsync(ex, "Ensuring user profile exists");
            _logger.LogError(ex, "Error ensuring user profile exists");
        }
    }

    private string GetErrorCode(string errorMessage)
    {
        try
        {
            var error = JsonSerializer.Deserialize<AuthError>(errorMessage);
            return error?.error_code ?? "unknown_error";
        }
        catch
        {
            if (errorMessage.Contains("already registered"))
                return "user_already_exists";
            if (errorMessage.Contains("Invalid login credentials"))
                return "invalid_credentials";
            if (errorMessage.Contains("Email not confirmed"))
                return "email_not_confirmed";
            if (errorMessage.Contains("weak password"))
                return "weak_password";
            
            return "unknown_error";
        }
    }

    private string GetFriendlyErrorMessage(string errorCode)
    {
        return errorCode switch
        {
            "user_already_exists" => "An account with this email already exists. Please sign in instead.",
            "weak_password" => "Password is too weak. Please use at least 8 characters with uppercase, lowercase, and numbers.",
            "invalid_credentials" => "Invalid email or password. Please try again.",
            "email_not_confirmed" => "Please verify your email before signing in. Check your inbox for the verification link.",
            "same_password" => "The new password must be different from your current password.",
            "over_email_send_rate_limit" => "Too many requests. Please wait a moment before trying again.",
            "over_request_rate_limit" => "Too many requests. Please wait a moment before trying again.",
            _ => "An error occurred. Please try again."
        };
    }

    private class AuthError
    {
        public string error_code { get; set; } = string.Empty;
        public string message { get; set; } = string.Empty;
    }
}