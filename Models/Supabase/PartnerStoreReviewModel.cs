// Models/Supabase/PartnerStoreReviewModel.cs
using Supabase.Postgrest.Attributes;
using Supabase.Postgrest.Models;
using Newtonsoft.Json;

namespace SubashaVentures.Models.Supabase;

/// <summary>
/// Maps to partner_store_reviews table.
/// Customer reviews left on a partner's public store page
/// (separate from product reviews which live in Firebase).
/// 
/// Suggested migration:
///   CREATE TABLE partner_store_reviews (
///     id               uuid PRIMARY KEY DEFAULT gen_random_uuid(),
///     store_id         uuid NOT NULL REFERENCES partner_stores(id) ON DELETE CASCADE,
///     partner_id       uuid NOT NULL REFERENCES partners(id) ON DELETE CASCADE,
///     user_id          uuid NOT NULL REFERENCES auth.users(id) ON DELETE CASCADE,
///     user_name        text NOT NULL,
///     user_avatar      text,
///     rating           smallint NOT NULL CHECK (rating BETWEEN 1 AND 5),
///     title            text,
///     comment          text NOT NULL CHECK (char_length(comment) >= 10),
///     is_verified_purchase bool NOT NULL DEFAULT false,
///     helpful_count    integer NOT NULL DEFAULT 0,
///     is_approved      bool NOT NULL DEFAULT false,
///     created_at       timestamptz NOT NULL DEFAULT now(),
///     updated_at       timestamptz,
///     UNIQUE (store_id, user_id)   -- one review per user per store
///   );
///   CREATE INDEX ON partner_store_reviews (store_id, is_approved);
///   CREATE INDEX ON partner_store_reviews (user_id);
/// </summary>
[Table("partner_store_reviews")]
public class PartnerStoreReviewModel : BaseModel
{
    [PrimaryKey("id", false)]
    [Column("id")]
    public Guid Id { get; set; }

    [Column("store_id")]
    public Guid StoreId { get; set; }

    [Column("partner_id")]
    public Guid PartnerId { get; set; }

    [Column("user_id")]
    public Guid UserId { get; set; }

    // ── Reviewer Identity ──────────────────────────────────────
    [Column("user_name")]
    public string UserName { get; set; } = string.Empty;

    [Column("user_avatar")]
    public string? UserAvatar { get; set; }

    // ── Review Content ─────────────────────────────────────────
    [Column("rating")]
    public short Rating { get; set; }

    [Column("title")]
    public string? Title { get; set; }

    [Column("comment")]
    public string Comment { get; set; } = string.Empty;

    // ── Metadata ──────────────────────────────────────────────
    [Column("is_verified_purchase")]
    public bool IsVerifiedPurchase { get; set; } = false;

    [Column("helpful_count")]
    public int HelpfulCount { get; set; } = 0;

    [Column("is_approved")]
    public bool IsApproved { get; set; } = false;

    // ── Timestamps ────────────────────────────────────────────
    [Column("created_at")]
    public DateTime CreatedAt { get; set; }

    [Column("updated_at")]
    public DateTime? UpdatedAt { get; set; }

    // ── Computed Helpers (not mapped) ──────────────────────────
    [JsonIgnore]
    public string DisplayRating => $"{Rating}/5";

    [JsonIgnore]
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
}

/// <summary>
/// Aggregate rating summary for a partner store.
/// Computed at service layer — not a DB model.
/// </summary>
public class PartnerStoreRatingSummary
{
    public Guid StoreId { get; set; }

    public float AverageRating { get; set; }
    public int TotalReviews { get; set; }

    // Star breakdown: key = star count (1-5), value = number of reviews
    public Dictionary<int, int> Breakdown { get; set; } = new()
    {
        { 1, 0 }, { 2, 0 }, { 3, 0 }, { 4, 0 }, { 5, 0 }
    };

    // ── Computed Display ───────────────────────────────────────
    public string DisplayAverage => TotalReviews > 0
        ? AverageRating.ToString("F1")
        : "—";

    public string DisplayTotal => TotalReviews == 1
        ? "1 review"
        : $"{TotalReviews} reviews";

    public int PercentFor(int stars)
    {
        if (TotalReviews == 0) return 0;
        return (int)Math.Round(Breakdown.GetValueOrDefault(stars, 0) / (double)TotalReviews * 100);
    }

    public static PartnerStoreRatingSummary Empty(Guid storeId) =>
        new() { StoreId = storeId };

    public static PartnerStoreRatingSummary FromReviews(
        Guid storeId,
        IEnumerable<PartnerStoreReviewModel> reviews)
    {
        var list = reviews.Where(r => r.IsApproved).ToList();

        if (!list.Any()) return Empty(storeId);

        var breakdown = new Dictionary<int, int> { { 1,0 }, { 2,0 }, { 3,0 }, { 4,0 }, { 5,0 } };
        foreach (var r in list)
        {
            var star = Math.Clamp((int)r.Rating, 1, 5);
            breakdown[star]++;
        }

        return new PartnerStoreRatingSummary
        {
            StoreId       = storeId,
            AverageRating = (float)list.Average(r => r.Rating),
            TotalReviews  = list.Count,
            Breakdown     = breakdown
        };
    }
}
