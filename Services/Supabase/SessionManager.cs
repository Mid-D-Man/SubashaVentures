// Services/Supabase/SessionManager.cs - COMPLETE WITH PKCE FIX
using System.Threading;
using SubashaVentures.Services.Storage;
using SubashaVentures.Utilities.HelperScripts;
using Supabase.Gotrue;
using LogLevel = SubashaVentures.Utilities.Logging.LogLevel;

namespace SubashaVentures.Services.Supabase;

/// <summary>
/// Manages Supabase session persistence and PKCE flow
/// Handles access tokens, refresh tokens, and OAuth state
/// </summary>
public class SessionManager
{
    private const string AccessTokenKey = "supabase_access_token";
    private const string RefreshTokenKey = "supabase_refresh_token";
    private const string SessionExpiryKey = "supabase_session_expiry";
    private const string PkceVerifierKey = "supabase_pkce_verifier";
    private const string OAuthReturnUrlKey = "oauth_return_url";
    
    private readonly IBlazorAppLocalStorageService _localStorage;
    private readonly ILogger<SessionManager> _logger;
    
    private readonly SemaphoreSlim _refreshLock = new(1, 1);
    private DateTime _lastRefreshAttempt = DateTime.MinValue;
    private const int RefreshCooldownSeconds = 10;
    
    public SessionManager(
        IBlazorAppLocalStorageService localStorage,
        ILogger<SessionManager> logger)
    {
        _localStorage = localStorage;
        _logger = logger;
    }

    // ==================== SESSION STORAGE ====================

    /// <summary>
    /// Get stored session from localStorage
    /// </summary>
    public async Task<StoredSession?> GetStoredSessionAsync()
    {
        try
        {
            var accessToken = await _localStorage.GetItemAsync<string>(AccessTokenKey);
            var refreshToken = await _localStorage.GetItemAsync<string>(RefreshTokenKey);
            var expiryString = await _localStorage.GetItemAsync<string>(SessionExpiryKey);

            if (string.IsNullOrEmpty(accessToken) || string.IsNullOrEmpty(refreshToken))
            {
                return null;
            }

            DateTime? expiry = null;
            if (!string.IsNullOrEmpty(expiryString) && 
                DateTime.TryParse(expiryString, out var parsedExpiry))
            {
                expiry = parsedExpiry;
            }

            return new StoredSession
            {
                AccessToken = accessToken,
                RefreshToken = refreshToken,
                ExpiresAt = expiry
            };
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error getting stored session");
            return null;
        }
    }

    /// <summary>
    /// Store session to localStorage
    /// </summary>
    public async Task StoreSessionAsync(Session session)
    {
        try
        {
            await _localStorage.SetItemAsync(AccessTokenKey, session.AccessToken);
            await _localStorage.SetItemAsync(RefreshTokenKey, session.RefreshToken ?? "");
            await _localStorage.SetItemAsync(SessionExpiryKey, session.ExpiresAt().ToString("o"));

            await MID_HelperFunctions.DebugMessageAsync(
                $"✅ Session stored (expires: {session.ExpiresAt():yyyy-MM-dd HH:mm:ss})",
                LogLevel.Debug
            );
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error storing session");
            throw;
        }
    }

    /// <summary>
    /// Clear all session data from localStorage
    /// </summary>
    public async Task ClearSessionAsync()
    {
        try
        {
            await _localStorage.RemoveItemAsync(AccessTokenKey);
            await _localStorage.RemoveItemAsync(RefreshTokenKey);
            await _localStorage.RemoveItemAsync(SessionExpiryKey);
            
            await MID_HelperFunctions.DebugMessageAsync(
                "✅ Session cleared",
                LogLevel.Debug
            );
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error clearing session");
        }
    }

    /// <summary>
    /// Check if session needs refresh (< 5 minutes until expiry)
    /// </summary>
    public bool ShouldRefresh(DateTime? expiresAt)
    {
        if (expiresAt == null) return true;
        
        var timeUntilExpiry = expiresAt.Value - DateTime.UtcNow;
        return timeUntilExpiry.TotalMinutes < 5;
    }

    /// <summary>
    /// Execute session refresh with lock to prevent concurrent refresh attempts
    /// </summary>
    public async Task<Session?> ExecuteRefreshWithLockAsync(Func<Task<Session?>> refreshFunc)
    {
        var timeSinceLastRefresh = DateTime.UtcNow - _lastRefreshAttempt;
        if (timeSinceLastRefresh.TotalSeconds < RefreshCooldownSeconds)
        {
            await MID_HelperFunctions.DebugMessageAsync(
                $"⏳ Refresh in cooldown ({RefreshCooldownSeconds - (int)timeSinceLastRefresh.TotalSeconds}s remaining)",
                LogLevel.Debug
            );
            return null;
        }

        await _refreshLock.WaitAsync();
        
        try
        {
            timeSinceLastRefresh = DateTime.UtcNow - _lastRefreshAttempt;
            if (timeSinceLastRefresh.TotalSeconds < RefreshCooldownSeconds)
            {
                return null;
            }

            await MID_HelperFunctions.DebugMessageAsync(
                "🔄 Executing session refresh (locked)",
                LogLevel.Info
            );

            _lastRefreshAttempt = DateTime.UtcNow;
            var session = await refreshFunc();

            if (session != null)
            {
                await StoreSessionAsync(session);
            }

            return session;
        }
        finally
        {
            _refreshLock.Release();
        }
    }

    // ==================== PKCE VERIFIER MANAGEMENT ====================

