
using System.Diagnostics;
using SubashaVentures.Services.Storage;
using SubashaVentures.Services.SupaBase;
using SubashaVentures.Utilities.HelperScripts;

namespace SubashaVentures.Services.Time
{
    public class ServerTimeService : IServerTimeService
    {
        private readonly ISupabaseEdgeFunctionService _edgeFunctionService;
        private readonly IBlazorAppLocalStorageService _localStorage;
        private readonly Stopwatch _serverTimeStopwatch;
        
        private DateTime _serverStartUtc = DateTime.UtcNow;
        private bool _isServerTimeSynced = false;
        
        private const string SERVER_TIME_KEY = "ServerSyncTime";
        private const string STOPWATCH_ELAPSED_KEY = "StopwatchElapsed";
        private const string LAST_SYNC_KEY = "LastSyncTime";
        private const int SYNC_EXPIRY_HOURS = 1; // Re-sync every hour

        public ServerTimeService(
            ISupabaseEdgeFunctionService edgeFunctionService,
            IBlazorAppLocalStorageService localStorage)
        {
            _edgeFunctionService = edgeFunctionService;
            _localStorage = localStorage;
            _serverTimeStopwatch = new Stopwatch();
            _ = InitializeAsync(); // Fire and forget initialization
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
                return DateTime.UtcNow; // Fallback to local time
            }

            return _serverStartUtc.Add(_serverTimeStopwatch.Elapsed);
        }

        public async Task<bool> SyncWithServerAsync()
        {
            try
            {
                var response = await _edgeFunctionService.GetServerTimeAsync();
                
                if (DateTime.TryParse(response, null, System.Globalization.DateTimeStyles.RoundtripKind, out DateTime serverTime))
                {
                    _serverStartUtc = serverTime;
                    _serverTimeStopwatch.Restart();
                    _isServerTimeSynced = true;

                    // Save to local storage
                    await _localStorage.SetItemAsync(SERVER_TIME_KEY, _serverStartUtc.ToString("O"));
                    await _localStorage.SetItemAsync(STOPWATCH_ELAPSED_KEY, _serverTimeStopwatch.ElapsedMilliseconds);
                    await _localStorage.SetItemAsync(LAST_SYNC_KEY, DateTime.UtcNow.ToString("O"));

                    return true;
                }
            }
            catch (Exception ex)
            {
                MID_HelperFunctions.DebugMessage($"Failed to sync server time: {ex.Message}");
            }

            return false;
        }

        public bool IsBusinessHours()
        {
            var currentTime = GetCachedServerTime();
            
            // Convert to local business timezone if needed (assuming UTC for now)
            var businessTime = currentTime;
            
            var isWeekday = businessTime.DayOfWeek != DayOfWeek.Saturday && 
                           businessTime.DayOfWeek != DayOfWeek.Sunday;
            
            var isBusinessHours = businessTime.Hour >= 8 && businessTime.Hour < 19; // 8 AM to 7 PM
            
            return isWeekday && isBusinessHours;
        }

        private async Task<bool> ShouldResyncAsync()
        {
            var lastSyncString = await _localStorage.GetItemAsync<string>(LAST_SYNC_KEY);
            
            if (string.IsNullOrEmpty(lastSyncString))
                return true;

            if (DateTime.TryParse(lastSyncString, null, System.Globalization.DateTimeStyles.RoundtripKind, out DateTime lastSync))
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
                
                if (!string.IsNullOrEmpty(serverTimeString))
                {
                    if (DateTime.TryParse(serverTimeString, null, System.Globalization.DateTimeStyles.RoundtripKind, out DateTime storedServerTime))
                    {
                        _serverStartUtc = storedServerTime;
                        _serverTimeStopwatch.Start();
                        
                        // Adjust for time that passed while app was closed
                        var timeSinceStorage = TimeSpan.FromMilliseconds(elapsedMs);
                        _serverStartUtc = _serverStartUtc.Add(timeSinceStorage);
                        
                        _isServerTimeSynced = true;
                    }
                }
            }
            catch (Exception ex)
            {
                MID_HelperFunctions.DebugMessage($"Failed to load server time from storage: {ex.Message}");
                _isServerTimeSynced = false;
            }
        }
    }
}