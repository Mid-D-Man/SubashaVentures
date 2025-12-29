// Services/Products/ReviewService.cs - COMPLETE FILE
using SubashaVentures.Models.Firebase;
using SubashaVentures.Services.Firebase;
using SubashaVentures.Utilities.HelperScripts;
using LogLevel = SubashaVentures.Utilities.Logging.LogLevel;

namespace SubashaVentures.Services.Products;

public class ReviewService : IReviewService
{
    private readonly IFirestoreService _firestore;
    private readonly ILogger<ReviewService> _logger;
    private const string COLLECTION = "reviews";

    public ReviewService(
        IFirestoreService firestore,
        ILogger<ReviewService> logger)
    {
        _firestore = firestore;
        _logger = logger;
    }

    public async Task<List<ReviewModel>> GetProductReviewsAsync(string productId)
    {
        try
        {
            if (string.IsNullOrWhiteSpace(productId))
            {
                _logger.LogWarning("GetProductReviews called with empty productId");
                return new List<ReviewModel>();
            }

            await MID_HelperFunctions.DebugMessageAsync(
                $"Fetching reviews for product: {productId}",
                LogLevel.Info
            );

            var reviews = await _firestore.QueryCollectionAsync<ReviewModel>(
                COLLECTION,
                "ProductId",
                productId
            );

            if (reviews == null || !reviews.Any())
            {
                await MID_HelperFunctions.DebugMessageAsync(
                    $"No reviews found for product: {productId}",
                    LogLevel.Info
                );
                return new List<ReviewModel>();
            }

            var approvedReviews = reviews
                .Where(r => r.IsApproved)
                .OrderByDescending(r => r.CreatedAt)
                .ToList();

            await MID_HelperFunctions.DebugMessageAsync(
                $"✓ Retrieved {approvedReviews.Count} approved reviews",
                LogLevel.Info
            );

            return approvedReviews;
        }
        catch (Exception ex)
        {
            await MID_HelperFunctions.LogExceptionAsync(ex, $"Getting reviews for product: {productId}");
            _logger.LogError(ex, "Failed to get reviews for product: {ProductId}", productId);
            return new List<ReviewModel>();
        }
    }

    public async Task<ReviewModel?> GetReviewByIdAsync(string reviewId)
    {
        try
        {
            if (string.IsNullOrWhiteSpace(reviewId))
            {
                _logger.LogWarning("GetReviewById called with empty reviewId");
                return null;
            }

            var review = await _firestore.GetDocumentAsync<ReviewModel>(COLLECTION, reviewId);

            if (review == null)
            {
                await MID_HelperFunctions.DebugMessageAsync(
                    $"Review not found: {reviewId}",
                    LogLevel.Warning
                );
            }

            return review;
        }
        catch (Exception ex)
        {
            await MID_HelperFunctions.LogExceptionAsync(ex, $"Getting review: {reviewId}");
            _logger.LogError(ex, "Failed to get review: {ReviewId}", reviewId);
            return null;
        }
    }

    public async Task<List<ReviewModel>> GetUserReviewsAsync(string userId)
    {
        try
        {
            if (string.IsNullOrWhiteSpace(userId))
            {
                _logger.LogWarning("GetUserReviews called with empty userId");
                return new List<ReviewModel>();
            }

            await MID_HelperFunctions.DebugMessageAsync(
                $"Fetching reviews for user: {userId}",
                LogLevel.Info
            );

            var reviews = await _firestore.QueryCollectionAsync<ReviewModel>(
                COLLECTION,
                "UserId",
                userId
            );

            var userReviews = reviews?
                .OrderByDescending(r => r.CreatedAt)
                .ToList() ?? new List<ReviewModel>();

            await MID_HelperFunctions.DebugMessageAsync(
                $"✓ Retrieved {userReviews.Count} reviews for user",
                LogLevel.Info
            );

            return userReviews;
        }
        catch (Exception ex)
        {
            await MID_HelperFunctions.LogExceptionAsync(ex, $"Getting reviews for user: {userId}");
            _logger.LogError(ex, "Failed to get reviews for user: {UserId}", userId);
            return new List<ReviewModel>();
        }
    }

    public async Task<string> CreateReviewAsync(CreateReviewRequest request)
    {
        try
        {
            if (request == null)
                throw new ArgumentNullException(nameof(request));

            if (string.IsNullOrWhiteSpace(request.ProductId) || string.IsNullOrWhiteSpace(request.UserId))
                throw new ArgumentException("ProductId and UserId are required");

            if (request.Rating < 1 || request.Rating > 5)
                throw new ArgumentException("Rating must be between 1 and 5");

            await MID_HelperFunctions.DebugMessageAsync(
                $"Creating review: Product={request.ProductId}, User={request.UserId}, Rating={request.Rating}",
                LogLevel.Info
            );

            var existingReview = await HasUserReviewedProductAsync(request.UserId, request.ProductId);
            if (existingReview)
            {
                await MID_HelperFunctions.DebugMessageAsync(
                    "User has already reviewed this product",
                    LogLevel.Warning
                );
                return string.Empty;
            }

            var reviewModel = new ReviewModel
            {
                Id = Guid.NewGuid().ToString(),
                ProductId = request.ProductId,
                UserId = request.UserId,
                UserName = request.UserName,
                UserAvatar = request.UserAvatar,
                Rating = request.Rating,
                Title = request.Title?.Trim(),
                Comment = request.Comment.Trim(),
                Images = request.ImageUrls ?? new List<string>(),
                IsVerifiedPurchase = request.IsVerifiedPurchase,
                HelpfulCount = 0,
                IsApproved = false,
                CreatedAt = DateTime.UtcNow,
                UpdatedAt = null
            };

            var id = await _firestore.AddDocumentAsync(COLLECTION, reviewModel, reviewModel.Id);

            if (!string.IsNullOrEmpty(id))
            {
                await MID_HelperFunctions.DebugMessageAsync(
                    $"✓ Review created: {id} (pending approval)",
                    LogLevel.Info
                );
            }

            return id ?? string.Empty;
        }
        catch (Exception ex)
        {
            await MID_HelperFunctions.LogExceptionAsync(ex, "Creating review");
            _logger.LogError(ex, "Failed to create review");
            return string.Empty;
        }
    }

