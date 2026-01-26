// Services/Time/IServerTimeService.cs - UPDATED
namespace SubashaVentures.Services.Time;

/// <summary>
/// Service for server time synchronization
/// Syncs once at session start, then only on-demand (e.g., order placement)
/// </summary>
public interface IServerTimeService
{
    /// <summary>
    /// Get current server time (syncs if not already synced)
    /// </summary>
    Task<DateTime> GetCurrentServerTimeAsync();
    
    /// <summary>
    /// Force sync with server time (used when placing orders)
    /// </summary>
    Task<bool> ForceSyncAsync();
    
    /// <summary>
    /// Get cached server time (calculated from last sync + stopwatch)
    /// Does NOT trigger a network call
    /// </summary>
    DateTime GetCachedServerTime();
    
    /// <summary>
    /// Check if server time has been synced at least once
    /// </summary>
    bool IsTimeSynced();
    
    /// <summary>
    /// Get time since last sync
    /// </summary>
    TimeSpan GetTimeSinceLastSync();
    
    /// <summary>
    /// Calculate estimated delivery time based on location
    /// </summary>
    /// <param name="deliveryLocation">City/State (e.g., "Kaduna", "Abuja")</param>
    /// <returns>Estimated delivery DateTime</returns>
    Task<DateTime> CalculateEstimatedDeliveryAsync(string deliveryLocation);
    
    /// <summary>
    /// Get delivery window in hours based on location
    /// </summary>
    /// <param name="deliveryLocation">City/State</param>
    /// <returns>Number of hours for delivery</returns>
    int GetDeliveryWindowHours(string deliveryLocation);
    
    /// <summary>
    /// Check if location is within supported delivery area
    /// </summary>
    /// <param name="city">City name</param>
    /// <param name="state">State name</param>
    /// <returns>True if we deliver to this location</returns>
    bool IsDeliverySupported(string city, string state);
    
    /// <summary>
    /// Get list of supported delivery locations
    /// </summary>
    List<DeliveryLocation> GetSupportedLocations();
}

/// <summary>
/// Supported delivery location
/// </summary>
public class DeliveryLocation
{
    public string City { get; set; } = string.Empty;
    public string State { get; set; } = string.Empty;
    public int DeliveryHours { get; set; }
    public bool IsPrimaryBase { get; set; }
    public string Description { get; set; } = string.Empty;
}
