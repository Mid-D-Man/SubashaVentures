// ConnectivityService.cs
using Microsoft.JSInterop;
using System.Text.Json;
using SubashaVentures.Domain.ValueObjects;
using SubashaVentures.Utilities.HelperScripts;

public class ConnectivityService : IAsyncDisposable
{
    private readonly IJSRuntime _jsRuntime;
    private readonly DotNetObjectReference<ConnectivityService> _dotNetRef;
    private bool _isOnline = true;
    private string _networkQuality = "unknown";
    private string _connectionStability = "unknown";
    private DateTime? _lastOnlineTime;

    public event Action<ConnectivityStatus>? ConnectivityChanged;

    public ConnectivityService(IJSRuntime jsRuntime)
    {
        _jsRuntime = jsRuntime;
        _dotNetRef = DotNetObjectReference.Create(this);
    }

    public async Task InitializeAsync()
    {
        try
        {
            await _jsRuntime.InvokeVoidAsync("connectivityChecker.init", _dotNetRef);
            
            // Get initial status
            var status = await GetConnectivityStatusAsync();
            _isOnline = status.IsOnline;
            _networkQuality = status.NetworkQuality;
            _connectionStability = status.ConnectionStability;
            _lastOnlineTime = status.LastOnlineTime;
        }
        catch (Exception ex)
        {
            MID_HelperFunctions.DebugMessage($"Failed to initialize connectivity service: {ex.Message}");
        }
    }

    public async Task<ConnectivityStatus> GetConnectivityStatusAsync()
    {
        try
        {
            var status = await _jsRuntime.InvokeAsync<JsonElement>("connectivityChecker.getOnlineStatus");
            
            return new ConnectivityStatus
            {
                IsOnline = status.GetProperty("isOnline").GetBoolean(),
                NetworkQuality = status.GetProperty("networkQuality").GetString() ?? "unknown",
                ConnectionStability = status.GetProperty("connectionStability").GetString() ?? "unknown",
                LastOnlineTime = status.TryGetProperty("lastOnlineTime", out var timeElement) && 
                                timeElement.ValueKind != JsonValueKind.Null
                    ? DateTimeOffset.FromUnixTimeMilliseconds(timeElement.GetInt64()).DateTime
                    : null
            };
        }
        catch (Exception ex)
        {
            MID_HelperFunctions.DebugMessage($"Failed to get connectivity status: {ex.Message}");
            return new ConnectivityStatus { IsOnline = false, NetworkQuality = "unknown" };
        }
    }

    public async Task<bool> GetSimpleOnlineStatusAsync()
    {
        try
        {
            return await _jsRuntime.InvokeAsync<bool>("connectivityChecker.getSimpleOnlineStatus");
        }
        catch
        {
            return false;
        }
    }

    public async Task ForceCheckAsync()
    {
        try
        {
            await _jsRuntime.InvokeVoidAsync("connectivityChecker.forceCheck");
        }
        catch (Exception ex)
        {
            MID_HelperFunctions.DebugMessage($"Failed to force connectivity check: {ex.Message}");
        }
    }

    public async Task<ConnectivityReport> GetDetailedReportAsync()
    {
        try
        {
            var report = await _jsRuntime.InvokeAsync<JsonElement>("connectivityChecker.getConnectivityReport");
            
            return new ConnectivityReport
            {
                IsOnline = report.GetProperty("isOnline").GetBoolean(),
                NetworkQuality = report.GetProperty("networkQuality").GetString() ?? "unknown",
                ConnectionStability = report.GetProperty("connectionStability").GetString() ?? "unknown",
                LastOnlineTime = report.TryGetProperty("lastOnlineTime", out var timeElement) && 
                                timeElement.ValueKind != JsonValueKind.Null
                    ? DateTimeOffset.FromUnixTimeMilliseconds(timeElement.GetInt64()).DateTime
                    : null,
                HasConnection = report.GetProperty("hasConnection").GetBoolean(),
                ConnectionHistory = report.GetProperty("connectionHistory").EnumerateArray()
                    .Select(x => x.GetBoolean()).ToList(),
                ConnectionInfo = report.TryGetProperty("connectionInfo", out var connInfo) && 
                               connInfo.ValueKind != JsonValueKind.Null
                    ? JsonSerializer.Deserialize<ConnectionInfo>(connInfo.GetRawText())
                    : null
            };
        }
        catch (Exception ex)
        {
            MID_HelperFunctions.DebugMessage($"Failed to get detailed connectivity report: {ex.Message}");
            return new ConnectivityReport { IsOnline = false, NetworkQuality = "unknown" };
        }
    }

    [JSInvokable]
    public async Task OnConnectivityChanged(JsonElement statusInfo)
    {
        try
        {
            var status = new ConnectivityStatus
            {
                IsOnline = statusInfo.GetProperty("isOnline").GetBoolean(),
                NetworkQuality = statusInfo.GetProperty("networkQuality").GetString() ?? "unknown",
                ConnectionStability = statusInfo.GetProperty("connectionStability").GetString() ?? "unknown",
                LastOnlineTime = statusInfo.TryGetProperty("lastOnlineTime", out var timeElement) && 
                                timeElement.ValueKind != JsonValueKind.Null
                    ? DateTimeOffset.FromUnixTimeMilliseconds(timeElement.GetInt64()).DateTime
                    : null
            };

            _isOnline = status.IsOnline;
            _networkQuality = status.NetworkQuality;
            _connectionStability = status.ConnectionStability;
            _lastOnlineTime = status.LastOnlineTime;

            ConnectivityChanged?.Invoke(status);
        }
        catch (Exception ex)
        {
            MID_HelperFunctions.DebugMessage($"Error processing connectivity change: {ex.Message}");
        }
    }

    // Properties for quick access
    public bool IsOnline => _isOnline;
    public string NetworkQuality => _networkQuality;
    public string ConnectionStability => _connectionStability;
    public DateTime? LastOnlineTime => _lastOnlineTime;

    public async ValueTask DisposeAsync()
    {
        try
        {
            await _jsRuntime.InvokeVoidAsync("connectivityChecker.dispose");
        }
        catch { }
        
        _dotNetRef?.Dispose();
    }
}

