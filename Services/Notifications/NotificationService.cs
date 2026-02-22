// Services/Notifications/NotificationService.cs
using Microsoft.JSInterop;
using SubashaVentures.Utilities.HelperScripts;
using LogLevel = SubashaVentures.Utilities.Logging.LogLevel;

namespace SubashaVentures.Services.Notifications;

public class NotificationService : INotificationService
{
    private readonly IJSRuntime _jsRuntime;
    private readonly ILogger<NotificationService> _logger;

    private int _unreadMessageCount;
    private DateTime _lastRefresh = DateTime.MinValue;
    private readonly TimeSpan _cooldown = TimeSpan.FromSeconds(30);

    public event Action? OnChange;
    public int UnreadMessageCount => _unreadMessageCount;

    public NotificationService(IJSRuntime jsRuntime, ILogger<NotificationService> logger)
    {
        _jsRuntime = jsRuntime;
        _logger = logger;
    }

    public async Task RefreshAsync(string userId)
    {
        if (string.IsNullOrWhiteSpace(userId)) return;
        if (DateTime.UtcNow - _lastRefresh < _cooldown) return;

        try
        {
            _lastRefresh = DateTime.UtcNow;

            var count = await _jsRuntime.InvokeAsync<int>(
                "messageHelper.getUserUnreadCount", userId);

            if (count != _unreadMessageCount)
            {
                _unreadMessageCount = count;

                await MID_HelperFunctions.DebugMessageAsync(
                    $"Unread message count updated: {count}", LogLevel.Debug);

                OnChange?.Invoke();
            }
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to refresh notification count for {UserId}", userId);
        }
    }

    public void Clear()
    {
        _unreadMessageCount = 0;
        _lastRefresh = DateTime.MinValue;
        OnChange?.Invoke();
    }
}
