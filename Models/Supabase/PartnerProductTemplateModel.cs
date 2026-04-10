using Supabase.Postgrest.Attributes;
using Supabase.Postgrest.Models;
using Newtonsoft.Json;

namespace SubashaVentures.Models.Supabase;

/// <summary>
/// Maps to partner_product_templates table.
/// Partner-submitted product proposals awaiting admin approval.
/// Admin converts approved templates into real ProductModel rows
/// via the approve-product-template edge function.
/// </summary>
[Table("partner_product_templates")]
public class PartnerProductTemplateModel : BaseModel
{
    [PrimaryKey("id", false)]
    [Column("id")]
    public Guid Id { get; set; }

    // ── Ownership ──────────────────────────────────────────────
    [Column("partner_id")]
    public Guid PartnerId { get; set; }

    [Column("user_id")]
    public Guid UserId { get; set; }

    // ── Template Identity ──────────────────────────────────────
    [Column("template_name")]
    public string TemplateName { get; set; } = string.Empty;

    // ── Status ─────────────────────────────────────────────────
    [Column("status")]
    public string Status { get; set; } = PartnerTemplateStatus.Draft;

    // ── Basic Product Info ─────────────────────────────────────
    [Column("product_name")]
    public string ProductName { get; set; } = string.Empty;

    [Column("description")]
    public string Description { get; set; } = string.Empty;

    // ── Category ──────────────────────────────────────────────
    [Column("category_id")]
    public string CategoryId { get; set; } = string.Empty;

    [Column("category_name")]
    public string CategoryName { get; set; } = string.Empty;

    // ── Simplified Variants ────────────────────────────────────
    /// <summary>
    /// Simplified variant array. At least one required.
    /// Each entry: { sku, size?, color?, stock, price_adjustment? }
    /// Converted to full ProductVariant format on approval.
    /// </summary>
    [Column("variants")]
    public List<PartnerTemplateVariant> Variants { get; set; } = new();

    // ── Pricing ───────────────────────────────────────────────
    [Column("proposed_price")]
    public decimal ProposedPrice { get; set; }

    [Column("proposed_original_price")]
    public decimal? ProposedOriginalPrice { get; set; }

    /// <summary>
    /// Admin may override the proposed price before approving.
    /// NULL means use ProposedPrice as-is.
    /// </summary>
    [Column("admin_approved_price")]
    public decimal? AdminApprovedPrice { get; set; }

    // ── Shipping ──────────────────────────────────────────────
    [Column("weight_kg")]
    public decimal WeightKg { get; set; } = 1.0m;

    [Column("has_free_shipping")]
    public bool HasFreeShipping { get; set; } = false;

    // ── Images (Cloudflare R2 URLs) ───────────────────────────
    /// <summary>
    /// R2 object paths. Pattern: partners/{partner_id}/templates/{template_id}/{filename}
    /// Min 1 required at submission. Max 10.
    /// </summary>
    [Column("image_urls")]
    public List<string> ImageUrls { get; set; } = new();

    // ── Tags ──────────────────────────────────────────────────
    [Column("tags")]
    public List<string> Tags { get; set; } = new();

    // ── Rejection History ─────────────────────────────────────
    /// <summary>
    /// Append-only log of all admin rejections.
    /// Each entry: { rejected_at, rejected_by, reason, fields_flagged }
    /// </summary>
    [Column("rejection_history")]
    public List<TemplateRejectionEntry> RejectionHistory { get; set; } = new();

    [Column("resubmission_count")]
    public short ResubmissionCount { get; set; } = 0;

    // ── Admin Review ──────────────────────────────────────────
    [Column("admin_notes")]
    public string? AdminNotes { get; set; }

    [Column("reviewed_by")]
    public string? ReviewedBy { get; set; }

    [Column("reviewed_at")]
    public DateTime? ReviewedAt { get; set; }

    // ── Linked Product ─────────────────────────────────────────
    [Column("approved_product_id")]
    public int? ApprovedProductId { get; set; }

