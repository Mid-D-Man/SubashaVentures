// Services/Partners/PartnerStoreReviewService.cs
using SubashaVentures.Domain.Partner;
using SubashaVentures.Models.Firebase;
using SubashaVentures.Services.Firebase;
using SubashaVentures.Utilities.HelperScripts;
using LogLevel = SubashaVentures.Utilities.Logging.LogLevel;

namespace SubashaVentures.Services.Partners;

public class PartnerStoreReviewService : IPartnerStoreReviewService
{
    private readonly IFirestoreService _firestore;
    private readonly ILogger<PartnerStoreReviewService> _logger;

    // Firestore collection name — same flat pattern as "reviews" for products
    private const string COLLECTION = "store_reviews";

    public PartnerStoreReviewService(
        IFirestoreService firestore,
        ILogger<PartnerStoreReviewService> logger)
    {
        _firestore = firestore;
        _logger    = logger;
    }

    // ── Public reads ───────────────────────────────────────────

    public async Task<List<PartnerStoreReviewViewModel>> GetStoreReviewsAsync(string storeId)
    {
        try
        {
            var models = await _firestore.QueryCollectionAsync<PartnerStoreReviewModel>(
                COLLECTION, "storeId", storeId);

            return models
                .Where(r => r.IsApproved)
                .OrderByDescending(r => r.CreatedAt)
                .Select(PartnerStoreReviewViewModel.FromFirebaseModel)
                .ToList();
        }
        catch (Exception ex)
        {
            await MID_HelperFunctions.LogExceptionAsync(ex, $"GetStoreReviews: {storeId}");
            return new List<PartnerStoreReviewViewModel>();
        }
    }

    public async Task<PartnerStoreRatingSummary> GetStoreRatingSummaryAsync(string storeId)
    {
        try
        {
            var models = await _firestore.QueryCollectionAsync<PartnerStoreReviewModel>(
                COLLECTION, "storeId", storeId);

            return PartnerStoreRatingSummary.FromReviews(storeId, models);
        }
        catch (Exception ex)
        {
            await MID_HelperFunctions.LogExceptionAsync(ex, $"GetStoreRatingSummary: {storeId}");
            return PartnerStoreRatingSummary.Empty(storeId);
        }
    }

    public async Task<bool> HasUserReviewedStoreAsync(string storeId, string userId)
    {
        try
        {
            var models = await _firestore.QueryCollectionAsync<PartnerStoreReviewModel>(
                COLLECTION, "storeId", storeId);

            return models.Any(r => r.UserId == userId);
        }
        catch (Exception ex)
        {
            await MID_HelperFunctions.LogExceptionAsync(
                ex, $"HasUserReviewedStore: {storeId}/{userId}");
            return false;
        }
    }

    // ── User actions ───────────────────────────────────────────

    public async Task<StoreReviewSubmissionResult> SubmitReviewAsync(
        string userId,
        string userName,
        string? userAvatar,
        SubmitStoreReviewRequest request)
    {
        try
        {
            // Validation
            if (request.Rating < 1 || request.Rating > 5)
                return StoreReviewSubmissionResult.Fail("Rating must be between 1 and 5");

            if (string.IsNullOrWhiteSpace(request.Comment) ||
                request.Comment.Trim().Length < 10)
                return StoreReviewSubmissionResult.Fail(
                    "Comment must be at least 10 characters");

            // Duplicate check
            if (await HasUserReviewedStoreAsync(request.StoreId, userId))
                return StoreReviewSubmissionResult.Duplicate();

            var id = Guid.NewGuid().ToString();

            var model = new PartnerStoreReviewModel
            {
                Id                 = id,
                StoreId            = request.StoreId,
                PartnerId          = request.PartnerId,
                UserId             = userId,
                UserName           = userName.Trim(),
                UserAvatar         = userAvatar,
                Rating             = request.Rating,
                Title              = string.IsNullOrWhiteSpace(request.Title)
                    ? null : request.Title.Trim(),
                Comment            = request.Comment.Trim(),
                IsVerifiedPurchase = false, // future: check order history
                HelpfulCount       = 0,
                IsApproved         = false, // requires admin approval
                CreatedAt          = DateTime.UtcNow
            };

            var docId = await _firestore.AddDocumentAsync(COLLECTION, model, id);

            if (string.IsNullOrEmpty(docId))
                return StoreReviewSubmissionResult.Fail("Failed to save review");

            await MID_HelperFunctions.DebugMessageAsync(
                $"Store review submitted: {docId} for store {request.StoreId}",
                LogLevel.Info);

            return StoreReviewSubmissionResult.Ok();
        }
        catch (Exception ex)
        {
            await MID_HelperFunctions.LogExceptionAsync(ex, "SubmitStoreReview");
            return StoreReviewSubmissionResult.Fail(ex.Message);
        }
    }

