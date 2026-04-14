// Models/Firebase/PartnerStoreReviewModel.cs
using System.Text.Json.Serialization;

namespace SubashaVentures.Models.Firebase;

/// <summary>
/// Partner store review stored in Firestore.
/// Collection: store_reviews/{reviewId}
/// Query by storeId to get all reviews for a store.
/// Mirrors the same pattern as ReviewModel (product reviews).
/// </summary>
public record PartnerStoreReviewModel
{
    [JsonPropertyName("id")]
    public string Id { get; init; } = string.Empty;

    [JsonPropertyName("storeId")]
    public string StoreId { get; init; } = string.Empty;

    [JsonPropertyName("partnerId")]
    public string PartnerId { get; init; } = string.Empty;

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

    // Admin fields — same pattern as ReviewModel
    [JsonPropertyName("approvedBy")]
    public string? ApprovedBy { get; init; }

    [JsonPropertyName("approvedAt")]
    public DateTime? ApprovedAt { get; init; }

    [JsonPropertyName("rejectionReason")]
    public string? RejectionReason { get; init; }

    // ── Computed helpers (not stored in Firestore) ─────────────
    [JsonIgnore]
    public string DisplayRating => $"{Rating}/5";

    [JsonIgnore]
    public string ApprovalStatus => IsApproved ? "Approved" : "Pending";

    [JsonIgnore]
    public string TimeAgo
    {
        get
        {
            var span = DateTime.UtcNow - CreatedAt;
            if (span.TotalDays > 365) return $"{(int)(span.TotalDays / 365)} year(s) ago";
            if (span.TotalDays > 30)  return $"{(int)(span.TotalDays / 30)} month(s) ago";
            if (span.TotalDays > 1)   return $"{(int)span.TotalDays} day(s) ago";
            if (span.TotalHours > 1)  return $"{(int)span.TotalHours}h ago";
            if (span.TotalMinutes > 1) return $"{(int)span.TotalMinutes}m ago";
            return "Just now";
        }
    }
}

/// <summary>
/// Aggregate rating summary computed from store reviews.
/// Not stored — built at service layer from the review list.
/// </summary>
public class PartnerStoreRatingSummary
{
    public string StoreId { get; set; } = string.Empty;

    public float AverageRating  { get; set; }
    public int   TotalReviews   { get; set; }

    // Key = star count 1–5, value = number of reviews with that rating
    public Dictionary<int, int> Breakdown { get; set; } = new()
    {
        { 1, 0 }, { 2, 0 }, { 3, 0 }, { 4, 0 }, { 5, 0 }
    };

    // ── Computed display ───────────────────────────────────────
    public string DisplayAverage => TotalReviews > 0
        ? AverageRating.ToString("F1")
        : "—";

    public string DisplayTotal => TotalReviews switch
    {
        0 => "No reviews yet",
        1 => "1 review",
        _ => $"{TotalReviews} reviews"
    };

    /// <summary>Percentage of total reviews for a given star count.</summary>
    public int PercentFor(int stars)
    {
        if (TotalReviews == 0) return 0;
        return (int)Math.Round(
            Breakdown.GetValueOrDefault(stars, 0) / (double)TotalReviews * 100);
    }

    public static PartnerStoreRatingSummary Empty(string storeId) =>
        new() { StoreId = storeId };

    public static PartnerStoreRatingSummary FromReviews(
        string storeId,
        IEnumerable<PartnerStoreReviewModel> reviews)
    {
        var approved = reviews.Where(r => r.IsApproved).ToList();
        if (!approved.Any()) return Empty(storeId);

        var breakdown = new Dictionary<int, int>
            { { 1, 0 }, { 2, 0 }, { 3, 0 }, { 4, 0 }, { 5, 0 } };

        foreach (var r in approved)
            breakdown[Math.Clamp(r.Rating, 1, 5)]++;

        return new PartnerStoreRatingSummary
        {
            StoreId       = storeId,
            AverageRating = (float)approved.Average(r => r.Rating),
            TotalReviews  = approved.Count,
            Breakdown     = breakdown
        };
    }
}