    // ── Timestamps ────────────────────────────────────────────
    [Column("submitted_at")]
    public DateTime? SubmittedAt { get; set; }

    [Column("created_at")]
    public DateTime CreatedAt { get; set; }

    [Column("updated_at")]
    public DateTime? UpdatedAt { get; set; }

    // ── Computed Helpers (not mapped) ──────────────────────────
    [JsonIgnore]
    public decimal EffectivePrice =>
        AdminApprovedPrice ?? ProposedPrice;

    [JsonIgnore]
    public string DisplayEffectivePrice => $"₦{EffectivePrice:N0}";

    [JsonIgnore]
    public string DisplayProposedPrice => $"₦{ProposedPrice:N0}";

    [JsonIgnore]
    public bool IsDraft          => Status == PartnerTemplateStatus.Draft;

    [JsonIgnore]
    public bool IsPendingReview  => Status == PartnerTemplateStatus.PendingReview;

    [JsonIgnore]
    public bool IsApproved       => Status == PartnerTemplateStatus.Approved;

    [JsonIgnore]
    public bool IsRejected       => Status == PartnerTemplateStatus.Rejected;

    [JsonIgnore]
    public bool CanSubmit        => IsDraft && ImageUrls.Count >= 1 && Variants.Count >= 1;

    [JsonIgnore]
    public bool CanEdit          => IsDraft || IsRejected;

    [JsonIgnore]
    public string? ThumbnailUrl  => ImageUrls.FirstOrDefault();

    [JsonIgnore]
    public int TotalStock        => Variants.Sum(v => v.Stock);

    [JsonIgnore]
    public bool HasPriceOverride => AdminApprovedPrice.HasValue;

    [JsonIgnore]
    public int? CalculatedDiscount
    {
        get
        {
            if (!ProposedOriginalPrice.HasValue || ProposedOriginalPrice <= ProposedPrice)
                return null;
            return (int)Math.Round(
                (ProposedOriginalPrice.Value - ProposedPrice) / ProposedOriginalPrice.Value * 100);
        }
    }
}

/// <summary>
/// Simplified variant entry inside a partner product template.
/// Converted to full ProductVariant on admin approval.
/// </summary>
public class PartnerTemplateVariant
{
    [JsonProperty("sku")]
    public string Sku { get; set; } = string.Empty;

    [JsonProperty("size")]
    public string? Size { get; set; }

    [JsonProperty("color")]
    public string? Color { get; set; }

    [JsonProperty("stock")]
    public int Stock { get; set; }

    [JsonProperty("price_adjustment")]
    public decimal PriceAdjustment { get; set; } = 0m;

    // ── Computed ──────────────────────────────────────────────
    [JsonIgnore]
    public string DisplayKey
    {
        get
        {
            var parts = new List<string>();
            if (!string.IsNullOrWhiteSpace(Size))  parts.Add(Size!.Trim());
            if (!string.IsNullOrWhiteSpace(Color)) parts.Add(Color!.Trim());
            return parts.Count > 0 ? string.Join(" / ", parts) : "Default";
        }
    }
}

/// <summary>
/// A single rejection entry appended to RejectionHistory.
/// </summary>
public class TemplateRejectionEntry
{
    [JsonProperty("rejected_at")]
    public DateTime RejectedAt { get; set; } = DateTime.UtcNow;

    [JsonProperty("rejected_by")]
    public string RejectedBy { get; set; } = string.Empty;

    [JsonProperty("reason")]
    public string Reason { get; set; } = string.Empty;

    [JsonProperty("fields_flagged")]
    public List<string> FieldsFlagged { get; set; } = new();
}

/// <summary>
/// Valid status values for partner product templates.
/// </summary>
public static class PartnerTemplateStatus
{
    public const string Draft         = "draft";
    public const string PendingReview = "pending_review";
    public const string Approved      = "approved";
    public const string Rejected      = "rejected";

    public static readonly IReadOnlyList<string> All = new[]
    {
        Draft, PendingReview, Approved, Rejected
    };

    public static readonly IReadOnlyList<string> Editable = new[]
    {
        Draft, Rejected
    };
}
