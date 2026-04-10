using SubashaVentures.Models.Supabase;

namespace SubashaVentures.Domain.Partner;

/// <summary>
/// View model for partner product templates.
/// Used on the partner template editor and the admin review queue.
/// </summary>
public class PartnerTemplateViewModel
{
    public string Id { get; set; } = string.Empty;
    public string PartnerId { get; set; } = string.Empty;
    public string UserId { get; set; } = string.Empty;

    // ── Template Identity ──────────────────────────────────────
    public string TemplateName { get; set; } = string.Empty;
    public string Status { get; set; } = PartnerTemplateStatus.Draft;

    // ── Product Info ──────────────────────────────────────────
    public string ProductName { get; set; } = string.Empty;
    public string Description { get; set; } = string.Empty;

    // ── Category ──────────────────────────────────────────────
    public string CategoryId { get; set; } = string.Empty;
    public string CategoryName { get; set; } = string.Empty;

    // ── Variants ──────────────────────────────────────────────
    public List<PartnerTemplateVariantViewModel> Variants { get; set; } = new();

    // ── Pricing ───────────────────────────────────────────────
    public decimal ProposedPrice { get; set; }
    public decimal? ProposedOriginalPrice { get; set; }

    /// <summary>
    /// Admin override. If set, this is the price used when
    /// the template is converted to a product.
    /// </summary>
    public decimal? AdminApprovedPrice { get; set; }

    // ── Shipping ──────────────────────────────────────────────
    public decimal WeightKg { get; set; } = 1.0m;
    public bool HasFreeShipping { get; set; } = false;

    // ── Images ────────────────────────────────────────────────
    public List<string> ImageUrls { get; set; } = new();

    // ── Tags ──────────────────────────────────────────────────
    public List<string> Tags { get; set; } = new();

    // ── Rejection History ─────────────────────────────────────
    public List<TemplateRejectionViewModel> RejectionHistory { get; set; } = new();
    public int ResubmissionCount { get; set; }

    // ── Admin Review ──────────────────────────────────────────
    public string? AdminNotes { get; set; }
    public string? ReviewedBy { get; set; }
    public DateTime? ReviewedAt { get; set; }

    // ── Linked Product ─────────────────────────────────────────
    public int? ApprovedProductId { get; set; }

    // ── Timestamps ────────────────────────────────────────────
    public DateTime? SubmittedAt { get; set; }
    public DateTime CreatedAt { get; set; }
    public DateTime? UpdatedAt { get; set; }

    // ── Computed Display ───────────────────────────────────────
    public decimal EffectivePrice          => AdminApprovedPrice ?? ProposedPrice;
    public string DisplayEffectivePrice    => $"₦{EffectivePrice:N0}";
    public string DisplayProposedPrice     => $"₦{ProposedPrice:N0}";
    public string? ThumbnailUrl            => ImageUrls.FirstOrDefault();
    public int TotalStock                  => Variants.Sum(v => v.Stock);
    public bool HasPriceOverride           => AdminApprovedPrice.HasValue;
    public bool CanSubmit                  => IsDraft && ImageUrls.Count >= 1 && Variants.Count >= 1;
    public bool CanEdit                    => IsDraft || IsRejected;
    public bool IsDraft                    => Status == PartnerTemplateStatus.Draft;
    public bool IsPendingReview            => Status == PartnerTemplateStatus.PendingReview;
    public bool IsApproved                 => Status == PartnerTemplateStatus.Approved;
    public bool IsRejected                 => Status == PartnerTemplateStatus.Rejected;

    public string StatusDisplay => Status switch
    {
        PartnerTemplateStatus.Draft         => "Draft",
        PartnerTemplateStatus.PendingReview => "Pending Review",
        PartnerTemplateStatus.Approved      => "Approved",
        PartnerTemplateStatus.Rejected      => "Rejected",
        _                                   => Status
    };

    public string StatusBadgeClass => Status switch
    {
        PartnerTemplateStatus.Draft         => "badge-secondary",
        PartnerTemplateStatus.PendingReview => "badge-warning",
        PartnerTemplateStatus.Approved      => "badge-success",
        PartnerTemplateStatus.Rejected      => "badge-danger",
        _                                   => "badge-secondary"
    };

    public int? CalculatedDiscount
    {
        get
        {
            if (!ProposedOriginalPrice.HasValue || ProposedOriginalPrice <= ProposedPrice)
                return null;
            return (int)Math.Round(
                (ProposedOriginalPrice.Value - ProposedPrice)
                / ProposedOriginalPrice.Value * 100);
        }
    }

    public string SubmittedAtDisplay =>
        SubmittedAt?.ToString("MMM dd, yyyy HH:mm") ?? "Not submitted";

    public TemplateRejectionViewModel? LatestRejection =>
        RejectionHistory.OrderByDescending(r => r.RejectedAt).FirstOrDefault();

    // ── Conversion ─────────────────────────────────────────────

