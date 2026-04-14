// Services/Partners/IPartnerStoreReviewService.cs
using SubashaVentures.Domain.Partner;
using SubashaVentures.Models.Firebase;

namespace SubashaVentures.Services.Partners;

/// <summary>
/// Service for partner store reviews stored in Firebase Firestore.
/// Mirrors the pattern of IReviewService (product reviews).
/// Firestore collection: store_reviews/{reviewId}
/// </summary>
public interface IPartnerStoreReviewService
{
    // ── Public reads ───────────────────────────────────────────

    /// <summary>Get all approved reviews for a store, newest first.</summary>
    Task<List<PartnerStoreReviewViewModel>> GetStoreReviewsAsync(string storeId);

    /// <summary>Get the aggregate rating summary for a store.</summary>
    Task<PartnerStoreRatingSummary> GetStoreRatingSummaryAsync(string storeId);

    /// <summary>
    /// Check whether the current user has already submitted a review
    /// for this store (enforces one review per user per store).
    /// </summary>
    Task<bool> HasUserReviewedStoreAsync(string storeId, string userId);

    // ── User actions ───────────────────────────────────────────

    /// <summary>
    /// Submit a new store review.
    /// Saved as is_approved = false — requires admin approval before appearing.
    /// </summary>
    Task<StoreReviewSubmissionResult> SubmitReviewAsync(
        string userId,
        string userName,
        string? userAvatar,
        SubmitStoreReviewRequest request);

    /// <summary>Mark a review as helpful (increments helpful_count).</summary>
    Task<bool> MarkHelpfulAsync(string reviewId);

    // ── Admin actions ──────────────────────────────────────────

    /// <summary>Get all reviews pending admin approval.</summary>
    Task<List<PartnerStoreReviewViewModel>> GetPendingReviewsAsync();

    /// <summary>Get all reviews for a store regardless of approval status (admin view).</summary>
    Task<List<PartnerStoreReviewViewModel>> GetAllReviewsForStoreAsync(string storeId);

    /// <summary>Approve a review so it appears publicly.</summary>
    Task<bool> ApproveReviewAsync(string reviewId, string adminUserId);

    /// <summary>Reject a review with a reason.</summary>
    Task<bool> RejectReviewAsync(string reviewId, string adminUserId, string reason);

    /// <summary>Delete a review (admin only).</summary>
    Task<bool> DeleteReviewAsync(string reviewId);
}
