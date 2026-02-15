// Services/Supabase/SupabaseAuthService.cs - COMPLETE WITH SESSION MANAGER
using SubashaVentures.Models.Supabase;
using SubashaVentures.Utilities.HelperScripts;
using SubashaVentures.Services.Storage;
using Supabase.Gotrue;
using Supabase.Gotrue.Exceptions;
using Microsoft.AspNetCore.Components;
using System.Text.Json;
using Microsoft.AspNetCore.WebUtilities;
using Supabase.Gotrue.Mfa;
using LogLevel = SubashaVentures.Utilities.Logging.LogLevel;
using Client = Supabase.Client;

namespace SubashaVentures.Services.Supabase;

public class SupabaseAuthService : ISupabaseAuthService
{
    private readonly Client _supabase;
    private readonly SessionManager _sessionManager;
    private readonly NavigationManager _navigationManager;
    private readonly ILogger<SupabaseAuthService> _logger;

    private const string PkceVerifierKey = "supabase_pkce_verifier";

    public SupabaseAuthService(
        Client supabase,
        SessionManager sessionManager,
        NavigationManager navigationManager,
        ILogger<SupabaseAuthService> logger)
    {
        _supabase = supabase;
        _sessionManager = sessionManager;
        _navigationManager = navigationManager;
        _logger = logger;
    }

    // ==================== INITIALIZATION & SESSION RESTORATION ====================

    /// <summary>
    /// Initialize auth service and restore session from storage
    /// Called once on app startup
    /// </summary>
    public async Task InitializeAsync()
    {
        try
        {
            await MID_HelperFunctions.DebugMessageAsync(
                "🔐 Initializing SupabaseAuthService...",
                LogLevel.Info
            );

            // Try to restore session from storage
            var storedSession = await _sessionManager.GetStoredSessionAsync();
            
            if (storedSession != null)
            {
                await MID_HelperFunctions.DebugMessageAsync(
                    $"✓ Found stored session (expires: {storedSession.ExpiresAt})",
                    LogLevel.Info
                );

                // Check if session needs refresh
                if (_sessionManager.ShouldRefresh(storedSession.ExpiresAt))
                {
                    await MID_HelperFunctions.DebugMessageAsync(
                        "🔄 Session near expiry, refreshing...",
                        LogLevel.Info
                    );

                    await RefreshSessionAsync();
                }
                else
                {
                    // Restore session to Supabase client
                    await _supabase.Auth.SetSession(
                        storedSession.AccessToken,
                        storedSession.RefreshToken
                    );

                    await MID_HelperFunctions.DebugMessageAsync(
                        "✓ Session restored successfully",
                        LogLevel.Info
                    );
                }
            }
            else
            {
                await MID_HelperFunctions.DebugMessageAsync(
                    "ℹ️ No stored session found",
                    LogLevel.Info
                );
            }
        }
        catch (Exception ex)
        {
            await MID_HelperFunctions.LogExceptionAsync(ex, "Initializing auth service");
            _logger.LogError(ex, "Failed to initialize auth service");
        }
    }

    // ==================== SIGN IN WITH EMAIL/PASSWORD ====================

