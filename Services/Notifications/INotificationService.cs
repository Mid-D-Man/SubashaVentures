// Services/Notifications/INotificationService.cs
using SubashaVentures.Models.Supabase;

namespace SubashaVentures.Services.Notifications;

/// <summary>
/// Polling-based notification service for Blazor WASM.
/// Call PollAsync() on navigation events or a timer — no real-time required.
/// </summary>
public interface INotificationService
{
    // ── State ─────────────────────────────────────────────────────────────────

    /// <summary>Number of unread notifications (updated by last poll).</summary>
    int UnreadCount { get; }

    /// <summary>Cached notifications from the last poll.</summary>
    IReadOnlyList<NotificationViewModel> Notifications { get; }

    /// <summary>Raised whenever notifications or unread count changes.</summary>
    event Action? OnChange;

    // ── Polling ───────────────────────────────────────────────────────────────

    /// <summary>
    /// Fetch latest notifications from Supabase.
    /// Call this on page navigation, or from a periodic timer.
    /// </summary>
    Task PollAsync(string userId);

    // ── Actions ───────────────────────────────────────────────────────────────

    /// <summary>Mark a single notification as read.</summary>
    Task MarkReadAsync(Guid notificationId);

    /// <summary>Mark all notifications for the user as read.</summary>
    Task MarkAllReadAsync(string userId);

    /// <summary>Clear the local cache (on sign-out).</summary>
    void Clear();
}

// ── View model returned to UI components ──────────────────────────────────────

public class NotificationViewModel
{
    public Guid Id { get; set; }
    public string Title { get; set; } = string.Empty;
    public string Message { get; set; } = string.Empty;
    public string Type { get; set; } = "info";   // info | order | system | promo
    public bool IsRead { get; set; }
    public string? ActionUrl { get; set; }
    public DateTime CreatedAt { get; set; }

    public string TimeAgo
    {
        get
        {
            var diff = DateTime.UtcNow - CreatedAt;
            if (diff.TotalMinutes < 1)   return "Just now";
            if (diff.TotalHours   < 1)   return $"{(int)diff.TotalMinutes}m ago";
            if (diff.TotalDays    < 1)   return $"{(int)diff.TotalHours}h ago";
            if (diff.TotalDays    < 7)   return $"{(int)diff.TotalDays}d ago";
            return CreatedAt.ToString("MMM d");
        }
    }

    public string IconEmoji => Type switch
    {
        "order"  => "📦",
        "promo"  => "🏷️",
        "system" => "⚙️",
        _        => "🔔"
    };
}
