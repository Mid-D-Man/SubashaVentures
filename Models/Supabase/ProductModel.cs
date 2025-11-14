using Supabase.Postgrest.Attributes;
using Supabase.Postgrest.Models;

namespace SubashaVentures.Models.Supabase;

/// <summary>
/// Supabase product model with analytics
/// </summary>
[Table("products")]
public class ProductModel : BaseModel
{
    [PrimaryKey("id", true)]
    public string Id { get; set; } = string.Empty;
    
    [Column("name")]
    public string Name { get; set; } = string.Empty;
    
    [Column("slug")]
    public string Slug { get; set; } = string.Empty;
    
    [Column("description")]
    public string Description { get; set; } = string.Empty;
    
    [Column("long_description")]
    public string LongDescription { get; set; } = string.Empty;
    
    // Pricing
    [Column("price")]
    public decimal Price { get; set; }
    
    [Column("original_price")]
    public decimal? OriginalPrice { get; set; }
    
    [Column("is_on_sale")]
    public bool IsOnSale { get; set; }
    
    [Column("discount")]
    public int Discount { get; set; }
    
    // Media (JSONB array)
    [Column("images")]
    public List<string> Images { get; set; } = new();
    
    [Column("video_url")]
    public string? VideoUrl { get; set; }
    
    // Variants (JSONB arrays)
    [Column("sizes")]
    public List<string> Sizes { get; set; } = new();
    
    [Column("colors")]
    public List<string> Colors { get; set; } = new();
    
    // Inventory
    [Column("stock")]
    public int Stock { get; set; }
    
    [Column("sku")]
    public string Sku { get; set; } = string.Empty;
    
    // Classification
    [Column("category_id")]
    public string CategoryId { get; set; } = string.Empty;
    
    [Column("category")]
    public string Category { get; set; } = string.Empty;
    
    [Column("sub_category")]
    public string? SubCategory { get; set; }
    
    [Column("brand")]
    public string Brand { get; set; } = string.Empty;
    
    [Column("tags")]
    public List<string> Tags { get; set; } = new();
    
    // Rating & Reviews
    [Column("rating")]
    public float Rating { get; set; }
    
    [Column("review_count")]
    public int ReviewCount { get; set; }
    
    // Analytics (aggregated from product_analytics table)
    [Column("view_count")]
    public int ViewCount { get; set; }
    
    [Column("click_count")]
    public int ClickCount { get; set; }
    
    [Column("add_to_cart_count")]
    public int AddToCartCount { get; set; }
    
    [Column("purchase_count")]
    public int PurchaseCount { get; set; }
    
    [Column("sales_count")]
    public int SalesCount { get; set; }
    
    [Column("total_revenue")]
    public decimal TotalRevenue { get; set; }
    
    // Metadata
    [Column("is_active")]
    public bool IsActive { get; set; } = true;
    
    [Column("is_featured")]
    public bool IsFeatured { get; set; }
    
    [Column("last_viewed_at")]
    public DateTime? LastViewedAt { get; set; }
    
    [Column("last_purchased_at")]
    public DateTime? LastPurchasedAt { get; set; }
    
    // ISecureEntity
    [Column("created_at")]
    public DateTime CreatedAt { get; set; }
    
    [Column("created_by")]
    public string CreatedBy { get; set; } = string.Empty;
    
    [Column("updated_at")]
    public DateTime? UpdatedAt { get; set; }
    
    [Column("updated_by")]
    public string? UpdatedBy { get; set; }
    
    [Column("is_deleted")]
    public bool IsDeleted { get; set; }
    
    [Column("deleted_at")]
    public DateTime? DeletedAt { get; set; }
    
    [Column("deleted_by")]
    public string? DeletedBy { get; set; }
}