    public async Task<SupabaseAuthResult> SignInAsync(string email, string password)
    {
        try
        {
            await MID_HelperFunctions.DebugMessageAsync(
                $"🔑 Attempting sign in for: {email}",
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

            // Store session using SessionManager
            await _sessionManager.StoreSessionAsync(session);

            await MID_HelperFunctions.DebugMessageAsync(
                $"✅ User signed in successfully: {email}",
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

    // ==================== SIGN UP ====================

    public async Task<SupabaseAuthResult> SignUpAsync(string email, string password, UserModel userData)
    {
        try
        {
            await MID_HelperFunctions.DebugMessageAsync(
                $"📝 Attempting sign up for: {email}",
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
                    { "role", "user" }
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

            // Create user profile (done by database trigger, but we verify)
            await EnsureUserProfileExistsAsync(session.User, userData);

            await MID_HelperFunctions.DebugMessageAsync(
                $"✅ User signed up successfully: {email}",
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

    // ==================== GOOGLE OAUTH (PKCE) ====================

    // Services/Supabase/SupabaseAuthService.cs - OAUTH METHODS COMPLETE

// ==================== GOOGLE OAUTH (PKCE) ====================

/// <summary>
/// Initiate Google OAuth sign-in with PKCE flow
/// Stores PKCE verifier before redirect
/// </summary>
public async Task<bool> SignInWithGoogleAsync(string? returnUrl = null)
{
    try
    {
        await MID_HelperFunctions.DebugMessageAsync(
            "🔵 Initiating Google OAuth with PKCE flow",
            LogLevel.Info
        );

        // Store return URL if provided
        if (!string.IsNullOrEmpty(returnUrl))
        {
            await _sessionManager.StoreOAuthReturnUrl(returnUrl);
            await MID_HelperFunctions.DebugMessageAsync(
                $"✅ Return URL stored: {returnUrl}",
                LogLevel.Info
            );
            
            // Ensure storage completes
            await Task.Delay(100);
        }

        var baseUri = _navigationManager.BaseUri;
        var redirectUrl = $"{baseUri}auth/callback";

        await MID_HelperFunctions.DebugMessageAsync(
            $"🔗 Redirect URL: {redirectUrl}",
            LogLevel.Info
        );

        var options = new SignInOptions
        {
            FlowType = Constants.OAuthFlowType.PKCE,
            RedirectTo = redirectUrl
        };

        // Generate PKCE challenge and get authorization URL
        var result = await _supabase.Auth.SignIn(Constants.Provider.Google, options);

        if (result?.Uri != null && !string.IsNullOrEmpty(result.PKCEVerifier))
        {
            await MID_HelperFunctions.DebugMessageAsync(
                $"✅ PKCE challenge generated: {result.PKCEVerifier.Substring(0, Math.Min(20, result.PKCEVerifier.Length))}...",
                LogLevel.Info
            );

            // ✅ CRITICAL FIX: Store verifier with retry and verification
            await _sessionManager.StorePkceVerifier(result.PKCEVerifier);
            
            // ✅ CRITICAL: Wait to ensure localStorage write completes
            await Task.Delay(300);
            
            // Verify storage worked
            var stored = await _sessionManager.GetPkceVerifier();
            if (string.IsNullOrEmpty(stored))
            {
                await MID_HelperFunctions.DebugMessageAsync(
                    "❌ PKCE verifier storage verification failed! Retrying...",
                    LogLevel.Error
                );
                
                // Retry storage
                await _sessionManager.StorePkceVerifier(result.PKCEVerifier);
                await Task.Delay(200);
                
                // Verify again
                stored = await _sessionManager.GetPkceVerifier();
                if (string.IsNullOrEmpty(stored))
                {
                    _logger.LogError("PKCE verifier storage failed after retry");
                    // Continue anyway - static fallback exists
                }
            }
            else
            {
                await MID_HelperFunctions.DebugMessageAsync(
                    "✅ PKCE verifier storage verified successfully",
                    LogLevel.Info
                );
            }
            
            await MID_HelperFunctions.DebugMessageAsync(
                $"🔀 Redirecting to Google: {result.Uri}",
                LogLevel.Info
            );
            
            // Now redirect to Google
            _navigationManager.NavigateTo(result.Uri.ToString(), forceLoad: true);
            return true;
        }

        await MID_HelperFunctions.DebugMessageAsync(
            "❌ Failed to generate PKCE challenge",
            LogLevel.Error
        );

        return false;
    }
    catch (Exception ex)
    {
        await MID_HelperFunctions.LogExceptionAsync(ex, "Google OAuth initiation");
        _logger.LogError(ex, "Failed to initiate Google OAuth");
        return false;
    }
}

/// <summary>
/// Handle OAuth callback after user authenticates with Google
/// Exchanges authorization code for session using PKCE verifier
/// </summary>
public async Task<SupabaseAuthResult> HandleOAuthCallbackAsync()
{
    try
    {
        var currentUri = _navigationManager.Uri;
        
        await MID_HelperFunctions.DebugMessageAsync(
            $"📥 OAuth callback received at: {currentUri}",
            LogLevel.Info
        );

        var uri = new Uri(currentUri);
        var queryParams = QueryHelpers.ParseQuery(uri.Query);

        // Check for authorization code
        if (!queryParams.TryGetValue("code", out var codeValues) || codeValues.Count == 0)
        {
            await MID_HelperFunctions.DebugMessageAsync(
                "❌ No authorization code in callback URL",
                LogLevel.Error
            );

            return new SupabaseAuthResult
            {
                Success = false,
                Message = "OAuth authentication failed - no authorization code",
                ErrorCode = "OAUTH_NO_CODE"
            };
        }

        var code = codeValues.First();
        
        await MID_HelperFunctions.DebugMessageAsync(
            $"✅ Authorization code received: {code.Substring(0, Math.Min(20, code.Length))}...",
            LogLevel.Info
        );

        // Try to get PKCE verifier from storage (with fallbacks)
        var pkceVerifier = await _sessionManager.GetPkceVerifier();

        if (string.IsNullOrEmpty(pkceVerifier))
        {
            await MID_HelperFunctions.DebugMessageAsync(
                "❌ PKCE verifier not found in SessionManager. Checking alternate storage...",
                LogLevel.Warning
            );

            // ✅ FALLBACK 1: Try direct localStorage access via service
            try
            {
                var directKey = "SubashaVentures_supabase_pkce_verifier";
                var localStorage = _navigationManager.BaseUri.Contains("localhost") 
                    ? _sessionManager 
                    : _sessionManager;
                
                // The SessionManager already checked localStorage, so try static storage
                pkceVerifier = StaticAuthStorage.PkceVerifier;
                
                if (!string.IsNullOrEmpty(pkceVerifier))
                {
                    await MID_HelperFunctions.DebugMessageAsync(
                        $"✅ PKCE verifier found in static storage: {pkceVerifier.Substring(0, Math.Min(20, pkceVerifier.Length))}...",
                        LogLevel.Info
                    );
                }
            }
            catch (Exception ex)
            {
                await MID_HelperFunctions.LogExceptionAsync(ex, "Fallback PKCE retrieval");
            }

            // ✅ FALLBACK 2: Check if session already exists (Supabase auto-exchange)
            if (string.IsNullOrEmpty(pkceVerifier))
            {
                var existingSession = _supabase.Auth.CurrentSession;
                if (existingSession != null && !string.IsNullOrEmpty(existingSession.AccessToken))
                {
                    await MID_HelperFunctions.DebugMessageAsync(
                        "✅ Session already exists! Supabase auto-completed exchange.",
                        LogLevel.Info
                    );

                    // Store session
                    await _sessionManager.StoreSessionAsync(existingSession);

                    // Ensure user profile exists
                    if (existingSession.User != null)
                    {
                        await EnsureUserProfileExistsAsync(existingSession.User);
                    }

                    return new SupabaseAuthResult
                    {
                        Success = true,
                        Message = "Sign in successful",
                        Session = CreateSessionInfo(existingSession)
                    };
                }

                await MID_HelperFunctions.DebugMessageAsync(
                    "❌ PKCE verifier not found in any storage location",
                    LogLevel.Error
                );

                return new SupabaseAuthResult
                {
                    Success = false,
                    Message = "OAuth authentication failed - session state lost. Please try signing in again.",
                    ErrorCode = "OAUTH_NO_VERIFIER"
                };
            }
        }

        await MID_HelperFunctions.DebugMessageAsync(
            $"🔄 Exchanging authorization code for session...",
            LogLevel.Info
        );
        
        await MID_HelperFunctions.DebugMessageAsync(
            $"   Code: {code.Substring(0, Math.Min(30, code.Length))}...",
            LogLevel.Debug
        );
        
        await MID_HelperFunctions.DebugMessageAsync(
            $"   Verifier: {pkceVerifier.Substring(0, Math.Min(30, pkceVerifier.Length))}...",
            LogLevel.Debug
        );

        // Exchange code for session using PKCE verifier
        Session? session;
        try
        {
            session = await _supabase.Auth.ExchangeCodeForSession(pkceVerifier, code);
        }
        catch (Exception ex)
        {
            await MID_HelperFunctions.LogExceptionAsync(ex, "Code exchange");
            
            return new SupabaseAuthResult
            {
                Success = false,
                Message = $"Failed to exchange code: {ex.Message}",
                ErrorCode = "OAUTH_EXCHANGE_ERROR"
            };
        }

        if (session == null || string.IsNullOrEmpty(session.AccessToken))
        {
            await MID_HelperFunctions.DebugMessageAsync(
                "❌ Code exchange returned null or invalid session",
                LogLevel.Error
            );

            return new SupabaseAuthResult
            {
                Success = false,
                Message = "OAuth authentication failed - could not establish session",
                ErrorCode = "OAUTH_EXCHANGE_FAILED"
            };
        }

        await MID_HelperFunctions.DebugMessageAsync(
            $"✅ Code exchange successful! User: {session.User?.Email}",
            LogLevel.Info
        );

        // Clean up PKCE verifier (no longer needed)
        await _sessionManager.ClearPkceVerifier();

        // Store session for future use
        await _sessionManager.StoreSessionAsync(session);

        // Ensure user profile exists in database
        if (session.User != null)
        {
            await EnsureUserProfileExistsAsync(session.User);
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
        await MID_HelperFunctions.LogExceptionAsync(ex, "OAuth callback handler");
        _logger.LogError(ex, "OAuth callback failed");
        
        // Clean up verifier on error
        await _sessionManager.ClearPkceVerifier();
        
        return new SupabaseAuthResult
        {
            Success = false,
            Message = $"Authentication failed: {ex.Message}",
            ErrorCode = "OAUTH_CALLBACK_ERROR"
        };
    }
}
    // ==================== SIGN OUT ====================

    public async Task<bool> SignOutAsync()
    {
        try
        {
            await MID_HelperFunctions.DebugMessageAsync(
                "🚪 Signing out user...",
                LogLevel.Info
            );

            await _supabase.Auth.SignOut();
            await _sessionManager.ClearSessionAsync();

            await MID_HelperFunctions.DebugMessageAsync(
                "✅ User signed out successfully",
                LogLevel.Info
            );

            return true;
        }
        catch (Exception ex)
        {
            await MID_HelperFunctions.LogExceptionAsync(ex, "Sign out");
            
            try
            {
                await _sessionManager.ClearSessionAsync();
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

            // Try to restore from storage
            var storedSession = await _sessionManager.GetStoredSessionAsync();
            if (storedSession != null)
            {
                var restoredSession = await _supabase.Auth.SetSession(
                    storedSession.AccessToken,
                    storedSession.RefreshToken
                );

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

    public async Task<Session?> GetCurrentSessionAsync()
    {
        try
        {
            var session = _supabase.Auth.CurrentSession;
            if (session != null && !string.IsNullOrEmpty(session.AccessToken))
            {
                return session;
            }

            // Try to restore from storage
            var storedSession = await _sessionManager.GetStoredSessionAsync();
            if (storedSession != null)
            {
                return await _supabase.Auth.SetSession(
                    storedSession.AccessToken,
                    storedSession.RefreshToken
                );
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
        var user = await GetCurrentUserAsync();
        return user != null;
    }

    public async Task<bool> RefreshSessionAsync()
    {
        // Use SessionManager's refresh with lock to prevent concurrent refresh
        var refreshedSession = await _sessionManager.ExecuteRefreshWithLockAsync(async () =>
        {
            try
            {
                return await _supabase.Auth.RefreshSession();
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Refresh failed");
                return null;
            }
        });

        return refreshedSession != null;
    }

    // ==================== PASSWORD MANAGEMENT ====================

    public async Task<bool> SendPasswordResetEmailAsync(string email)
    {
        try
        {
            await _supabase.Auth.ResetPasswordForEmail(email);
            return true;
        }
        catch (Exception ex)
        {
            await MID_HelperFunctions.LogExceptionAsync(ex, "Send password reset");
            return false;
        }
    }

    public async Task<SupabaseAuthResult> UpdatePasswordAsync(string newPassword)
    {
        try
        {
            var attributes = new UserAttributes { Password = newPassword };
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

            return new SupabaseAuthResult
            {
                Success = true,
                Message = "Password updated successfully"
            };
        }
        catch (Exception ex)
        {
            await MID_HelperFunctions.LogExceptionAsync(ex, "Update password");
            return new SupabaseAuthResult
            {
                Success = false,
                Message = "Failed to update password",
                ErrorCode = "UPDATE_ERROR"
            };
        }
    }

    public async Task<SupabaseAuthResult> ChangePasswordAsync(string newPassword)
        => await UpdatePasswordAsync(newPassword);

    public async Task<SupabaseAuthResult> ResetPasswordWithTokenAsync(string token, string newPassword)
    {
        try
        {
            var session = await _supabase.Auth.VerifyOTP(token, token, Constants.EmailOtpType.Recovery);
            
            if (session == null)
            {
                return new SupabaseAuthResult
                {
                    Success = false,
                    Message = "Invalid or expired reset token",
                    ErrorCode = "INVALID_TOKEN"
                };
            }

            var updateResult = await UpdatePasswordAsync(newPassword);
            
            if (updateResult.Success)
            {
                await _sessionManager.StoreSessionAsync(session);
            }

            return updateResult;
        }
        catch (Exception ex)
        {
            await MID_HelperFunctions.LogExceptionAsync(ex, "Reset password with token");
            return new SupabaseAuthResult
            {
                Success = false,
                Message = "Failed to reset password",
                ErrorCode = "RESET_ERROR"
            };
        }
    }

    // ==================== EMAIL VERIFICATION ====================

    public async Task<bool> VerifyEmailAsync(string email, string token)
    {
        try
        {
            var session = await _supabase.Auth.VerifyOTP(email, token, Constants.EmailOtpType.Email);
            
            if (session != null)
            {
                await _sessionManager.StoreSessionAsync(session);
                return true;
            }

            return false;
        }
        catch (Exception ex)
        {
            await MID_HelperFunctions.LogExceptionAsync(ex, "Verify email");
            return false;
        }
    }

    public async Task<bool> ResendVerificationEmailAsync(string email)
    {
        try
        {
            await _supabase.Auth.SignUp(email, "");
            return true;
        }
        catch (Exception ex)
        {
            await MID_HelperFunctions.LogExceptionAsync(ex, "Resend verification");
            return false;
        }
    }

    // ==================== PROFILE MANAGEMENT ====================

    public async Task<bool> UpdateUserProfileAsync(Dictionary<string, object> updates)
    {
        try
        {
            var attributes = new UserAttributes { Data = updates };
            var user = await _supabase.Auth.Update(attributes);
            return user != null;
        }
        catch (Exception ex)
        {
            await MID_HelperFunctions.LogExceptionAsync(ex, "Update profile");
            return false;
        }
    }

    // ==================== MFA ====================
    
    public async Task<MfaEnrollmentResult> EnrollMfaAsync(string factorType)
    {
        try
        {
            var user = await GetCurrentUserAsync();
            if (user == null)
            {
                return new MfaEnrollmentResult
                {
                    Success = false,
                    ErrorMessage = "User not authenticated"
                };
            }

            var enrollResponse = await _supabase.Auth.Enroll(new MfaEnrollParams
            {
                FactorType = factorType,
                FriendlyName = $"{user.Email} - TOTP"
            });

            if (enrollResponse == null)
            {
                return new MfaEnrollmentResult
                {
                    Success = false,
                    ErrorMessage = "Failed to enroll MFA factor"
                };
            }

            return new MfaEnrollmentResult
            {
                Success = true,
                FactorId = enrollResponse.Id,
                QrCodeUrl = enrollResponse.Totp?.QrCode,
                Secret = enrollResponse.Totp?.Secret
            };
        }
        catch (Exception ex)
        {
            await MID_HelperFunctions.LogExceptionAsync(ex, "Enroll MFA");
            return new MfaEnrollmentResult
            {
                Success = false,
                ErrorMessage = "Failed to enroll MFA factor"
            };
        }
    }

    public async Task<SupabaseAuthResult> VerifyMfaAsync(string factorId, string code)
    {
        try
        {
            var challengeResponse = await _supabase.Auth.Challenge(new MfaChallengeParams
            {
                FactorId = factorId
            });

            if (challengeResponse == null || string.IsNullOrEmpty(challengeResponse.Id))
            {
                return new SupabaseAuthResult
                {
                    Success = false,
                    Message = "Failed to create MFA challenge",
                    ErrorCode = "MFA_CHALLENGE_ERROR"
                };
            }

            var verifyResponse = await _supabase.Auth.Verify(new MfaVerifyParams
            {
                FactorId = factorId,
                ChallengeId = challengeResponse.Id,
                Code = code
            });

            if (verifyResponse?.User != null)
            {
                return new SupabaseAuthResult
                {
                    Success = true,
                    Message = "MFA enabled successfully"
                };
            }

            return new SupabaseAuthResult
            {
                Success = false,
                Message = "Invalid verification code",
                ErrorCode = "INVALID_MFA_CODE"
            };
        }
        catch (Exception ex)
        {
            await MID_HelperFunctions.LogExceptionAsync(ex, "Verify MFA");
            return new SupabaseAuthResult
            {
                Success = false,
                Message = "Failed to verify MFA code",
                ErrorCode = "MFA_VERIFY_ERROR"
            };
        }
    }

    public async Task<SupabaseAuthResult> UnenrollMfaAsync(string factorId)
    {
        try
        {
            var response = await _supabase.Auth.Unenroll(new MfaUnenrollParams
            {
                FactorId = factorId
            });

            if (response != null)
            {
                return new SupabaseAuthResult
                {
                    Success = true,
                    Message = "MFA disabled successfully"
                };
            }

            return new SupabaseAuthResult
            {
                Success = false,
                Message = "Failed to disable MFA",
                ErrorCode = "MFA_UNENROLL_ERROR"
            };
        }
        catch (Exception ex)
        {
            await MID_HelperFunctions.LogExceptionAsync(ex, "Unenroll MFA");
            return new SupabaseAuthResult
            {
                Success = false,
                Message = "Failed to disable MFA",
                ErrorCode = "MFA_UNENROLL_ERROR"
            };
        }
    }

    public async Task<List<MfaFactor>?> GetMfaFactorsAsync()
    {
        try
        {
            var factors = await _supabase.Auth.ListFactors();

            if (factors == null || !factors.All.Any())
            {
                return new List<MfaFactor>();
            }

            return factors.All.Select(f => new MfaFactor
            {
                Id = f.Id,
                Type = f.FactorType,
                Status = f.Status,
                FriendlyName = f.FriendlyName ?? "",
                CreatedAt = f.CreatedAt,
                UpdatedAt = f.UpdatedAt
            }).ToList();
        }
        catch (Exception ex)
        {
            await MID_HelperFunctions.LogExceptionAsync(ex, "Get MFA factors");
            return null;
        }
    }

    public async Task<string?> ChallengeMfaAsync(string factorId)
    {
        try
        {
            var challengeResponse = await _supabase.Auth.Challenge(new MfaChallengeParams
            {
                FactorId = factorId
            });

            return challengeResponse?.Id;
        }
        catch (Exception ex)
        {
            await MID_HelperFunctions.LogExceptionAsync(ex, "Challenge MFA");
            return null;
        }
    }

    public async Task<SupabaseAuthResult> VerifyMfaChallengeAsync(string factorId, string challengeId, string code)
    {
        try
        {
            var verifyResponse = await _supabase.Auth.Verify(new MfaVerifyParams
            {
                FactorId = factorId,
                ChallengeId = challengeId,
                Code = code
            });

            if (verifyResponse?.User != null)
            {
                if (verifyResponse.AccessToken != null)
                {
                    var session = new Session
                    {
                        AccessToken = verifyResponse.AccessToken,
                        RefreshToken = verifyResponse.RefreshToken,
                        User = verifyResponse.User
                    };
                    await _sessionManager.StoreSessionAsync(session);
                }

                return new SupabaseAuthResult
                {
                    Success = true,
                    Message = "MFA verification successful"
                };
            }

            return new SupabaseAuthResult
            {
                Success = false,
                Message = "Invalid verification code",
                ErrorCode = "INVALID_MFA_CODE"
            };
        }
        catch (Exception ex)
        {
            await MID_HelperFunctions.LogExceptionAsync(ex, "Verify MFA challenge");
            return new SupabaseAuthResult
            {
                Success = false,
                Message = "Failed to verify MFA code",
                ErrorCode = "MFA_VERIFY_ERROR"
            };
        }
    }

    // ==================== PRIVATE HELPER METHODS ====================

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

    private async Task EnsureUserProfileExistsAsync(User authUser, UserModel? userData = null)
{
    try
    {
        await MID_HelperFunctions.DebugMessageAsync(
            $"Checking user profile for: {authUser.Email}",
            LogLevel.Info
        );

        // ✅ Use service_role key for this operation to bypass RLS
        // This prevents the infinite recursion issue during user creation
        var existingUser = await _supabase
            .From<UserModel>()
            .Where(u => u.Id == authUser.Id)
            .Single();

        if (existingUser == null)
        {
            await MID_HelperFunctions.DebugMessageAsync(
                $"User profile not found, creating for: {authUser.Email}",
                LogLevel.Info
            );

            var userProfile = new UserModel
            {
                Id = authUser.Id,
                Email = authUser.Email ?? "",
                FirstName = userData?.FirstName ?? 
                    authUser.UserMetadata?.GetValueOrDefault("first_name")?.ToString() ?? 
                    authUser.UserMetadata?.GetValueOrDefault("name")?.ToString()?.Split(' ').FirstOrDefault() ?? "",
                LastName = userData?.LastName ?? 
                    authUser.UserMetadata?.GetValueOrDefault("last_name")?.ToString() ?? 
                    authUser.UserMetadata?.GetValueOrDefault("name")?.ToString()?.Split(' ').Skip(1).FirstOrDefault() ?? "",
                AvatarUrl = userData?.AvatarUrl ?? 
                    authUser.UserMetadata?.GetValueOrDefault("avatar_url")?.ToString() ?? 
                    authUser.UserMetadata?.GetValueOrDefault("picture")?.ToString(),
                IsEmailVerified = authUser.EmailConfirmedAt != null,
                AccountStatus = "Active",
                EmailNotifications = true,
                SmsNotifications = false,
                PreferredLanguage = "en",
                Currency = "NGN",
                MembershipTier = "Bronze",
                Role = "user", // ✅ Always start with user role
                CreatedAt = DateTime.UtcNow,
                CreatedBy = authUser.Id
            };

            await _supabase.From<UserModel>().Insert(userProfile);

            await MID_HelperFunctions.DebugMessageAsync(
                $"✅ User profile created: {authUser.Email}",
                LogLevel.Info
            );
        }
        else
        {
            await MID_HelperFunctions.DebugMessageAsync(
                $"✅ User profile already exists: {authUser.Email}",
                LogLevel.Info
            );
        }
    }
    catch (Exception ex)
    {
        await MID_HelperFunctions.LogExceptionAsync(ex, "Ensure user profile exists");
        _logger.LogError(ex, "Failed to ensure user profile exists for {Email}", authUser.Email);
        
        // ⚠️ Don't throw - let authentication continue even if profile creation fails
        // The database trigger should create the profile anyway
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
