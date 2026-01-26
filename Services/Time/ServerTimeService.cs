// Services/Time/ServerTimeService.cs - COMPLETE REWRITE
using System.Diagnostics;
using SubashaVentures.Services.Storage;
using SubashaVentures.Services.Supabase;
using SubashaVentures.Utilities.HelperScripts;
using LogLevel = SubashaVentures.Utilities.Logging.LogLevel;

namespace SubashaVentures.Services.Time;

public class ServerTimeService : IServerTimeService
{
    private readonly ISupabaseEdgeFunctionService _edgeFunctions;
    private readonly IBlazorAppLocalStorageService _localStorage;
    private readonly ILogger<ServerTimeService> _logger;
    private readonly Stopwatch _serverTimeStopwatch;
    
    private DateTime _serverStartUtc = DateTime.UtcNow;
    private bool _isServerTimeSynced = false;
    private DateTime _lastSyncTime = DateTime.MinValue;
    
    // Storage keys
    private const string SERVER_TIME_KEY = "ServerSyncTime";
    private const string STOPWATCH_ELAPSED_KEY = "StopwatchElapsed";
    private const string LAST_SYNC_KEY = "LastSyncTime";
    private const string IS_SYNCED_KEY = "IsTimeSynced";
    
    // Delivery configurations
    private static readonly List<DeliveryLocation> SupportedLocations = new()
    {
        new DeliveryLocation
        {
            City = "Kaduna",
            State = "Kaduna",
            DeliveryHours = 24,
            IsPrimaryBase = true,
            Description = "Express delivery within 24 hours"
        },
        new DeliveryLocation
        {
            City = "Abuja",
            State = "FCT",
            DeliveryHours = 72, // 3 days
            IsPrimaryBase = false,
            Description = "Standard delivery within 3 days"
        }
    };

    public ServerTimeService(
        ISupabaseEdgeFunctionService edgeFunctions,
        IBlazorAppLocalStorageService localStorage,
        ILogger<ServerTimeService> logger)
    {
        _edgeFunctions = edgeFunctions;
        _localStorage = localStorage;
        _logger = logger;
        _serverTimeStopwatch = new Stopwatch();
        
        // Initialize in background - don't block constructor
        _ = InitializeAsync();
    }

    /// <summary>
    /// Initialize - loads from storage or syncs once at startup
    /// </summary>
    private async Task InitializeAsync()
    {
        try
        {
            // Try to load from storage first
            var wasRestored = await LoadServerTimeFromStorageAsync();
            
            if (!wasRestored)
            {
                // First time or storage expired - sync with server
                await MID_HelperFunctions.DebugMessageAsync(
                    "🕐 First session or storage expired - syncing server time",
                    LogLevel.Info
                );
                await ForceSyncAsync();
            }
            else
            {
                await MID_HelperFunctions.DebugMessageAsync(
                    "✅ Server time restored from storage",
                    LogLevel.Info
                );
            }
        }
        catch (Exception ex)
        {
            await MID_HelperFunctions.LogExceptionAsync(ex, "Initializing server time service");
            _logger.LogError(ex, "Failed to initialize server time service");
            
            // Fallback to local time
            _isServerTimeSynced = false;
        }
    }

    public async Task<DateTime> GetCurrentServerTimeAsync()
    {
        // If not synced yet, sync now
        if (!_isServerTimeSynced)
        {
            await ForceSyncAsync();
        }

        return GetCachedServerTime();
    }

    public DateTime GetCachedServerTime()
    {
        if (!_isServerTimeSynced || !_serverTimeStopwatch.IsRunning)
        {
            // Fallback to local UTC if not synced
            return DateTime.UtcNow;
        }

        // Return server time + elapsed since sync
        return _serverStartUtc.Add(_serverTimeStopwatch.Elapsed);
    }

    public bool IsTimeSynced()
    {
        return _isServerTimeSynced;
    }

    public TimeSpan GetTimeSinceLastSync()
    {
        if (_lastSyncTime == DateTime.MinValue)
            return TimeSpan.MaxValue;
            
        return DateTime.UtcNow - _lastSyncTime;
    }

    public async Task<bool> ForceSyncAsync()
    {
        try
        {
            await MID_HelperFunctions.DebugMessageAsync(
                "🔄 Forcing server time sync...",
                LogLevel.Info
            );

            // Call edge function to get server time
            var serverTime = await _edgeFunctions.GetServerTimeAsync("utc");

            if (serverTime == default || serverTime.Year < 2020)
            {
                _logger.LogWarning("Invalid server time received, using local time");
                return false;
            }

            // Set synced time
            _serverStartUtc = serverTime;
            _serverTimeStopwatch.Restart();
            _isServerTimeSynced = true;
            _lastSyncTime = DateTime.UtcNow;

            // Save to local storage
            await SaveServerTimeToStorageAsync();

            await MID_HelperFunctions.DebugMessageAsync(
                $"✅ Server time synced successfully: {serverTime:yyyy-MM-dd HH:mm:ss} UTC",
                LogLevel.Info
            );

            return true;
        }
        catch (Exception ex)
        {
            await MID_HelperFunctions.LogExceptionAsync(ex, "Force syncing server time");
            _logger.LogError(ex, "Failed to sync server time");
            
            // Use local time as fallback
            _isServerTimeSynced = false;
            return false;
        }
    }

