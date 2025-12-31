// Models/Firebase/ReviewModel.cs - UPDATED with proper serialization
using System.Text.Json.Serialization;

namespace SubashaVentures.Models.Firebase;

/// <summary>
/// Review model stored in Firestore 'reviews' collection
/// Uses flat structure: reviews/{reviewId}
/// Query by ProductId to get product reviews
/// </summary>
public record ReviewModel
{
    [JsonPropertyName("id")]
    public string Id { get; init; } = string.Empty;
    
    [JsonPropertyName("productId")]
    public string ProductId { get; init; } = string.Empty;
    
    [JsonPropertyName("userId")]
    public string UserId { get; init; } = string.Empty;
    
    [JsonPropertyName("userName")]
    public string UserName { get; init; } = string.Empty;
    
    [JsonPropertyName("userAvatar")]
    public string? UserAvatar { get; init; }
    
    [JsonPropertyName("rating")]
    public int Rating { get; init; }
    
    [JsonPropertyName("title")]
    public string? Title { get; init; }
    
    [JsonPropertyName("comment")]
    public string Comment { get; init; } = string.Empty;
    
    [JsonPropertyName("images")]
    public List<string> Images { get; init; } = new();
    
    [JsonPropertyName("isVerifiedPurchase")]
    public bool IsVerifiedPurchase { get; init; }
    
    [JsonPropertyName("helpfulCount")]
    public int HelpfulCount { get; init; }
    
    [JsonPropertyName("isApproved")]
    public bool IsApproved { get; init; }
    
    [JsonPropertyName("createdAt")]
    public DateTime CreatedAt { get; init; }
    
    [JsonPropertyName("updatedAt")]
    public DateTime? UpdatedAt { get; init; }
    
    // Admin fields
    [JsonPropertyName("approvedBy")]
    public string? ApprovedBy { get; init; }
    
    [JsonPropertyName("approvedAt")]
    public DateTime? ApprovedAt { get; init; }
    
    [JsonPropertyName("rejectionReason")]
    public string? RejectionReason { get; init; }
    
    // Helper properties (not stored in Firestore)
    [JsonIgnore]
    public string DisplayRating => $"{Rating}/5";
    
    [JsonIgnore]
    public string TimeAgo => GetTimeAgo(CreatedAt);
    
    [JsonIgnore]
    public string ApprovalStatus => IsApproved ? "Approved" : "Pending";
    
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
}

/// <summary>
/// Admin review list item (lightweight for lists)
/// </summary>
public record ReviewAdminDto
{
    public string Id { get; init; } = string.Empty;
    public string ProductId { get; init; } = string.Empty;
    public string ProductName { get; init; } = string.Empty; // Fetched from product
    public string UserName { get; init; } = string.Empty;
    public int Rating { get; init; }
    public string CommentPreview { get; init; } = string.Empty;
    public bool IsApproved { get; init; }
    public DateTime CreatedAt { get; init; }
    public string TimeAgo { get; init; } = string.Empty;
    
    public static ReviewAdminDto FromReviewModel(ReviewModel review, string productName)
    {
        var commentPreview = review.Comment.Length > 100 
            ? review.Comment.Substring(0, 97) + "..." 
            : review.Comment;
            
        return new ReviewAdminDto
        {
            Id = review.Id,
            ProductId = review.ProductId,
            ProductName = productName,
            UserName = review.UserName,
            Rating = review.Rating,
            CommentPreview = commentPreview,
            IsApproved = review.IsApproved,
            CreatedAt = review.CreatedAt,
            TimeAgo = review.TimeAgo
        };
    }
}
