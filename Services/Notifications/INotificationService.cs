// Services/Notifications/INotificationService.cs
namespace SubashaVentures.Services.Notifications;

/// <summary>
/// Lightweight notification service — tracks unread message count only.
/// Refresh is triggered on navigation or explicit call, never on a timer.
/// </summary>
public interface INotificationService
{
    /// <summary>Total unread message count from Firebase conversations.</summary>
    int UnreadMessageCount { get; }

    /// <summary>Raised whenever UnreadMessageCount changes.</summary>
    event Action? OnChange;

    /// <summary>
    /// Refresh from Firebase. Respects a 30-second cooldown so rapid
    /// navigation events don't hammer Firebase.
    /// </summary>
    Task RefreshAsync(string userId);

    /// <summary>Clear local state on sign-out.</summary>
    void Clear();
}