    public async Task<DateTime> CalculateEstimatedDeliveryAsync(string deliveryLocation)
    {
        // Force sync to get accurate current time
        await ForceSyncAsync();

        var currentServerTime = GetCachedServerTime();
        var deliveryHours = GetDeliveryWindowHours(deliveryLocation);

        // Calculate estimated delivery time
        var estimatedDelivery = currentServerTime.AddHours(deliveryHours);

        await MID_HelperFunctions.DebugMessageAsync(
            $"📦 Estimated delivery for {deliveryLocation}: {estimatedDelivery:yyyy-MM-dd HH:mm} " +
            $"({deliveryHours} hours from now)",
            LogLevel.Info
        );

        return estimatedDelivery;
    }

    public int GetDeliveryWindowHours(string deliveryLocation)
    {
        var location = SupportedLocations.FirstOrDefault(l => 
            l.City.Equals(deliveryLocation, StringComparison.OrdinalIgnoreCase) ||
            l.State.Equals(deliveryLocation, StringComparison.OrdinalIgnoreCase));

        if (location != null)
        {
            return location.DeliveryHours;
        }

        // Default to Abuja timing if location not found
        _logger.LogWarning("Delivery location not found: {Location}, using default 72 hours", deliveryLocation);
        return 72; // 3 days default
    }

    public bool IsDeliverySupported(string city, string state)
    {
        return SupportedLocations.Any(l =>
            (string.IsNullOrEmpty(city) || l.City.Equals(city, StringComparison.OrdinalIgnoreCase)) &&
            (string.IsNullOrEmpty(state) || l.State.Equals(state, StringComparison.OrdinalIgnoreCase)));
    }

    public List<DeliveryLocation> GetSupportedLocations()
    {
        return new List<DeliveryLocation>(SupportedLocations);
    }

    // ==================== PRIVATE HELPER METHODS ====================

    private async Task SaveServerTimeToStorageAsync()
    {
        try
        {
            await _localStorage.SetItemAsync(SERVER_TIME_KEY, _serverStartUtc.ToString("O"));
            await _localStorage.SetItemAsync(STOPWATCH_ELAPSED_KEY, _serverTimeStopwatch.ElapsedMilliseconds);
            await _localStorage.SetItemAsync(LAST_SYNC_KEY, _lastSyncTime.ToString("O"));
            await _localStorage.SetItemAsync(IS_SYNCED_KEY, _isServerTimeSynced);

            _logger.LogDebug("Server time saved to storage");
        }
        catch (Exception ex)
        {
            await MID_HelperFunctions.LogExceptionAsync(ex, "Saving server time to storage");
        }
    }

    private async Task<bool> LoadServerTimeFromStorageAsync()
    {
        try
        {
            var isSynced = await _localStorage.GetItemAsync<bool>(IS_SYNCED_KEY);
            
            if (!isSynced)
            {
                return false; // Never synced before
            }

            var serverTimeString = await _localStorage.GetItemAsync<string>(SERVER_TIME_KEY);
            var elapsedMs = await _localStorage.GetItemAsync<long>(STOPWATCH_ELAPSED_KEY);
            var lastSyncString = await _localStorage.GetItemAsync<string>(LAST_SYNC_KEY);
            
            if (string.IsNullOrEmpty(serverTimeString) || string.IsNullOrEmpty(lastSyncString))
            {
                return false;
            }

            if (!DateTime.TryParse(serverTimeString, out var storedServerTime) ||
                !DateTime.TryParse(lastSyncString, out var lastSync))
            {
                return false;
            }

            // Check if sync is stale (more than 7 days old)
            var syncAge = DateTime.UtcNow - lastSync;
            if (syncAge.TotalDays > 7)
            {
                _logger.LogWarning("Stored server time is stale ({Days} days old), will re-sync", syncAge.TotalDays);
                return false;
            }

            // Restore state
            _serverStartUtc = storedServerTime;
            _lastSyncTime = lastSync;
            _isServerTimeSynced = true;
            
            // Restart stopwatch and fast-forward by elapsed time
            _serverTimeStopwatch.Start();
            var timeSinceStorage = DateTime.UtcNow - lastSync;
            _serverStartUtc = _serverStartUtc.Add(timeSinceStorage);

            _logger.LogInformation("Server time restored from storage (synced {Age} ago)", 
                syncAge.TotalHours > 24 ? $"{syncAge.TotalDays:F1} days" : $"{syncAge.TotalHours:F1} hours");

            return true;
        }
        catch (Exception ex)
        {
            await MID_HelperFunctions.LogExceptionAsync(ex, "Loading server time from storage");
            _logger.LogError(ex, "Failed to load server time from storage");
            return false;
        }
    }
}
