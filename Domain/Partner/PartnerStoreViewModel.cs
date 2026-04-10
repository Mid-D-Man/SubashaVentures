using SubashaVentures.Models.Supabase;

namespace SubashaVentures.Domain.Partner;

/// <summary>
/// View model for a partner's public-facing store.
/// Used on the public store page and the partner's own store editor.
/// </summary>
public class PartnerStoreViewModel
{
    public string Id { get; set; } = string.Empty;
    public string PartnerId { get; set; } = string.Empty;
    public string UserId { get; set; } = string.Empty;

    // ── Store Identity ─────────────────────────────────────────
    public string StoreName { get; set; } = string.Empty;
    public string StoreSlug { get; set; } = string.Empty;
    public string? Tagline { get; set; }
    public string? Description { get; set; }
    public string Location { get; set; } = string.Empty;

    // ── Media ─────────────────────────────────────────────────
    /// <summary>
    /// Cloudflare R2 object path.
    /// Served via: https://r2-proxy.mysubasha.com/{LogoUrl}
    /// </summary>
    public string? LogoUrl { get; set; }

    /// <summary>
    /// Cloudflare R2 object path.
    /// </summary>
    public string? BannerUrl { get; set; }

    // ── Public Contact ─────────────────────────────────────────
    public string? PublicPhone { get; set; }
    public string? PublicEmail { get; set; }

    // ── Metrics ───────────────────────────────────────────────
    public bool IsActive { get; set; } = true;
    public int TotalProducts { get; set; }
    public decimal TotalSales { get; set; }
    public int TotalOrders { get; set; }

    // ── Timestamps ────────────────────────────────────────────
    public DateTime CreatedAt { get; set; }
    public DateTime? UpdatedAt { get; set; }

    // ── Computed Display ───────────────────────────────────────
    public string DisplayTotalSales => $"₦{TotalSales:N0}";
    public string PublicStoreUrl    => $"/store/{StoreSlug}";
    public bool HasLogo             => !string.IsNullOrEmpty(LogoUrl);
    public bool HasBanner           => !string.IsNullOrEmpty(BannerUrl);

    public string LocationDisplay => Location switch
    {
        "Abuja"  => "Abuja, FCT",
        "Kaduna" => "Kaduna State",
        _        => Location
    };

    public string CreatedAtDisplay =>
        CreatedAt.ToString("MMM yyyy");

    // ── Conversion ─────────────────────────────────────────────

    public static PartnerStoreViewModel FromCloudModel(PartnerStoreModel model)
    {
        if (model == null) throw new ArgumentNullException(nameof(model));

        return new PartnerStoreViewModel
        {
            Id            = model.Id.ToString(),
            PartnerId     = model.PartnerId.ToString(),
            UserId        = model.UserId.ToString(),
            StoreName     = model.StoreName,
            StoreSlug     = model.StoreSlug,
            Tagline       = model.Tagline,
            Description   = model.Description,
            Location      = model.Location,
            LogoUrl       = model.LogoUrl,
            BannerUrl     = model.BannerUrl,
            PublicPhone   = model.PublicPhone,
            PublicEmail   = model.PublicEmail,
            IsActive      = model.IsActive,
            TotalProducts = model.TotalProducts,
            TotalSales    = model.TotalSales,
            TotalOrders   = model.TotalOrders,
            CreatedAt     = model.CreatedAt,
            UpdatedAt     = model.UpdatedAt
        };
    }

    public static List<PartnerStoreViewModel> FromCloudModels(
        IEnumerable<PartnerStoreModel> models)
    {
        if (models == null) return new List<PartnerStoreViewModel>();
        return models.Select(FromCloudModel).ToList();
    }
}

/// <summary>
/// Request data when a partner updates their store profile.
/// Structural fields (is_active, store_slug, partner_id) are excluded —
/// those are admin-only.
/// </summary>
public class UpdateStoreRequest
{
    public string? StoreName { get; set; }
    public string? Tagline { get; set; }
    public string? Description { get; set; }

    /// <summary>
    /// New R2 object path for logo.
    /// Set by CloudflareR2Service after upload.
    /// </summary>
    public string? LogoUrl { get; set; }

    /// <summary>
    /// New R2 object path for banner.
    /// </summary>
    public string? BannerUrl { get; set; }

    public string? PublicPhone { get; set; }
    public string? PublicEmail { get; set; }
}
