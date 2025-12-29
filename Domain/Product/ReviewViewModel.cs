namespace SubashaVentures.Domain.Product;

using SubashaVentures.Models.Firebase;

public class ReviewViewModel
{
    public string Id { get; set; } = string.Empty;
    public string ProductId { get; set; } = string.Empty;
    public string UserId { get; set; } = string.Empty;
    public string UserName { get; set; } = string.Empty;
    public string? UserAvatar { get; set; }
    public int Rating { get; set; }
    public string? Title { get; set; }
    public string Comment { get; set; } = string.Empty;
    public List<string> Images { get; set; } = new();
    public bool IsVerifiedPurchase { get; set; }
    public int HelpfulCount { get; set; }
    public DateTime CreatedAt { get; set; }
    public DateTime? UpdatedAt { get; set; }
    
    public string DisplayRating => $"{Rating}/5";
    public string TimeAgo => GetTimeAgo(CreatedAt);
    
    private string GetTimeAgo(DateTime date)
    {
        var span = DateTime.UtcNow - date;
        if (span.TotalDays > 365) return $"{(int)(span.TotalDays / 365)} year(s) ago";
        if (span.TotalDays > 30) return $"{(int)(span.TotalDays / 30)} month(s) ago";
        if (span.TotalDays > 1) return $"{(int)span.TotalDays} day(s) ago";
        if (span.TotalHours > 1) return $"{(int)span.TotalHours} hour(s) ago";
        if (span.TotalMinutes > 1) return $"{(int)span.TotalMinutes} minute(s) ago";
        return "Just now";
    }
    
    // ==================== CONVERSION METHODS ====================
    
    /// <summary>
    /// Convert from Firebase ReviewModel to ReviewViewModel
    /// </summary>
    public static ReviewViewModel FromCloudModel(ReviewModel model)
    {
        if (model == null)
            throw new ArgumentNullException(nameof(model));
            
        return new ReviewViewModel
        {
            Id = model.Id,
            ProductId = model.ProductId,
            UserId = model.UserId,
            UserName = model.UserName,
            UserAvatar = model.UserAvatar,
            Rating = model.Rating,
            Title = model.Title,
            Comment = model.Comment,
            Images = model.Images ?? new List<string>(),
            IsVerifiedPurchase = model.IsVerifiedPurchase,
            HelpfulCount = model.HelpfulCount,
            CreatedAt = model.CreatedAt,
            UpdatedAt = model.UpdatedAt
        };
    }
    
    /// <summary>
    /// Convert from ReviewViewModel to Firebase ReviewModel
    /// </summary>
    public ReviewModel ToCloudModel()
    {
        return new ReviewModel
        {
            Id = this.Id,
            ProductId = this.ProductId,
            UserId = this.UserId,
            UserName = this.UserName,
            UserAvatar = this.UserAvatar,
            Rating = this.Rating,
            Title = this.Title,
            Comment = this.Comment,
            Images = this.Images ?? new List<string>(),
            IsVerifiedPurchase = this.IsVerifiedPurchase,
            HelpfulCount = this.HelpfulCount,
            IsApproved = true, // Default to approved when converting from view model
            CreatedAt = this.CreatedAt,
            UpdatedAt = this.UpdatedAt
        };
    }
    
    /// <summary>
    /// Convert list of ReviewModels to list of ReviewViewModels
    /// </summary>
    public static List<ReviewViewModel> FromCloudModels(IEnumerable<ReviewModel> models)
    {
        if (models == null)
            return new List<ReviewViewModel>();
            
        return models.Select(FromCloudModel).ToList();
    }
}