    public static PartnerTemplateViewModel FromCloudModel(PartnerProductTemplateModel model)
    {
        if (model == null) throw new ArgumentNullException(nameof(model));

        return new PartnerTemplateViewModel
        {
            Id                  = model.Id.ToString(),
            PartnerId           = model.PartnerId.ToString(),
            UserId              = model.UserId.ToString(),
            TemplateName        = model.TemplateName,
            Status              = model.Status,
            ProductName         = model.ProductName,
            Description         = model.Description,
            CategoryId          = model.CategoryId,
            CategoryName        = model.CategoryName,
            Variants            = model.Variants
                .Select(PartnerTemplateVariantViewModel.FromModel)
                .ToList(),
            ProposedPrice       = model.ProposedPrice,
            ProposedOriginalPrice = model.ProposedOriginalPrice,
            AdminApprovedPrice  = model.AdminApprovedPrice,
            WeightKg            = model.WeightKg,
            HasFreeShipping     = model.HasFreeShipping,
            ImageUrls           = model.ImageUrls ?? new List<string>(),
            Tags                = model.Tags ?? new List<string>(),
            RejectionHistory    = model.RejectionHistory
                .Select(TemplateRejectionViewModel.FromModel)
                .ToList(),
            ResubmissionCount   = model.ResubmissionCount,
            AdminNotes          = model.AdminNotes,
            ReviewedBy          = model.ReviewedBy,
            ReviewedAt          = model.ReviewedAt,
            ApprovedProductId   = model.ApprovedProductId,
            SubmittedAt         = model.SubmittedAt,
            CreatedAt           = model.CreatedAt,
            UpdatedAt           = model.UpdatedAt
        };
    }

    public static List<PartnerTemplateViewModel> FromCloudModels(
        IEnumerable<PartnerProductTemplateModel> models)
    {
        if (models == null) return new List<PartnerTemplateViewModel>();
        return models.Select(FromCloudModel).ToList();
    }
}

/// <summary>
/// View model for a single simplified variant inside a template.
/// </summary>
public class PartnerTemplateVariantViewModel
{
    public string Sku { get; set; } = string.Empty;
    public string? Size { get; set; }
    public string? Color { get; set; }
    public int Stock { get; set; }
    public decimal PriceAdjustment { get; set; } = 0m;

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

    public string DisplayPriceAdjustment =>
        PriceAdjustment == 0
            ? "No adjustment"
            : PriceAdjustment > 0
                ? $"+₦{PriceAdjustment:N0}"
                : $"-₦{Math.Abs(PriceAdjustment):N0}";

    public static PartnerTemplateVariantViewModel FromModel(PartnerTemplateVariant model)
    {
        if (model == null) throw new ArgumentNullException(nameof(model));

        return new PartnerTemplateVariantViewModel
        {
            Sku             = model.Sku,
            Size            = model.Size,
            Color           = model.Color,
            Stock           = model.Stock,
            PriceAdjustment = model.PriceAdjustment
        };
    }
}

/// <summary>
/// View model for a single rejection history entry.
/// </summary>
public class TemplateRejectionViewModel
{
    public DateTime RejectedAt { get; set; }
    public string RejectedBy { get; set; } = string.Empty;
    public string Reason { get; set; } = string.Empty;
    public List<string> FieldsFlagged { get; set; } = new();

    public string RejectedAtDisplay =>
        RejectedAt.ToString("MMM dd, yyyy HH:mm");

    public string FieldsFlaggedDisplay =>
        FieldsFlagged.Count > 0
            ? string.Join(", ", FieldsFlagged)
            : "None specified";

    public static TemplateRejectionViewModel FromModel(TemplateRejectionEntry model)
    {
        if (model == null) throw new ArgumentNullException(nameof(model));

        return new TemplateRejectionViewModel
        {
            RejectedAt    = model.RejectedAt,
            RejectedBy    = model.RejectedBy,
            Reason        = model.Reason,
            FieldsFlagged = model.FieldsFlagged ?? new List<string>()
        };
    }
}

/// <summary>
/// Request to create or update a template draft.
/// </summary>
public class SaveTemplateRequest
{
    public string? TemplateId { get; set; }
    public string TemplateName { get; set; } = string.Empty;
    public string ProductName { get; set; } = string.Empty;
    public string Description { get; set; } = string.Empty;
    public string CategoryId { get; set; } = string.Empty;
    public string CategoryName { get; set; } = string.Empty;
    public List<PartnerTemplateVariantViewModel> Variants { get; set; } = new();
    public decimal ProposedPrice { get; set; }
    public decimal? ProposedOriginalPrice { get; set; }
    public decimal WeightKg { get; set; } = 1.0m;
    public bool HasFreeShipping { get; set; } = false;
    public List<string> ImageUrls { get; set; } = new();
    public List<string> Tags { get; set; } = new();
}

/// <summary>
/// Request when admin approves a template with optional overrides.
/// </summary>
public class ApproveTemplateRequest
{
    public string TemplateId { get; set; } = string.Empty;
    public string AdminUserId { get; set; } = string.Empty;

    /// <summary>
    /// Override the partner's proposed price. Null = use proposed price.
    /// </summary>
    public decimal? PriceOverride { get; set; }

    /// <summary>
    /// Optional admin notes shown only to admin.
    /// </summary>
    public string? AdminNotes { get; set; }
}

/// <summary>
/// Request when admin rejects a template.
/// </summary>
public class RejectTemplateRequest
{
    public string TemplateId { get; set; } = string.Empty;
    public string AdminUserId { get; set; } = string.Empty;
    public string Reason { get; set; } = string.Empty;
    public List<string> FieldsFlagged { get; set; } = new();
    public string? AdminNotes { get; set; }
}
