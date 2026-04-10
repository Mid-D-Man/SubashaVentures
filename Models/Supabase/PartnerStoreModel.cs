using Supabase.Postgrest.Attributes;
using Supabase.Postgrest.Models;
using Newtonsoft.Json;

namespace SubashaVentures.Models.Supabase;

/// <summary>
/// Maps to partner_stores table.
/// Public-facing storefront profile. One per partner.
/// Created atomically by approve-partner-application edge function.
/// Images are served via Cloudflare R2 proxy worker.
/// </summary>
[Table("partner_stores")]
public class PartnerStoreModel : BaseModel
{
    [PrimaryKey("id", false)]
    [Column("id")]
    public Guid Id { get; set; }

    // ── Links ──────────────────────────────────────────────────
    [Column("partner_id")]
    public Guid PartnerId { get; set; }

    [Column("user_id")]
    public Guid UserId { get; set; }

    // ── Store Identity ─────────────────────────────────────────
    [Column("store_name")]
    public string StoreName { get; set; } = string.Empty;

    [Column("store_slug")]
    public string StoreSlug { get; set; } = string.Empty;

    [Column("tagline")]
    public string? Tagline { get; set; }

    [Column("description")]
    public string? Description { get; set; }

    // ── Location ──────────────────────────────────────────────
    [Column("location")]
    public string Location { get; set; } = string.Empty;

    // ── Media (Cloudflare R2 URLs) ────────────────────────────
    /// <summary>
    /// Full R2 object path. Pattern: partners/{partner_id}/store/logo.{ext}
    /// Served via the R2 proxy Cloudflare Worker.
    /// </summary>
    [Column("logo_url")]
    public string? LogoUrl { get; set; }

    /// <summary>
    /// Full R2 object path. Pattern: partners/{partner_id}/store/banner.{ext}
    /// </summary>
    [Column("banner_url")]
    public string? BannerUrl { get; set; }

    // ── Public Contact ─────────────────────────────────────────
    [Column("public_phone")]
    public string? PublicPhone { get; set; }

    [Column("public_email")]
    public string? PublicEmail { get; set; }

    // ── Store Metrics ──────────────────────────────────────────
    [Column("is_active")]
    public bool IsActive { get; set; } = true;

    [Column("total_products")]
    public int TotalProducts { get; set; } = 0;

    [Column("total_sales")]
    public decimal TotalSales { get; set; } = 0;

    [Column("total_orders")]
    public int TotalOrders { get; set; } = 0;

    // ── Timestamps ────────────────────────────────────────────
    [Column("created_at")]
    public DateTime CreatedAt { get; set; }

    [Column("updated_at")]
    public DateTime? UpdatedAt { get; set; }

    // ── Computed Helpers (not mapped) ──────────────────────────
    [JsonIgnore]
    public string DisplayTotalSales => $"₦{TotalSales:N0}";

    [JsonIgnore]
    public string PublicStoreUrl => $"/store/{StoreSlug}";

    [JsonIgnore]
    public bool HasLogo => !string.IsNullOrEmpty(LogoUrl);

    [JsonIgnore]
    public bool HasBanner => !string.IsNullOrEmpty(BannerUrl);
}
