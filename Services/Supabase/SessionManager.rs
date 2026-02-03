using System.Threading;
using SubashaVentures.Services.Storage;
using SubashaVentures.Utilities.HelperScripts;
using Supabase.Gotrue;
using LogLevel = SubashaVentures.Utilities.Logging.LogLevel;

namespace SubashaVentures.Services.Supabase;

/// <summary>
/// Manages Supabase session with refresh lock to prevent concurrent refresh attempts
/// CRITICAL: Prevents "refresh_token_already_used" errors
/// </summary>
public class SessionManager
{
    private const string AccessTokenKey = "supabase_access_token";
    private const string RefreshTokenKey = "supabase_refresh_token";
    private const string SessionExpiryKey = "supabase_session_expiry";
    
    private readonly IBlazorAppLocalStorageService _localStorage;
    private readonly ILogger<SessionManager> _logger;
    
    // Refresh lock - prevents concurrent refresh attempts
    private readonly SemaphoreSlim _refreshLock = new(1, 1);
    private DateTime _lastRefreshAttempt = DateTime.MinValue;
    private const int RefreshCooldownSeconds = 10; // Supabase's reuse interval
    
    public SessionManager(
        IBlazorAppLocalStorageService localStorage,
        ILogger<SessionManager> logger)
    {
        _localStorage = localStorage;
        _logger = logger;
    }

    /// <summary>
    /// Get stored session tokens (if valid)
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
                await MID_HelperFunctions.DebugMessageAsync(
                    "No stored session found",
                    LogLevel.Debug
                );
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
    /// Store session tokens
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
        }
    }

    /// <summary>
    /// Clear stored session
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
    /// Check if session needs refresh (within 5 minutes of expiry)
    /// </summary>
    public bool ShouldRefresh(DateTime? expiresAt)
    {
        if (expiresAt == null) return true;
        
        var timeUntilExpiry = expiresAt.Value - DateTime.UtcNow;
        return timeUntilExpiry.TotalMinutes < 5;
    }

    /// <summary>
    /// Execute refresh with lock to prevent concurrent attempts
    /// CRITICAL: This prevents "refresh_token_already_used" errors
    /// </summary>
    public async Task<Session?> ExecuteRefreshWithLockAsync(
        Func<Task<Session?>> refreshFunc)
    {
        // Check cooldown period
        var timeSinceLastRefresh = DateTime.UtcNow - _lastRefreshAttempt;
        if (timeSinceLastRefresh.TotalSeconds < RefreshCooldownSeconds)
        {
            await MID_HelperFunctions.DebugMessageAsync(
                $"⏳ Refresh in cooldown ({RefreshCooldownSeconds - (int)timeSinceLastRefresh.TotalSeconds}s remaining)",
                LogLevel.Debug
            );
            return null;
        }

        // Acquire lock (wait if another refresh is in progress)
        await _refreshLock.WaitAsync();
        
        try
        {
            // Double-check cooldown after acquiring lock
            timeSinceLastRefresh = DateTime.UtcNow - _lastRefreshAttempt;
            if (timeSinceLastRefresh.TotalSeconds < RefreshCooldownSeconds)
            {
                await MID_HelperFunctions.DebugMessageAsync(
                    "⏭️ Skipping refresh - another refresh just completed",
                    LogLevel.Debug
                );
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
                
                await MID_HelperFunctions.DebugMessageAsync(
                    "✅ Session refreshed successfully",
                    LogLevel.Info
                );
            }
            else
            {
                await MID_HelperFunctions.DebugMessageAsync(
                    "❌ Session refresh returned null",
                    LogLevel.Warning
                );
            }

            return session;
        }
        catch (Exception ex)
        {
            await MID_HelperFunctions.LogExceptionAsync(ex, "Session refresh");
            _logger.LogError(ex, "Error during locked session refresh");
            return null;
        }
        finally
        {
            _refreshLock.Release();
        }
    }
}

public class StoredSession
{
    public string AccessToken { get; set; } = string.Empty;
    public string RefreshToken { get; set; } = string.Empty;
    public DateTime? ExpiresAt { get; set; }
}
