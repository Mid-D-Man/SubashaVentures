// ===== Domain/Activity/NotificationViewModel.cs =====
namespace SubashaVentures.Domain.Activity;

using SubashaVentures.Models.Supabase;

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
    
    // ==================== CONVERSION METHODS ====================
    
    /// <summary>
    /// Convert from Supabase NotificationModel to NotificationViewModel
    /// </summary>
    public static NotificationViewModel FromCloudModel(NotificationModel model)
    {
        if (model == null)
            throw new ArgumentNullException(nameof(model));
            
        // Parse NotificationType from string
        NotificationType notificationType = NotificationType.AccountUpdate;
        if (Enum.TryParse<NotificationType>(model.Type, true, out var parsedType))
        {
            notificationType = parsedType;
        }
            
        return new NotificationViewModel
        {
            Id = model.Id,
            UserId = model.UserId,
            Type = notificationType,
            Title = model.Title,
            Message = model.Message,
            ActionUrl = model.ActionUrl,
            ImageUrl = model.ImageUrl,
            IsRead = model.IsRead,
            CreatedAt = model.CreatedAt,
            ReadAt = model.ReadAt
        };
    }
    
    /// <summary>
    /// Convert from NotificationViewModel to Supabase NotificationModel
    /// </summary>
    public NotificationModel ToCloudModel()
    {
        return new NotificationModel
        {
            Id = this.Id,
            UserId = this.UserId,
            Type = this.Type.ToString(),
            Title = this.Title,
            Message = this.Message,
            ActionUrl = this.ActionUrl,
            ImageUrl = this.ImageUrl,
            IsRead = this.IsRead,
            ReadAt = this.ReadAt,
            CreatedAt = this.CreatedAt,
            CreatedBy = "system"
        };
    }
    
    /// <summary>
    /// Convert list of NotificationModels to list of NotificationViewModels
    /// </summary>
    public static List<NotificationViewModel> FromCloudModels(IEnumerable<NotificationModel> models)
    {
        if (models == null)
            return new List<NotificationViewModel>();
            
        return models.Select(FromCloudModel).ToList();
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