    public async Task<bool> MarkHelpfulAsync(string reviewId)
    {
        try
        {
            var model = await _firestore.GetDocumentAsync<PartnerStoreReviewModel>(
                COLLECTION, reviewId);
            if (model == null) return false;

            // Firestore doesn't support direct increment via the C# wrapper easily,
            // so we rebuild the record with incremented count using a with-expression
            var updated = model with { HelpfulCount = model.HelpfulCount + 1 };
            return await _firestore.UpdateDocumentAsync(COLLECTION, reviewId, updated);
        }
        catch (Exception ex)
        {
            await MID_HelperFunctions.LogExceptionAsync(ex, $"MarkHelpful: {reviewId}");
            return false;
        }
    }

    // ── Admin actions ──────────────────────────────────────────

    public async Task<List<PartnerStoreReviewViewModel>> GetPendingReviewsAsync()
    {
        try
        {
            var models = await _firestore.QueryCollectionAsync<PartnerStoreReviewModel>(
                COLLECTION, "isApproved", false);

            return models
                .OrderBy(r => r.CreatedAt)
                .Select(PartnerStoreReviewViewModel.FromFirebaseModel)
                .ToList();
        }
        catch (Exception ex)
        {
            await MID_HelperFunctions.LogExceptionAsync(ex, "GetPendingStoreReviews");
            return new List<PartnerStoreReviewViewModel>();
        }
    }

    public async Task<List<PartnerStoreReviewViewModel>> GetAllReviewsForStoreAsync(string storeId)
    {
        try
        {
            var models = await _firestore.QueryCollectionAsync<PartnerStoreReviewModel>(
                COLLECTION, "storeId", storeId);

            return models
                .OrderByDescending(r => r.CreatedAt)
                .Select(PartnerStoreReviewViewModel.FromFirebaseModel)
                .ToList();
        }
        catch (Exception ex)
        {
            await MID_HelperFunctions.LogExceptionAsync(
                ex, $"GetAllReviewsForStore: {storeId}");
            return new List<PartnerStoreReviewViewModel>();
        }
    }

    public async Task<bool> ApproveReviewAsync(string reviewId, string adminUserId)
    {
        try
        {
            var model = await _firestore.GetDocumentAsync<PartnerStoreReviewModel>(
                COLLECTION, reviewId);
            if (model == null) return false;

            var updated = model with
            {
                IsApproved  = true,
                ApprovedBy  = adminUserId,
                ApprovedAt  = DateTime.UtcNow,
                UpdatedAt   = DateTime.UtcNow
            };

            var success = await _firestore.UpdateDocumentAsync(COLLECTION, reviewId, updated);

            if (success)
                await MID_HelperFunctions.DebugMessageAsync(
                    $"Store review approved: {reviewId}", LogLevel.Info);

            return success;
        }
        catch (Exception ex)
        {
            await MID_HelperFunctions.LogExceptionAsync(ex, $"ApproveStoreReview: {reviewId}");
            return false;
        }
    }

    public async Task<bool> RejectReviewAsync(
        string reviewId, string adminUserId, string reason)
    {
        try
        {
            var model = await _firestore.GetDocumentAsync<PartnerStoreReviewModel>(
                COLLECTION, reviewId);
            if (model == null) return false;

            var updated = model with
            {
                IsApproved      = false,
                RejectionReason = reason,
                ApprovedBy      = adminUserId,
                UpdatedAt       = DateTime.UtcNow
            };

            return await _firestore.UpdateDocumentAsync(COLLECTION, reviewId, updated);
        }
        catch (Exception ex)
        {
            await MID_HelperFunctions.LogExceptionAsync(
                ex, $"RejectStoreReview: {reviewId}");
            return false;
        }
    }

    public async Task<bool> DeleteReviewAsync(string reviewId)
    {
        try
        {
            return await _firestore.DeleteDocumentAsync(COLLECTION, reviewId);
        }
        catch (Exception ex)
        {
            await MID_HelperFunctions.LogExceptionAsync(
                ex, $"DeleteStoreReview: {reviewId}");
            return false;
        }
    }
}
