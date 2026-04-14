// Domain/Partner/PartnerStoreReviewViewModel.cs
using SubashaVentures.Models.Firebase;

namespace SubashaVentures.Domain.Partner;

/// <summary>
/// View model for a single partner store review.
/// Converted from PartnerStoreReviewModel (Firestore).
/// </summary>
public class PartnerStoreReviewViewModel
{
    public string Id                { get; set; } = string.Empty;
    public string StoreId           { get; set; } = string.Empty;
    public string PartnerId         { get; set; } = string.Empty;
    public string UserId            { get; set; } = string.Empty;
    public string UserName          { get; set; } = string.Empty;
    public string? UserAvatar       { get; set; }
    public int    Rating            { get; set; }
    public string? Title            { get; set; }
    public string Comment           { get; set; } = string.Empty;
    public bool   IsVerifiedPurchase { get; set; }
    public int    HelpfulCount      { get; set; }
    public bool   IsApproved        { get; set; }
    public DateTime  CreatedAt      { get; set; }
    public DateTime? UpdatedAt      { get; set; }

    // ── Computed ───────────────────────────────────────────────
    public string DisplayRating  => $"{Rating}/5";
    public string CreatedAtDisplay => CreatedAt.ToString("MMM dd, yyyy");

    public string TimeAgo
    {
        get
        {
            var span = DateTime.UtcNow - CreatedAt;
            if (span.TotalDays > 365) return $"{(int)(span.TotalDays / 365)}y ago";
            if (span.TotalDays > 30)  return $"{(int)(span.TotalDays / 30)}mo ago";
            if (span.TotalDays > 1)   return $"{(int)span.TotalDays}d ago";
            if (span.TotalHours > 1)  return $"{(int)span.TotalHours}h ago";
            if (span.TotalMinutes > 1) return $"{(int)span.TotalMinutes}m ago";
            return "Just now";
        }
    }

    // ── Conversion ─────────────────────────────────────────────
    public static PartnerStoreReviewViewModel FromFirebaseModel(PartnerStoreReviewModel model)
    {
        if (model == null) throw new ArgumentNullException(nameof(model));

        return new PartnerStoreReviewViewModel
        {
            Id                 = model.Id,
            StoreId            = model.StoreId,
            PartnerId          = model.PartnerId,
            UserId             = model.UserId,
            UserName           = model.UserName,
            UserAvatar         = model.UserAvatar,
            Rating             = model.Rating,
            Title              = model.Title,
            Comment            = model.Comment,
            IsVerifiedPurchase = model.IsVerifiedPurchase,
            HelpfulCount       = model.HelpfulCount,
            IsApproved         = model.IsApproved,
            CreatedAt          = model.CreatedAt,
            UpdatedAt          = model.UpdatedAt
        };
    }

    public static List<PartnerStoreReviewViewModel> FromFirebaseModels(
        IEnumerable<PartnerStoreReviewModel> models)
    {
        if (models == null) return new List<PartnerStoreReviewViewModel>();
        return models.Select(FromFirebaseModel).ToList();
    }
}

/// <summary>Request to submit a new store review.</summary>
public class SubmitStoreReviewRequest
{
    public string  StoreId   { get; set; } = string.Empty;
    public string  PartnerId { get; set; } = string.Empty;
    public int     Rating    { get; set; }
    public string? Title     { get; set; }
    public string  Comment   { get; set; } = string.Empty;
}

/// <summary>Result of a store review submission.</summary>
public class StoreReviewSubmissionResult
{
    public bool    Success         { get; set; }
    public string? ErrorMessage    { get; set; }
    public bool    AlreadyReviewed { get; set; }

    public static StoreReviewSubmissionResult Ok() =>
        new() { Success = true };

    public static StoreReviewSubmissionResult Fail(string error) =>
        new() { Success = false, ErrorMessage = error };

    public static StoreReviewSubmissionResult Duplicate() =>
        new() { Success = false, AlreadyReviewed = true,
                ErrorMessage = "You have already reviewed this store." };
}
