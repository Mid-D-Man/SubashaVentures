// Services/Products/IReviewService.cs
using SubashaVentures.Models.Firebase;

namespace SubashaVentures.Services.Products;

/// <summary>
/// Service for managing product reviews
/// </summary>
public interface IReviewService
{
    /// <summary>
    /// Get all reviews for a product
    /// </summary>
    Task<List<ReviewModel>> GetProductReviewsAsync(string productId);
    
    /// <summary>
    /// Get a single review by ID
    /// </summary>
    Task<ReviewModel?> GetReviewByIdAsync(string reviewId);
    
    /// <summary>
    /// Get reviews by user
    /// </summary>
    Task<List<ReviewModel>> GetUserReviewsAsync(string userId);
    
    /// <summary>
    /// Create a new review
    /// </summary>
    Task<string> CreateReviewAsync(CreateReviewRequest request);
    
    /// <summary>
    /// Update an existing review
    /// </summary>
    Task<bool> UpdateReviewAsync(string reviewId, UpdateReviewRequest request);
    
    /// <summary>
    /// Delete a review
    /// </summary>
    Task<bool> DeleteReviewAsync(string reviewId);
    
    /// <summary>
    /// Check if user has reviewed a product
    /// </summary>
    Task<bool> HasUserReviewedProductAsync(string userId, string productId);
    
    /// <summary>
    /// Mark review as helpful
    /// </summary>
    Task<bool> MarkReviewHelpfulAsync(string reviewId);
    
    /// <summary>
    /// Get review statistics for a product
    /// </summary>
    Task<ReviewStatistics> GetReviewStatisticsAsync(string productId);
}

/// <summary>
/// Request to create a new review
/// </summary>
public class CreateReviewRequest
{
    public string ProductId { get; set; } = string.Empty;
    public string UserId { get; set; } = string.Empty;
    public string UserName { get; set; } = string.Empty;
    public string? UserAvatar { get; set; }
    public int Rating { get; set; } // 1-5
    public string? Title { get; set; }
    public string Comment { get; set; } = string.Empty;
    public List<string>? ImageUrls { get; set; }
    public bool IsVerifiedPurchase { get; set; }
}

/// <summary>
/// Request to update an existing review
/// </summary>
public class UpdateReviewRequest
{
    public int? Rating { get; set; }
    public string? Title { get; set; }
    public string? Comment { get; set; }
    public List<string>? ImageUrls { get; set; }
}

/// <summary>
/// Review statistics for a product
/// </summary>
public class ReviewStatistics
{
    public string ProductId { get; set; } = string.Empty;
    public int TotalReviews { get; set; }
    public float AverageRating { get; set; }
    public Dictionary<int, int> RatingDistribution { get; set; } = new(); // Star -> Count
    public int VerifiedPurchaseCount { get; set; }
    public int WithImagesCount { get; set; }
    
    public string FormattedRating => $"{AverageRating:F1}/5";
    public int FiveStarCount => RatingDistribution.GetValueOrDefault(5, 0);
    public int FourStarCount => RatingDistribution.GetValueOrDefault(4, 0);
    public int ThreeStarCount => RatingDistribution.GetValueOrDefault(3, 0);
    public int TwoStarCount => RatingDistribution.GetValueOrDefault(2, 0);
    public int OneStarCount => RatingDistribution.GetValueOrDefault(1, 0);
    
    public int FiveStarPercentage => TotalReviews > 0 ? (FiveStarCount * 100 / TotalReviews) : 0;
    public int FourStarPercentage => TotalReviews > 0 ? (FourStarCount * 100 / TotalReviews) : 0;
    public int ThreeStarPercentage => TotalReviews > 0 ? (ThreeStarCount * 100 / TotalReviews) : 0;
    public int TwoStarPercentage => TotalReviews > 0 ? (TwoStarCount * 100 / TotalReviews) : 0;
    public int OneStarPercentage => TotalReviews > 0 ? (OneStarCount * 100 / TotalReviews) : 0;
}
