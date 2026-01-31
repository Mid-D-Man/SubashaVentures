// Models/Supabase/ProductAnalyticsModel.cs - UPDATED: Added wishlist tracking
using Supabase.Postgrest.Attributes;
using Supabase.Postgrest.Models;

namespace SubashaVentures.Models.Supabase;

/// <summary>
/// Product analytics - SINGLE SOURCE OF TRUTH for all product metrics
/// UPDATED: Added wishlist tracking columns
/// </summary>
[Table("product_analytics")]
public class ProductAnalyticsModel : BaseModel
{
    [PrimaryKey("product_id", false)]
    [Column("product_id")]
    public int ProductId { get; set; }
    
    [Column("product_sku")]
    public string ProductSku { get; set; } = string.Empty;
    
    [Column("product_name")]
    public string ProductName { get; set; } = string.Empty;
    
    // ==================== CORE METRICS ====================
    
    [Column("total_views")]
    public int TotalViews { get; set; }
    
    [Column("total_clicks")]
    public int TotalClicks { get; set; }
    
    [Column("total_add_to_cart")]
    public int TotalAddToCart { get; set; }
    
    [Column("total_purchases")]
    public int TotalPurchases { get; set; }
    
    [Column("total_revenue")]
    public decimal TotalRevenue { get; set; }
    
    // ==================== WISHLIST METRICS (NEW) ====================
    
    [Column("total_wishlist_adds")]
    public int TotalWishlistAdds { get; set; }
    
    [Column("last_wishlisted_at")]
    public DateTime? LastWishlistedAt { get; set; }
    
    // ==================== CONVERSION RATES ====================
    
    [Column("view_to_cart_rate")]
    public decimal ViewToCartRate { get; set; }
    
    [Column("cart_to_purchase_rate")]
    public decimal CartToPurchaseRate { get; set; }
    
    [Column("overall_conversion_rate")]
    public decimal OverallConversionRate { get; set; }
    
    [Column("wishlist_to_cart_rate")]
    public decimal WishlistToCartRate { get; set; }
    
    [Column("wishlist_to_purchase_rate")]
    public decimal WishlistToPurchaseRate { get; set; }
    
    // ==================== TIME-BASED METRICS ====================
    
    [Column("last_viewed_at")]
    public DateTime? LastViewedAt { get; set; }
    
    [Column("last_purchased_at")]
    public DateTime? LastPurchasedAt { get; set; }
    
    [Column("first_sale_date")]
    public DateTime? FirstSaleDate { get; set; }
    
    // ==================== PERFORMANCE INDICATORS ====================
    
    [Column("is_trending")]
    public bool IsTrending { get; set; }
    
    [Column("is_best_seller")]
    public bool IsBestSeller { get; set; }
    
    [Column("needs_attention")]
    public bool NeedsAttention { get; set; }
    
    // ==================== TIMESTAMPS ====================
    
    [Column("created_at")]
    public DateTime CreatedAt { get; set; }
    
    [Column("updated_at")]
    public DateTime UpdatedAt { get; set; }
}
