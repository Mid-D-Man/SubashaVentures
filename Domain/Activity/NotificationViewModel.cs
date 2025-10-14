
// ===== Domain/Activity/NotificationViewModel.cs =====
namespace SubashaVentures.Domain.Activity;

public class NotificationViewModel
{
    public string Id { get; set; } = string.Empty;
    public string UserId { get; set; } = string.Empty;
    public NotificationType Type { get; set; }
    public string Title { get; set; } = string.Empty;
    public string Message { get; set; } = string.Empty;
    public string? ActionUrl { get; set; }
    public string? ImageUrl { get; set; }
    public bool IsRead { get; set; }
    public DateTime CreatedAt { get; set; }
    public DateTime? ReadAt { get; set; }
    
    public string TimeAgo => GetTimeAgo(CreatedAt);
    
    private string GetTimeAgo(DateTime date)
    {
        var span = DateTime.UtcNow - date;
        if (span.TotalDays > 1) return $"{(int)span.TotalDays}d ago";
        if (span.TotalHours > 1) return $"{(int)span.TotalHours}h ago";
        if (span.TotalMinutes > 1) return $"{(int)span.TotalMinutes}m ago";
        return "Now";
    }
}

public enum NotificationType
{
    OrderConfirmed,
    OrderShipped,
    OrderDelivered,
    OrderCancelled,
    PaymentReceived,
    PaymentFailed,
    PromoCode,
    NewProduct,
    PriceDropAlert,
    BackInStock,
    ReviewResponse,
    AccountUpdate
}
