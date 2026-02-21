// Services/Notifications/NotificationService.cs
using SubashaVentures.Utilities.HelperScripts;
using System.Text.Json;
using System.Text.Json.Serialization;
using Client = Supabase.Client;
using LogLevel = SubashaVentures.Utilities.Logging.LogLevel;

namespace SubashaVentures.Services.Notifications;

public class NotificationService : INotificationService
{
    private readonly Client _supabase;
    private readonly ILogger<NotificationService> _logger;

    private List<NotificationViewModel> _notifications = new();
    private int _unreadCount = 0;
    private DateTime _lastPoll = DateTime.MinValue;

    // Minimum gap between polls to avoid hammering Supabase
    private readonly TimeSpan _pollCooldown = TimeSpan.FromSeconds(30);

    public event Action? OnChange;
    public int UnreadCount => _unreadCount;
    public IReadOnlyList<NotificationViewModel> Notifications => _notifications;

    public NotificationService(Client supabase, ILogger<NotificationService> logger)
    {
        _supabase = supabase;
        _logger = logger;
    }

    // ── Polling ───────────────────────────────────────────────────────────────

    public async Task PollAsync(string userId)
    {
        if (string.IsNullOrWhiteSpace(userId)) return;

        // Respect cooldown so navigating quickly doesn't spam Supabase
        if (DateTime.UtcNow - _lastPoll < _pollCooldown) return;

        try
        {
            _lastPoll = DateTime.UtcNow;

            // Fetch last 30 notifications for the user, newest first
            var response = await _supabase
                .From<NotificationRow>()
                .Select("*")
                .Filter("user_id", Supabase.Postgrest.Constants.Operator.Equals, userId)
                .Order("created_at", Supabase.Postgrest.Constants.Ordering.Descending)
                .Limit(30)
                .Get();

            if (response?.Models == null) return;

            _notifications = response.Models
                .Where(n => n.ExpiresAt == null || n.ExpiresAt > DateTime.UtcNow)
                .Select(n => new NotificationViewModel
                {
                    Id        = n.Id,
                    Title     = n.Title,
                    Message   = n.Message,
                    Type      = n.Type,
                    IsRead    = n.IsRead,
                    ActionUrl = n.ActionUrl,
                    CreatedAt = n.CreatedAt,
                })
                .ToList();

            _unreadCount = _notifications.Count(n => !n.IsRead);

            await MID_HelperFunctions.DebugMessageAsync(
                $"📬 Notifications polled: {_notifications.Count} total, {_unreadCount} unread",
                LogLevel.Debug);

            OnChange?.Invoke();
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to poll notifications for user {UserId}", userId);
        }
    }

    // ── Mark Read ─────────────────────────────────────────────────────────────

    public async Task MarkReadAsync(Guid notificationId)
    {
        try
        {
            await _supabase
                .From<NotificationRow>()
                .Filter("id", Supabase.Postgrest.Constants.Operator.Equals, notificationId.ToString())
                .Set(n => n.IsRead, true)
                .Update();

            // Update local cache instantly (no need to re-poll)
            var local = _notifications.FirstOrDefault(n => n.Id == notificationId);
            if (local != null)
            {
                local.IsRead = true;
                _unreadCount = _notifications.Count(n => !n.IsRead);
                OnChange?.Invoke();
            }
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to mark notification {Id} as read", notificationId);
        }
    }

    public async Task MarkAllReadAsync(string userId)
    {
        try
        {
            await _supabase
                .From<NotificationRow>()
                .Filter("user_id", Supabase.Postgrest.Constants.Operator.Equals, userId)
                .Filter("is_read", Supabase.Postgrest.Constants.Operator.Equals, "false")
                .Set(n => n.IsRead, true)
                .Update();

            foreach (var n in _notifications) n.IsRead = true;
            _unreadCount = 0;
            OnChange?.Invoke();
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to mark all notifications read for {UserId}", userId);
        }
    }

    public void Clear()
    {
        _notifications.Clear();
        _unreadCount = 0;
        _lastPoll = DateTime.MinValue;
        OnChange?.Invoke();
    }
}

// ── Supabase Postgrest model ───────────────────────────────────────────────────
// Maps to the user_notifications table you created in SQL.

[Supabase.Postgrest.Attributes.Table("user_notifications")]
public class NotificationRow : Supabase.Postgrest.Models.BaseModel
{
    [Supabase.Postgrest.Attributes.PrimaryKey("id", false)]
    [Supabase.Postgrest.Attributes.Column("id")]
    public Guid Id { get; set; }

    [Supabase.Postgrest.Attributes.Column("user_id")]
    public string UserId { get; set; } = string.Empty;

    [Supabase.Postgrest.Attributes.Column("title")]
    public string Title { get; set; } = string.Empty;

    [Supabase.Postgrest.Attributes.Column("message")]
    public string Message { get; set; } = string.Empty;

    [Supabase.Postgrest.Attributes.Column("type")]
    public string Type { get; set; } = "info";

    [Supabase.Postgrest.Attributes.Column("is_read")]
    public bool IsRead { get; set; }

    [Supabase.Postgrest.Attributes.Column("action_url")]
    public string? ActionUrl { get; set; }

    [Supabase.Postgrest.Attributes.Column("created_at")]
    public DateTime CreatedAt { get; set; }

    [Supabase.Postgrest.Attributes.Column("expires_at")]
    public DateTime? ExpiresAt { get; set; }

    // Prevent immutable fields from being serialized on update
    public bool ShouldSerializeId() => false;
    public bool ShouldSerializeUserId() => false;
    public bool ShouldSerializeCreatedAt() => false;
}