    /// <summary>
    /// Store PKCE verifier with retry logic for reliability
    /// Critical for OAuth PKCE flow
    /// </summary>
    public async Task StorePkceVerifier(string verifier)
    {
        try
        {
            await MID_HelperFunctions.DebugMessageAsync(
                $"💾 Storing PKCE verifier: {verifier.Substring(0, Math.Min(20, verifier.Length))}...",
                LogLevel.Info
            );

            // Store with retry logic for reliability
            for (int attempt = 1; attempt <= 3; attempt++)
            {
                try
                {
                    await _localStorage.SetItemAsync(PkceVerifierKey, verifier);
                    
                    // Small delay to ensure write completes
                    await Task.Delay(50);
                    
                    // Verify storage by reading back
                    var stored = await _localStorage.GetItemAsync<string>(PkceVerifierKey);
                    
                    if (!string.IsNullOrEmpty(stored) && stored == verifier)
                    {
                        await MID_HelperFunctions.DebugMessageAsync(
                            $"✅ PKCE verifier stored and verified (attempt {attempt})",
                            LogLevel.Info
                        );
                        
                        // Also store in static fallback
                        StaticAuthStorage.PkceVerifier = verifier;
                        
                        return;
                    }
                    
                    await MID_HelperFunctions.DebugMessageAsync(
                        $"⚠️ PKCE verifier verification failed (attempt {attempt})",
                        LogLevel.Warning
                    );
                }
                catch (Exception ex)
                {
                    _logger.LogWarning(ex, "PKCE storage attempt {Attempt} failed", attempt);
                }
                
                // Wait before retry
                if (attempt < 3)
                {
                    await Task.Delay(100 * attempt);
                }
            }
            
            // If we get here, localStorage failed but we have static fallback
            await MID_HelperFunctions.DebugMessageAsync(
                "⚠️ localStorage storage failed after 3 attempts, using static fallback only",
                LogLevel.Warning
            );
            
            StaticAuthStorage.PkceVerifier = verifier;
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to store PKCE verifier");
            
            // Last resort: static storage
            StaticAuthStorage.PkceVerifier = verifier;
            
            throw;
        }
    }

    /// <summary>
    /// Get PKCE verifier from storage with multiple fallback options
    /// </summary>
    public async Task<string?> GetPkceVerifier()
    {
        try
        {
            // Try localStorage first
            var verifier = await _localStorage.GetItemAsync<string>(PkceVerifierKey);
            
            if (!string.IsNullOrEmpty(verifier))
            {
                await MID_HelperFunctions.DebugMessageAsync(
                    $"✅ PKCE verifier retrieved from localStorage: {verifier.Substring(0, Math.Min(20, verifier.Length))}...",
                    LogLevel.Info
                );
                return verifier;
            }
            
            // Fallback to static storage
            if (!string.IsNullOrEmpty(StaticAuthStorage.PkceVerifier))
            {
                await MID_HelperFunctions.DebugMessageAsync(
                    $"✅ PKCE verifier retrieved from static fallback: {StaticAuthStorage.PkceVerifier.Substring(0, Math.Min(20, StaticAuthStorage.PkceVerifier.Length))}...",
                    LogLevel.Info
                );
                return StaticAuthStorage.PkceVerifier;
            }
            
            await MID_HelperFunctions.DebugMessageAsync(
                "❌ PKCE verifier not found in any storage",
                LogLevel.Error
            );
            
            return null;
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to get PKCE verifier");
            
            // Try static storage as last resort
            return StaticAuthStorage.PkceVerifier;
        }
    }

    /// <summary>
    /// Clear PKCE verifier from all storage locations
    /// </summary>
    public async Task ClearPkceVerifier()
    {
        try
        {
            await _localStorage.RemoveItemAsync(PkceVerifierKey);
            StaticAuthStorage.PkceVerifier = null;
            
            await MID_HelperFunctions.DebugMessageAsync(
                "✅ PKCE verifier cleared from all storage",
                LogLevel.Debug
            );
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to clear PKCE verifier");
            
            // Still clear static storage
            StaticAuthStorage.PkceVerifier = null;
        }
    }

    // ==================== OAUTH RETURN URL MANAGEMENT ====================

    /// <summary>
    /// Store OAuth return URL for redirect after authentication
    /// </summary>
    public async Task StoreOAuthReturnUrl(string url)
    {
        try
        {
            await _localStorage.SetItemAsync(OAuthReturnUrlKey, url);
            
            await MID_HelperFunctions.DebugMessageAsync(
                $"✅ OAuth return URL stored: {url}",
                LogLevel.Debug
            );
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to store OAuth return URL");
        }
    }

    /// <summary>
    /// Get stored OAuth return URL
    /// </summary>
    public async Task<string?> GetOAuthReturnUrl()
    {
        try
        {
            return await _localStorage.GetItemAsync<string>(OAuthReturnUrlKey);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to get OAuth return URL");
            return null;
        }
    }

    /// <summary>
    /// Clear OAuth return URL from storage
    /// </summary>
    public async Task ClearOAuthReturnUrl()
    {
        try
        {
            await _localStorage.RemoveItemAsync(OAuthReturnUrlKey);
            
            await MID_HelperFunctions.DebugMessageAsync(
                "✅ OAuth return URL cleared",
                LogLevel.Debug
            );
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to clear OAuth return URL");
        }
    }
}

/// <summary>
/// Stored session data model
/// </summary>
public class StoredSession
{
    public string AccessToken { get; set; } = string.Empty;
    public string RefreshToken { get; set; } = string.Empty;
    public DateTime? ExpiresAt { get; set; }
}

/// <summary>
/// Static storage fallback for PKCE verifier
/// Used when localStorage is unreliable (timing issues, browser restrictions)
/// </summary>
public static class StaticAuthStorage
{
    public static string? PkceVerifier { get; set; }
}
