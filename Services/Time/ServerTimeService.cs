using System.Diagnostics;
using SubashaVentures.Services.Storage;
using SubashaVentures.Utilities.HelperScripts;
using LogLevel = SubashaVentures.Utilities.Logging.LogLevel ;
namespace SubashaVentures.Services.Time;

public class ServerTimeService : IServerTimeService
{
    private readonly HttpClient _httpClient;
    private readonly IBlazorAppLocalStorageService _localStorage;
    private readonly ILogger<ServerTimeService> _logger;
    private readonly Stopwatch _serverTimeStopwatch;
    
    private DateTime _serverStartUtc = DateTime.UtcNow;
    private bool _isServerTimeSynced = false;
    private DateTime _lastSyncTime = DateTime.MinValue;
    
    private const string SERVER_TIME_KEY = "ServerSyncTime";
    private const string STOPWATCH_ELAPSED_KEY = "StopwatchElapsed";
    private const string LAST_SYNC_KEY = "LastSyncTime";
    private const int SYNC_EXPIRY_HOURS = 1;
    private const string TIME_API_URL = "https://worldtimeapi.org/api/timezone/Etc/UTC";

    public ServerTimeService(
        HttpClient httpClient,
        IBlazorAppLocalStorageService localStorage,
        ILogger<ServerTimeService> logger)
    {
        _httpClient = httpClient;
        _localStorage = localStorage;
        _logger = logger;
        _serverTimeStopwatch = new Stopwatch();
        
        _ = InitializeAsync();
    }

    private async Task InitializeAsync()
    {
        await LoadServerTimeFromStorageAsync();
    }

    public async Task<DateTime> GetCurrentServerTimeAsync()
    {
        if (!_isServerTimeSynced || await ShouldResyncAsync())
        {
            await SyncWithServerAsync();
        }

        return GetCachedServerTime();
    }

    public DateTime GetCachedServerTime()
    {
        if (!_isServerTimeSynced)
        {
            return DateTime.UtcNow;
        }

        return _serverStartUtc.Add(_serverTimeStopwatch.Elapsed);
    }

    public async Task<bool> SyncWithServerAsync()
    {
        try
        {
            var response = await _httpClient.GetAsync(TIME_API_URL);
            response.EnsureSuccessStatusCode();
            
            var content = await response.Content.ReadAsStringAsync();
            var timeData = JsonHelper.Deserialize<Dictionary<string, object>>(content);
            
            if (timeData != null && timeData.ContainsKey("utc_datetime"))
            {
                var utcTimeString = timeData["utc_datetime"].ToString();
                if (DateTime.TryParse(utcTimeString, out DateTime serverTime))
                {
                    _serverStartUtc = serverTime;
                    _serverTimeStopwatch.Restart();
                    _isServerTimeSynced = true;
                    _lastSyncTime = DateTime.UtcNow;

                    // Save to local storage
                    await _localStorage.SetItemAsync(SERVER_TIME_KEY, _serverStartUtc.ToString("O"));
                    await _localStorage.SetItemAsync(STOPWATCH_ELAPSED_KEY, _serverTimeStopwatch.ElapsedMilliseconds);
                    await _localStorage.SetItemAsync(LAST_SYNC_KEY, _lastSyncTime.ToString("O"));

                    _logger.LogInformation("Server time synced successfully");
                    return true;
                }
            }
        }
        catch (Exception ex)
        {
            await MID_HelperFunctions.DebugMessageAsync($"Failed to sync server time: {ex.Message}", LogLevel.Exception);
        }

        return false;
    }

    public bool IsBusinessHours()
    {
        var currentTime = GetCachedServerTime();
        var businessTime = currentTime;
        
        var isWeekday = businessTime.DayOfWeek != DayOfWeek.Saturday && 
                       businessTime.DayOfWeek != DayOfWeek.Sunday;
        
        var isBusinessHours = businessTime.Hour >= 8 && businessTime.Hour < 19;
        
        return isWeekday && isBusinessHours;
    }

    public TimeSpan GetTimeSinceLastSync()
    {
        if (_lastSyncTime == DateTime.MinValue)
            return TimeSpan.MaxValue;
            
        return DateTime.UtcNow - _lastSyncTime;
    }

    private async Task<bool> ShouldResyncAsync()
    {
        var lastSyncString = await _localStorage.GetItemAsync<string>(LAST_SYNC_KEY);
        
        if (string.IsNullOrEmpty(lastSyncString))
            return true;

        if (DateTime.TryParse(lastSyncString, out DateTime lastSync))
        {
            return DateTime.UtcNow.Subtract(lastSync).TotalHours > SYNC_EXPIRY_HOURS;
        }

        return true;
    }

    private async Task LoadServerTimeFromStorageAsync()
    {
        try
        {
            var serverTimeString = await _localStorage.GetItemAsync<string>(SERVER_TIME_KEY);
            var elapsedMs = await _localStorage.GetItemAsync<long>(STOPWATCH_ELAPSED_KEY);
            var lastSyncString = await _localStorage.GetItemAsync<string>(LAST_SYNC_KEY);
            
            if (!string.IsNullOrEmpty(serverTimeString) && !string.IsNullOrEmpty(lastSyncString))
            {
                if (DateTime.TryParse(serverTimeString, out DateTime storedServerTime) &&
                    DateTime.TryParse(lastSyncString, out DateTime lastSync))
                {
                    _serverStartUtc = storedServerTime;
                    _lastSyncTime = lastSync;
                    _serverTimeStopwatch.Start();
                    
                    var timeSinceStorage = TimeSpan.FromMilliseconds(elapsedMs);
                    _serverStartUtc = _serverStartUtc.Add(timeSinceStorage);
                    
                    _isServerTimeSynced = true;
                    _logger.LogInformation("Server time loaded from storage");
                }
            }
        }
        catch (Exception ex)
        {
            await MID_HelperFunctions.DebugMessageAsync($"Failed to load server time from storage: {ex.Message}", LogLevel.Exception);
            _isServerTimeSynced = false;
        }
    }
}