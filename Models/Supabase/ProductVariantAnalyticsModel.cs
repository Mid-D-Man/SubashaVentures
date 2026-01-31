// Models/Supabase/ProductVariantAnalyticsModel.cs - NEW: Track variant popularity
using Supabase.Postgrest.Attributes;
using Supabase.Postgrest.Models;

namespace SubashaVentures.Models.Supabase;

/// <summary>
/// Tracks analytics for individual product variants (size/color combinations)
/// Helps identify which variants are most popular
/// </summary>
[Table("product_variant_analytics")]
public class ProductVariantAnalyticsModel : BaseModel
{
    [PrimaryKey("id", false)]
    [Column("id")]
    public long Id { get; set; }
    
    [Column("product_id")]
    public long ProductId { get; set; }
    
    [Column("variant_key")]
    public string VariantKey { get; set; } = string.Empty;
    
    [Column("variant_size")]
    public string? VariantSize { get; set; }
    
    [Column("variant_color")]
    public string? VariantColor { get; set; }
    
    // ==================== METRICS ====================
    
    [Column("total_views")]
    public int TotalViews { get; set; }
    
    [Column("total_add_to_cart")]
    public int TotalAddToCart { get; set; }
    
    [Column("total_purchases")]
    public int TotalPurchases { get; set; }
    
    [Column("total_revenue")]
    public decimal TotalRevenue { get; set; }
    
    // ==================== TIMESTAMPS ====================
    
    [Column("last_purchased_at")]
    public DateTime? LastPurchasedAt { get; set; }
    
    [Column("created_at")]
    public DateTime CreatedAt { get; set; }
    
    [Column("updated_at")]
    public DateTime UpdatedAt { get; set; }
    
    // ==================== DISPLAY PROPERTIES ====================
    
    public string DisplayVariant => string.IsNullOrEmpty(VariantKey) 
        ? "Default" 
        : VariantKey.Replace("_", " / ");
    
    public string FormattedRevenue => $"₦{TotalRevenue:N0}";
}