    public async Task<bool> UpdateReviewAsync(string reviewId, UpdateReviewRequest request)
    {
        try
        {
            if (string.IsNullOrWhiteSpace(reviewId))
                throw new ArgumentException("ReviewId is required");

            if (request == null)
                throw new ArgumentNullException(nameof(request));

            var existing = await _firestore.GetDocumentAsync<ReviewModel>(COLLECTION, reviewId);

            if (existing == null)
            {
                await MID_HelperFunctions.DebugMessageAsync(
                    $"Review not found: {reviewId}",
                    LogLevel.Warning
                );
                return false;
            }

            var updated = existing with
            {
                Rating = request.Rating ?? existing.Rating,
                Title = request.Title ?? existing.Title,
                Comment = request.Comment ?? existing.Comment,
                Images = request.ImageUrls ?? existing.Images,
                UpdatedAt = DateTime.UtcNow
            };

            if (updated.Rating < 1 || updated.Rating > 5)
            {
                throw new ArgumentException("Rating must be between 1 and 5");
            }

            var success = await _firestore.UpdateDocumentAsync(COLLECTION, reviewId, updated);

            if (success)
            {
                await MID_HelperFunctions.DebugMessageAsync(
                    $"✓ Review updated: {reviewId}",
                    LogLevel.Info
                );
            }

            return success;
        }
        catch (Exception ex)
        {
            await MID_HelperFunctions.LogExceptionAsync(ex, $"Updating review: {reviewId}");
            _logger.LogError(ex, "Failed to update review: {ReviewId}", reviewId);
            return false;
        }
    }

    public async Task<bool> DeleteReviewAsync(string reviewId)
    {
        try
        {
            if (string.IsNullOrWhiteSpace(reviewId))
                throw new ArgumentException("ReviewId is required");

            await MID_HelperFunctions.DebugMessageAsync(
                $"Deleting review: {reviewId}",
                LogLevel.Warning
            );

            var success = await _firestore.DeleteDocumentAsync(COLLECTION, reviewId);

            if (success)
            {
                await MID_HelperFunctions.DebugMessageAsync(
                    $"✓ Review deleted: {reviewId}",
                    LogLevel.Info
                );
            }

            return success;
        }
        catch (Exception ex)
        {
            await MID_HelperFunctions.LogExceptionAsync(ex, $"Deleting review: {reviewId}");
            _logger.LogError(ex, "Failed to delete review: {ReviewId}", reviewId);
            return false;
        }
    }

    public async Task<bool> HasUserReviewedProductAsync(string userId, string productId)
    {
        try
        {
            if (string.IsNullOrWhiteSpace(userId) || string.IsNullOrWhiteSpace(productId))
                return false;

            var reviews = await _firestore.QueryCollectionAsync<ReviewModel>(
                COLLECTION,
                "UserId",
                userId
            );

            return reviews?.Any(r => r.ProductId == productId) ?? false;
        }
        catch (Exception ex)
        {
            await MID_HelperFunctions.LogExceptionAsync(ex, "Checking if user reviewed product");
            return false;
        }
    }

    public async Task<bool> MarkReviewHelpfulAsync(string reviewId)
    {
        try
        {
            if (string.IsNullOrWhiteSpace(reviewId))
                return false;

            var review = await _firestore.GetDocumentAsync<ReviewModel>(COLLECTION, reviewId);

            if (review == null)
                return false;

            var updated = review with
            {
                HelpfulCount = review.HelpfulCount + 1,
                UpdatedAt = DateTime.UtcNow
            };

            return await _firestore.UpdateDocumentAsync(COLLECTION, reviewId, updated);
        }
        catch (Exception ex)
        {
            await MID_HelperFunctions.LogExceptionAsync(ex, $"Marking review helpful: {reviewId}");
            return false;
        }
    }

    public async Task<ReviewStatistics> GetReviewStatisticsAsync(string productId)
    {
        try
        {
            if (string.IsNullOrWhiteSpace(productId))
            {
                _logger.LogWarning("GetReviewStatistics called with empty productId");
                return new ReviewStatistics { ProductId = productId };
            }

            var reviews = await GetProductReviewsAsync(productId);

            if (!reviews.Any())
            {
                return new ReviewStatistics { ProductId = productId };
            }

            var stats = new ReviewStatistics
            {
                ProductId = productId,
                TotalReviews = reviews.Count,
                AverageRating = (float)reviews.Average(r => r.Rating),
                RatingDistribution = reviews
                    .GroupBy(r => r.Rating)
                    .ToDictionary(g => g.Key, g => g.Count()),
                VerifiedPurchaseCount = reviews.Count(r => r.IsVerifiedPurchase),
                WithImagesCount = reviews.Count(r => r.Images.Any())
            };

            await MID_HelperFunctions.DebugMessageAsync(
                $"✓ Review statistics calculated: {stats.TotalReviews} reviews, {stats.AverageRating:F1} avg rating",
                LogLevel.Info
            );

            return stats;
        }
        catch (Exception ex)
        {
            await MID_HelperFunctions.LogExceptionAsync(ex, $"Getting review statistics: {productId}");
            _logger.LogError(ex, "Failed to get review statistics for product: {ProductId}", productId);
            return new ReviewStatistics { ProductId = productId };
        }
    }
}
