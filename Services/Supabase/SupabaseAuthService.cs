// Services/Auth/SupabaseAuthService.cs - USING EXISTING MODEL
using SubashaVentures.Models.Supabase;
using SubashaVentures.Utilities.HelperScripts;
using SubashaVentures.Services.Storage;
using Supabase.Gotrue;
using Supabase.Gotrue.Exceptions;
using System.Text.Json;
using LogLevel = SubashaVentures.Utilities.Logging.LogLevel;

namespace SubashaVentures.Services.Auth;

public class SupabaseAuthService
{
    private const string AccessTokenKey = "supabase_access_token";
    private const string RefreshTokenKey = "supabase_refresh_token";

    private readonly Client _supabase;
    private readonly IBlazorAppLocalStorageService _localStorage;
    private readonly ILogger<SupabaseAuthService> _logger;

    public SupabaseAuthService(
        Client supabase,
        IBlazorAppLocalStorageService localStorage,
        ILogger<SupabaseAuthService> logger)
    {
        _supabase = supabase;
        _localStorage = localStorage;
        _logger = logger;
    }

    // ==================== SIGN IN ====================
    
    public async Task<SupabaseAuthResult> SignInAsync(string email, string password)
    {
        try
        {
            await MID_HelperFunctions.DebugMessageAsync(
                $"Attempting sign in for: {email}",
                LogLevel.Info
            );

            var session = await _supabase.SignIn(email, password);

            if (session == null || string.IsNullOrEmpty(session.AccessToken))
            {
                return new SupabaseAuthResult
                {
                    Success = false,
                    Message = "Sign in failed",
                    ErrorCode = "AUTH_ERROR"
                };
            }

            // Store tokens
            await _localStorage.SetItemAsync(AccessTokenKey, session.AccessToken);
            await _localStorage.SetItemAsync(RefreshTokenKey, session.RefreshToken);

            await MID_HelperFunctions.DebugMessageAsync(
                $"✓ User signed in successfully: {email}",
                LogLevel.Info
            );

            return new SupabaseAuthResult
            {
                Success = true,
                Message = "Sign in successful"
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
                Message = "An unexpected error occurred",
                ErrorCode = "UNEXPECTED_ERROR"
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
                    { "avatar_url", userData.AvatarUrl ?? "" }
                }
            };

            var session = await _supabase.SignUp(email, password, signUpOptions);

            if (session?.User == null)
            {
                return new SupabaseAuthResult
                {
                    Success = false,
                    Message = "Sign up failed",
                    ErrorCode = "SIGNUP_ERROR"
                };
            }

            await MID_HelperFunctions.DebugMessageAsync(
                $"✓ User signed up successfully: {email}",
                LogLevel.Info
            );

            return new SupabaseAuthResult
            {
                Success = true,
                Message = "Registration successful. Please check your email to verify your account."
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
                Message = "An unexpected error occurred",
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

            await _supabase.SignOut();
            
            // Clear stored tokens
            await _localStorage.RemoveItemAsync(AccessTokenKey);
            await _localStorage.RemoveItemAsync(RefreshTokenKey);

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

    // ==================== GET CURRENT USER ====================
    
    public async Task<User?> GetCurrentUserAsync()
    {
        try
        {
            var session = _supabase.CurrentSession;
            if (session != null)
            {
                return session.User;
            }

            // Try to restore session from storage
            var accessToken = await _localStorage.GetItemAsync<string>(AccessTokenKey);
            var refreshToken = await _localStorage.GetItemAsync<string>(RefreshTokenKey);

            if (!string.IsNullOrEmpty(accessToken) && !string.IsNullOrEmpty(refreshToken))
            {
                var restoredSession = await _supabase.SetSession(accessToken, refreshToken);
                return restoredSession?.User;
            }

            return null;
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error getting current user");
            return null;
        }
    }

    // ==================== SESSION MANAGEMENT ====================
    
    public async Task<bool> IsAuthenticatedAsync()
    {
        try
        {
            var user = await GetCurrentUserAsync();
            return user != null;
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error checking authentication");
            return false;
        }
    }

    public async Task<Session?> GetCurrentSessionAsync()
    {
        try
        {
            var session = _supabase.CurrentSession;
            if (session != null)
            {
                return session;
            }

            // Try to restore session
            var accessToken = await _localStorage.GetItemAsync<string>(AccessTokenKey);
            var refreshToken = await _localStorage.GetItemAsync<string>(RefreshTokenKey);

            if (!string.IsNullOrEmpty(accessToken) && !string.IsNullOrEmpty(refreshToken))
            {
                return await _supabase.SetSession(accessToken, refreshToken);
            }

            return null;
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error getting session");
            return null;
        }
    }

    public async Task<bool> RefreshSessionAsync()
    {
        try
        {
            var session = await _supabase.RefreshSession();
            
            if (session != null && !string.IsNullOrEmpty(session.AccessToken))
            {
                await _localStorage.SetItemAsync(AccessTokenKey, session.AccessToken);
                await _localStorage.SetItemAsync(RefreshTokenKey, session.RefreshToken);
                return true;
            }

            return false;
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error refreshing session");
            return false;
        }
    }

    // ==================== PASSWORD MANAGEMENT ====================
    
    public async Task<bool> SendPasswordResetEmailAsync(string email)
    {
        try
        {
            await _supabase.ResetPasswordForEmail(email);
            return true;
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error sending password reset email");
            return false;
        }
    }

    public async Task<SupabaseAuthResult> UpdatePasswordAsync(string newPassword)
    {
        try
        {
            var attributes = new UserAttributes
            {
                Password = newPassword
            };

            var user = await _supabase.Update(attributes);

            if (user == null)
            {
                return new SupabaseAuthResult
                {
                    Success = false,
                    Message = "Failed to update password",
                    ErrorCode = "UPDATE_ERROR"
                };
            }

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
            _logger.LogError(ex, "Unexpected error updating password");
            return new SupabaseAuthResult
            {
                Success = false,
                Message = "An unexpected error occurred",
                ErrorCode = "UNEXPECTED_ERROR"
            };
        }
    }

    // ==================== ERROR HANDLING ====================
    
    private string GetErrorCode(string errorMessage)
    {
        try
        {
            var error = JsonSerializer.Deserialize<AuthError>(errorMessage);
            return error?.error_code ?? "unknown_error";
        }
        catch
        {
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
            "email_not_confirmed" => "Please verify your email before signing in. Check your inbox.",
            "same_password" => "The new password must be different from the old one.",
            _ => "An error occurred. Please try again."
        };
    }

    // Helper class for error parsing
    private class AuthError
    {
        public string error_code { get; set; } = string.Empty;
    }
}
