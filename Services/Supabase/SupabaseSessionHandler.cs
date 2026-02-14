// Services/Supabase/SupabaseSessionHandler.cs - CORRECTED FOR SYNC INTERFACE
using Supabase.Gotrue.Interfaces;
using Supabase.Gotrue;
using Blazored.LocalStorage;
using System.Text.Json;

namespace SubashaVentures.Services.Supabase;

/// <summary>
/// Custom session handler implementing IGotrueSessionPersistence with SYNCHRONOUS methods
/// Uses Blazored.LocalStorage's synchronous API (ISyncLocalStorageService) for Blazor WASM
/// </summary>
public class SupabaseSessionHandler : IGotrueSessionPersistence<Session>
{
    private readonly ISyncLocalStorageService _syncLocalStorage;
    private readonly ILogger<SupabaseSessionHandler> _logger;
    
    private const string SessionKey = "supabase_session_v2";

    public SupabaseSessionHandler(
        ISyncLocalStorageService syncLocalStorage,
        ILogger<SupabaseSessionHandler> logger)
    {
        _syncLocalStorage = syncLocalStorage;
        _logger = logger;
    }

    /// <summary>
    /// Load session from LocalStorage (SYNCHRONOUS)
    /// </summary>
    public Session? LoadSession()
    {
        try
        {
            if (!_syncLocalStorage.ContainKey(SessionKey))
            {
                _logger.LogInformation("No stored session found");
                return null;
            }

            var sessionJson = _syncLocalStorage.GetItemAsString(SessionKey);
            
            if (string.IsNullOrEmpty(sessionJson))
            {
                _logger.LogWarning("Session key exists but value is empty");
                return null;
            }

            var session = JsonSerializer.Deserialize<Session>(sessionJson);
            
            if (session != null)
            {
                _logger.LogInformation("✓ Session loaded from storage (expires: {Expiry})", 
                    session.ExpiresAt());
            }
            
            return session;
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to load session from storage");
            return null;
        }
    }

    /// <summary>
    /// Save session to LocalStorage (SYNCHRONOUS)
    /// </summary>
    public void SaveSession(Session session)
    {
        try
        {
            if (session == null)
            {
                _logger.LogWarning("Attempted to save null session");
                return;
            }

            var sessionJson = JsonSerializer.Serialize(session);
            _syncLocalStorage.SetItemAsString(SessionKey, sessionJson);
            
            _logger.LogInformation("✓ Session saved to storage (expires: {Expiry})",
                session.ExpiresAt());
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to save session to storage");
        }
    }

    /// <summary>
    /// Destroy session in LocalStorage (SYNCHRONOUS)
    /// </summary>
    public void DestroySession()
    {
        try
        {
            if (_syncLocalStorage.ContainKey(SessionKey))
            {
                _syncLocalStorage.RemoveItem(SessionKey);
                _logger.LogInformation("✓ Session destroyed");
            }
            else
            {
                _logger.LogInformation("No session to destroy");
            }
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to destroy session");
        }
    }
}