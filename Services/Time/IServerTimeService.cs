namespace SubashaVentures.Services.Time;

/// <summary>
/// Service for server time synchronization
/// </summary>
public interface IServerTimeService
{
    /// <summary>
    /// Get current server time
    /// </summary>
    Task<DateTime> GetCurrentServerTimeAsync();
    
    /// <summary>
    /// Sync with server time
    /// </summary>
    Task<bool> SyncWithServerAsync();
    
    /// <summary>
    /// Get cached server time (calculated from last sync)
    /// </summary>
    DateTime GetCachedServerTime();
    
    /// <summary>
    /// Check if within business hours
    /// </summary>
    bool IsBusinessHours();
    
    /// <summary>
    /// Get time since last sync
    /// </summary>
    TimeSpan GetTimeSinceLastSync();
}