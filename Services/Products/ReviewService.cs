// Services/Products/ReviewService.cs - COMPLETE with admin methods
using SubashaVentures.Models.Firebase;
using SubashaVentures.Services.Firebase;
using SubashaVentures.Services.Products;
using SubashaVentures.Utilities.HelperScripts;
using LogLevel = SubashaVentures.Utilities.Logging.LogLevel;

namespace SubashaVentures.Services.Products;

public class ReviewService : IReviewService
{
    private readonly IFirestoreService _firestore;
    private readonly IProductService _productService;
    private readonly ILogger<ReviewService> _logger;
    private const string COLLECTION = "reviews";

    public ReviewService(
        IFirestoreService firestore,
        IProductService productService,
        ILogger<ReviewService> logger)
    {
        _firestore = firestore;
        _productService = productService;
        _logger = logger;
    }

    // ==================== USER METHODS ====================

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
                $"Fetching approved reviews for product: {productId}",
                LogLevel.Info
            );

            var reviews = await _firestore.QueryCollectionAsync<ReviewModel>(
                COLLECTION,
                productId, // Use camelCase to match Firestore field
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

            // Only return approved reviews for public view
            var approvedReviews = reviews
                .Where(r => r.IsApproved)
                .OrderByDescending(r => r.CreatedAt)
                .ToList();

            await MID_HelperFunctions.DebugMessageAsync(
                $"✓ Retrieved {approvedReviews.Count} approved reviews out of {reviews.Count} total",
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
                "userId", // Use camelCase to match Firestore field
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

            if (string.IsNullOrWhiteSpace(request.Comment) || request.Comment.Length < 10)
                throw new ArgumentException("Comment must be at least 10 characters");

            await MID_HelperFunctions.DebugMessageAsync(
                $"Creating review: Product={request.ProductId}, User={request.UserId}, Rating={request.Rating}",
                LogLevel.Info
            );

            // Check if user already reviewed this product
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
                IsApproved = false, // Requires admin approval
                CreatedAt = DateTime.UtcNow,
                UpdatedAt = null,
                ApprovedBy = null,
                ApprovedAt = null,
                RejectionReason = null
            };

            var id = await _firestore.AddDocumentAsync(COLLECTION, reviewModel, reviewModel.Id);

            if (!string.IsNullOrEmpty(id))
            {
                await MID_HelperFunctions.DebugMessageAsync(
                    $"✓ Review created: {id} (pending approval)",
                    LogLevel.Info
                );
            }
            else
            {
                await MID_HelperFunctions.DebugMessageAsync(
                    "❌ Failed to create review - Firestore returned empty ID",
                    LogLevel.Error
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
                "userId",
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

    // ==================== ADMIN METHODS ====================

    public async Task<List<ReviewAdminDto>> GetAllReviewsAdminAsync()
    {
        try
        {
            await MID_HelperFunctions.DebugMessageAsync(
                "Fetching ALL reviews for admin",
                LogLevel.Info
            );

            var reviews = await _firestore.GetCollectionAsync<ReviewModel>(COLLECTION);

            if (reviews == null || !reviews.Any())
            {
                await MID_HelperFunctions.DebugMessageAsync(
                    "No reviews found in system",
                    LogLevel.Info
                );
                return new List<ReviewAdminDto>();
            }

            // Get unique product IDs
            var productIds = reviews.Select(r => r.ProductId).Distinct().ToList();
            
            // Fetch product names (batch request)
            var productNames = new Dictionary<string, string>();
            foreach (var productId in productIds)
            {
                if (int.TryParse(productId, out var id))
                {
                    var product = await _productService.GetProductByIdAsync(id);
                    if (product != null)
                    {
                        productNames[productId] = product.Name;
                    }
                    else
                    {
                        productNames[productId] = $"Product #{productId}";
                    }
                }
                else
                {
                    productNames[productId] = $"Product #{productId}";
                }
            }

            var adminDtos = reviews
                .OrderByDescending(r => r.CreatedAt)
                .Select(r => ReviewAdminDto.FromReviewModel(
                    r, 
                    productNames.GetValueOrDefault(r.ProductId, "Unknown Product")
                ))
                .ToList();

            await MID_HelperFunctions.DebugMessageAsync(
                $"✓ Retrieved {adminDtos.Count} reviews for admin",
                LogLevel.Info
            );

            return adminDtos;
        }
        catch (Exception ex)
        {
            await MID_HelperFunctions.LogExceptionAsync(ex, "Getting all reviews for admin");
            _logger.LogError(ex, "Failed to get all reviews for admin");
            return new List<ReviewAdminDto>();
        }
    }

    public async Task<List<ReviewAdminDto>> GetPendingReviewsAsync()
    {
        try
        {
            await MID_HelperFunctions.DebugMessageAsync(
                "Fetching pending reviews",
                LogLevel.Info
            );

            var reviews = await _firestore.QueryCollectionAsync<ReviewModel>(
                COLLECTION,
                "isApproved",
                false
            );

            if (reviews == null || !reviews.Any())
            {
                await MID_HelperFunctions.DebugMessageAsync(
                    "No pending reviews found",
                    LogLevel.Info
                );
                return new List<ReviewAdminDto>();
            }

            // Get product names
            var productIds = reviews.Select(r => r.ProductId).Distinct().ToList();
            var productNames = new Dictionary<string, string>();
            
            foreach (var productId in productIds)
            {
                if (int.TryParse(productId, out var id))
                {
                    var product = await _productService.GetProductByIdAsync(id);
                    productNames[productId] = product?.Name ?? $"Product #{productId}";
                }
                else
                {
                    productNames[productId] = $"Product #{productId}";
                }
            }

            var pendingDtos = reviews
                .OrderBy(r => r.CreatedAt) // Oldest first for pending
                .Select(r => ReviewAdminDto.FromReviewModel(
                    r, 
                    productNames.GetValueOrDefault(r.ProductId, "Unknown Product")
                ))
                .ToList();

            await MID_HelperFunctions.DebugMessageAsync(
                $"✓ Retrieved {pendingDtos.Count} pending reviews",
                LogLevel.Info
            );

            return pendingDtos;
        }
        catch (Exception ex)
        {
            await MID_HelperFunctions.LogExceptionAsync(ex, "Getting pending reviews");
            _logger.LogError(ex, "Failed to get pending reviews");
            return new List<ReviewAdminDto>();
        }
    }

    public async Task<List<ReviewAdminDto>> GetApprovedReviewsAsync()
    {
        try
        {
            await MID_HelperFunctions.DebugMessageAsync(
                "Fetching approved reviews",
                LogLevel.Info
            );

            var reviews = await _firestore.QueryCollectionAsync<ReviewModel>(
                COLLECTION,
                "isApproved",
                true
            );

            if (reviews == null || !reviews.Any())
            {
                await MID_HelperFunctions.DebugMessageAsync(
                    "No approved reviews found",
                    LogLevel.Info
                );
                return new List<ReviewAdminDto>();
            }

            // Get product names
            var productIds = reviews.Select(r => r.ProductId).Distinct().ToList();
            var productNames = new Dictionary<string, string>();
            
            foreach (var productId in productIds)
            {
                if (int.TryParse(productId, out var id))
                {
                    var product = await _productService.GetProductByIdAsync(id);
                    productNames[productId] = product?.Name ?? $"Product #{productId}";
                }
                else
                {
                    productNames[productId] = $"Product #{productId}";
                }
            }

            var approvedDtos = reviews
                .OrderByDescending(r => r.ApprovedAt ?? r.CreatedAt)
                .Select(r => ReviewAdminDto.FromReviewModel(
                    r, 
                    productNames.GetValueOrDefault(r.ProductId, "Unknown Product")
                ))
                .ToList();

            await MID_HelperFunctions.DebugMessageAsync(
                $"✓ Retrieved {approvedDtos.Count} approved reviews",
                LogLevel.Info
            );

            return approvedDtos;
        }
        catch (Exception ex)
        {
            await MID_HelperFunctions.LogExceptionAsync(ex, "Getting approved reviews");
            _logger.LogError(ex, "Failed to get approved reviews");
            return new List<ReviewAdminDto>();
        }
    }

    public async Task<List<ReviewAdminDto>> GetReviewsByStatusAsync(bool isApproved)
    {
        return isApproved 
            ? await GetApprovedReviewsAsync() 
            : await GetPendingReviewsAsync();
    }

    public async Task<bool> ApproveReviewAsync(string reviewId, string approvedBy)
    {
        try
        {
            if (string.IsNullOrWhiteSpace(reviewId))
                throw new ArgumentException("ReviewId is required");

            if (string.IsNullOrWhiteSpace(approvedBy))
                throw new ArgumentException("ApprovedBy is required");

            await MID_HelperFunctions.DebugMessageAsync(
                $"Approving review: {reviewId} by {approvedBy}",
                LogLevel.Info
            );

            var review = await _firestore.GetDocumentAsync<ReviewModel>(COLLECTION, reviewId);

            if (review == null)
            {
                await MID_HelperFunctions.DebugMessageAsync(
                    $"Review not found: {reviewId}",
                    LogLevel.Warning
                );
                return false;
            }

            var updated = review with
            {
                IsApproved = true,
                ApprovedBy = approvedBy,
                ApprovedAt = DateTime.UtcNow,
                UpdatedAt = DateTime.UtcNow,
                RejectionReason = null // Clear any previous rejection
            };

            var success = await _firestore.UpdateDocumentAsync(COLLECTION, reviewId, updated);

            if (success)
            {
                await MID_HelperFunctions.DebugMessageAsync(
                    $"✓ Review approved: {reviewId}",
                    LogLevel.Info
                );
            }
            else
            {
                await MID_HelperFunctions.DebugMessageAsync(
                    $"❌ Failed to approve review: {reviewId}",
                    LogLevel.Error
                );
            }

            return success;
        }
        catch (Exception ex)
        {
            await MID_HelperFunctions.LogExceptionAsync(ex, $"Approving review: {reviewId}");
            _logger.LogError(ex, "Failed to approve review: {ReviewId}", reviewId);
            return false;
        }
    }

    public async Task<bool> RejectReviewAsync(string reviewId, string rejectionReason)
    {
        try
        {
            if (string.IsNullOrWhiteSpace(reviewId))
                throw new ArgumentException("ReviewId is required");

            if (string.IsNullOrWhiteSpace(rejectionReason))
                throw new ArgumentException("Rejection reason is required");

            await MID_HelperFunctions.DebugMessageAsync(
                $"Rejecting review: {reviewId} - Reason: {rejectionReason}",
                LogLevel.Warning
            );

            // Delete the review (or you could mark it as rejected)
            var success = await _firestore.DeleteDocumentAsync(COLLECTION, reviewId);

            if (success)
            {
                await MID_HelperFunctions.DebugMessageAsync(
                    $"✓ Review rejected and deleted: {reviewId}",
                    LogLevel.Info
                );
            }
            else
            {
                await MID_HelperFunctions.DebugMessageAsync(
                    $"❌ Failed to reject review: {reviewId}",
                    LogLevel.Error
                );
            }

            return success;
        }
        catch (Exception ex)
        {
            await MID_HelperFunctions.LogExceptionAsync(ex, $"Rejecting review: {reviewId}");
            _logger.LogError(ex, "Failed to reject review: {ReviewId}", reviewId);
            return false;
        }
    }

    public async Task<ReviewStatusCounts> GetReviewStatusCountsAsync()
    {
        try
        {
            await MID_HelperFunctions.DebugMessageAsync(
                "Fetching review status counts",
                LogLevel.Info
            );

            var allReviews = await _firestore.GetCollectionAsync<ReviewModel>(COLLECTION);

            if (allReviews == null || !allReviews.Any())
            {
                return new ReviewStatusCounts();
            }

            var counts = new ReviewStatusCounts
            {
                TotalReviews = allReviews.Count,
                PendingReviews = allReviews.Count(r => !r.IsApproved),
                ApprovedReviews = allReviews.Count(r => r.IsApproved),
                RejectedReviews = 0 // We delete rejected reviews, so always 0
            };

            await MID_HelperFunctions.DebugMessageAsync(
                $"✓ Review counts: Total={counts.TotalReviews}, Pending={counts.PendingReviews}, Approved={counts.ApprovedReviews}",
                LogLevel.Info
            );

            return counts;
        }
        catch (Exception ex)
        {
            await MID_HelperFunctions.LogExceptionAsync(ex, "Getting review status counts");
            _logger.LogError(ex, "Failed to get review status counts");
            return new ReviewStatusCounts();
        }
    }

    public async Task<List<ReviewModel>> GetProductReviewsAdminAsync(string productId)
    {
        try
        {
            if (string.IsNullOrWhiteSpace(productId))
            {
                _logger.LogWarning("GetProductReviewsAdmin called with empty productId");
                return new List<ReviewModel>();
            }

            await MID_HelperFunctions.DebugMessageAsync(
                $"Fetching ALL reviews (including pending) for product: {productId}",
                LogLevel.Info
            );

            var reviews = await _firestore.QueryCollectionAsync<ReviewModel>(
                COLLECTION,
                "productId",
                productId
            );

            var allReviews = reviews?
                .OrderByDescending(r => r.CreatedAt)
                .ToList() ?? new List<ReviewModel>();

            await MID_HelperFunctions.DebugMessageAsync(
                $"✓ Retrieved {allReviews.Count} total reviews for product (admin view)",
                LogLevel.Info
            );

            return allReviews;
        }
        catch (Exception ex)
        {
            await MID_HelperFunctions.LogExceptionAsync(ex, $"Getting admin reviews for product: {productId}");
            _logger.LogError(ex, "Failed to get admin reviews for product: {ProductId}", productId);
            return new List<ReviewModel>();
        }
    }
}
