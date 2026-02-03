// Services/Supabase/SessionManager.cs - ENHANCED
using System.Threading;
using SubashaVentures.Services.Storage;
using SubashaVentures.Utilities.HelperScripts;
using Supabase.Gotrue;
using LogLevel = SubashaVentures.Utilities.Logging.LogLevel;

namespace SubashaVentures.Services.Supabase;

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

    public bool ShouldRefresh(DateTime? expiresAt)
    {
        if (expiresAt == null) return true;
        
        var timeUntilExpiry = expiresAt.Value - DateTime.UtcNow;
        return timeUntilExpiry.TotalMinutes < 5;
    }

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

    // PKCE Verifier Management
    public async Task StorePkceVerifier(string verifier)
    {
        await _localStorage.SetItemAsync(PkceVerifierKey, verifier);
    }

    public async Task<string?> GetPkceVerifier()
    {
        return await _localStorage.GetItemAsync<string>(PkceVerifierKey);
    }

    public async Task ClearPkceVerifier()
    {
        await _localStorage.RemoveItemAsync(PkceVerifierKey);
    }

    // OAuth Return URL Management
    public async Task StoreOAuthReturnUrl(string url)
    {
        await _localStorage.SetItemAsync(OAuthReturnUrlKey, url);
    }

    public async Task<string?> GetOAuthReturnUrl()
    {
        return await _localStorage.GetItemAsync<string>(OAuthReturnUrlKey);
    }

    public async Task ClearOAuthReturnUrl()
    {
        await _localStorage.RemoveItemAsync(OAuthReturnUrlKey);
    }
}

public class StoredSession
{
    public string AccessToken { get; set; } = string.Empty;
    public string RefreshToken { get; set; } = string.Empty;
    public DateTime? ExpiresAt { get; set; }
}