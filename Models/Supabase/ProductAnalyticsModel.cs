using Supabase.Postgrest.Attributes;
using Supabase.Postgrest.Models;

namespace SubashaVentures.Models.Supabase;

/// <summary>
/// Product analytics - shares same ID as product
/// </summary>
[Table("product_analytics")]
public class ProductAnalyticsModel : BaseModel
{
    // SAME ID AS PRODUCT - Primary Key
    [PrimaryKey("product_id", false)]
    [Column("product_id")]
    public int ProductId { get; set; }
    
    [Column("product_sku")]
    public string ProductSku { get; set; } = string.Empty;
    
    [Column("product_name")]
    public string ProductName { get; set; } = string.Empty;
    
    // Aggregated Metrics
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
    
    // Conversion Metrics
    [Column("view_to_cart_rate")]
    public decimal ViewToCartRate { get; set; } // (AddToCart / Views) * 100
    
    [Column("cart_to_purchase_rate")]
    public decimal CartToPurchaseRate { get; set; } // (Purchases / AddToCart) * 100
    
    [Column("overall_conversion_rate")]
    public decimal OverallConversionRate { get; set; } // (Purchases / Views) * 100
    
    // Time-based Metrics
    [Column("last_viewed_at")]
    public DateTime? LastViewedAt { get; set; }
    
    [Column("last_purchased_at")]
    public DateTime? LastPurchasedAt { get; set; }
    
    [Column("first_sale_date")]
    public DateTime? FirstSaleDate { get; set; }
    
    // Performance Indicators
    [Column("is_trending")]
    public bool IsTrending { get; set; } // Views increased >50% this week
    
    [Column("is_best_seller")]
    public bool IsBestSeller { get; set; } // Top 10% in purchases
    
    [Column("needs_attention")]
    public bool NeedsAttention { get; set; } // High views but low conversion
    
    // Timestamps
    [Column("created_at")]
    public DateTime CreatedAt { get; set; }
    
    [Column("updated_at")]
    public DateTime UpdatedAt { get; set; }
}