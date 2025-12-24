// Services/Supabase/SupabaseAuthService.cs - JAVASCRIPT-BASED
using SubashaVentures.Models.Supabase;
using SubashaVentures.Utilities.HelperScripts;
using Microsoft.JSInterop;
using Newtonsoft.Json.Linq;
using Supabase.Gotrue;
using LogLevel = SubashaVentures.Utilities.Logging.LogLevel;

namespace SubashaVentures.Services.Supabase;

public class SupabaseAuthService : ISupabaseAuthService
{
    private readonly IJSRuntime _jsRuntime;
    private readonly ILogger<SupabaseAuthService> _logger;

    public SupabaseAuthService(
        IJSRuntime jsRuntime,
        ILogger<SupabaseAuthService> logger)
    {
        _jsRuntime = jsRuntime;
        _logger = logger;
    }

    // ==================== EMAIL/PASSWORD AUTH ====================

    public async Task<SupabaseAuthResult> SignInAsync(string email, string password)
    {
        try
        {
            await MID_HelperFunctions.DebugMessageAsync(
                $"Attempting sign in for: {email}",
                LogLevel.Info
            );

            var resultJson = await _jsRuntime.InvokeAsync<string>(
                "supabaseOAuth.signIn", email, password);

            var result = JObject.Parse(resultJson);
            
            if (result["success"]?.Value<bool>() == true)
            {
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
            else
            {
                var error = result["error"]?.ToString() ?? "Sign in failed";
                var errorCode = result["errorCode"]?.ToString();

                return new SupabaseAuthResult
                {
                    Success = false,
                    Message = error,
                    ErrorCode = errorCode == "400" ? "INVALID_CREDENTIALS" : "AUTH_ERROR"
                };
            }
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
            await MID_HelperFunctions.DebugMessageAsync(
                $"Attempting sign up for: {email}",
                LogLevel.Info
            );

            var userMetadata = new
            {
                first_name = userData.FirstName,
                last_name = userData.LastName,
                phone_number = userData.PhoneNumber ?? "",
                avatar_url = userData.AvatarUrl ?? ""
            };

            var resultJson = await _jsRuntime.InvokeAsync<string>(
                "supabaseOAuth.signUp", email, password, userMetadata);

            var result = JObject.Parse(resultJson);
            
            if (result["success"]?.Value<bool>() == true)
            {
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
            else
            {
                var error = result["error"]?.ToString() ?? "Sign up failed";
                
                return new SupabaseAuthResult
                {
                    Success = false,
                    Message = error,
                    ErrorCode = error.Contains("already registered") ? "USER_EXISTS" : "SIGNUP_ERROR"
                };
            }
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

    // ==================== OAUTH ====================

    public async Task<bool> SignInWithGoogleAsync()
    {
        try
        {
            await MID_HelperFunctions.DebugMessageAsync(
                "Initiating Google OAuth sign in",
                LogLevel.Info
            );

            var success = await _jsRuntime.InvokeAsync<bool>("supabaseOAuth.signInWithGoogle");
            
            if (success)
            {
                _logger.LogInformation("Google OAuth initiated successfully");
            }
            
            return success;
        }
        catch (Exception ex)
        {
            await MID_HelperFunctions.LogExceptionAsync(ex, "Google OAuth sign in");
            _logger.LogError(ex, "Failed to initiate Google sign in");
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

            var success = await _jsRuntime.InvokeAsync<bool>("supabaseOAuth.signOut");
            
            if (success)
            {
                await MID_HelperFunctions.DebugMessageAsync(
                    "✓ User signed out successfully",
                    LogLevel.Info
                );
            }
            
            return success;
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
            var userJson = await _jsRuntime.InvokeAsync<string>("eval",
                @"(async function() {
                    try {
                        const user = await window.supabaseOAuth.getUser();
                        return user ? JSON.stringify(user) : null;
                    } catch (error) {
                        return null;
                    }
                })()");

            if (string.IsNullOrEmpty(userJson))
            {
                return null;
            }

            // For now, return null - we're using JavaScript exclusively
            return null;
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error retrieving current user");
            return null;
        }
    }

    public async Task<bool> IsAuthenticatedAsync()
    {
        try
        {
            return await _jsRuntime.InvokeAsync<bool>("supabaseOAuth.isAuthenticated");
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error checking authentication status");
            return false;
        }
    }

    public async Task<bool> RefreshSessionAsync()
    {
        // Session refresh is handled automatically by JavaScript SDK
        return true;
    }

    // ==================== NOT IMPLEMENTED (Use JavaScript) ====================

    public Task<bool> SendPasswordResetEmailAsync(string email) => Task.FromResult(false);
    public Task<bool> UpdatePasswordAsync(string newPassword) => Task.FromResult(false);
    public Task<bool> UpdateUserProfileAsync(Dictionary<string, object> updates) => Task.FromResult(false);
    public Task<SupabaseSessionInfo?> GetSessionInfoAsync() => Task.FromResult<SupabaseSessionInfo?>(null);
    public Task<bool> VerifyEmailAsync(string token) => Task.FromResult(false);
    public Task<bool> ResendVerificationEmailAsync(string email) => Task.FromResult(false);
}
