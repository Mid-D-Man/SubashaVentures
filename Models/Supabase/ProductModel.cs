
namespace SubashaVentures.Models.Supabase;


/// <summary>
/// Supabase product model with analytics
/// </summary>
public record ProductModel : ISecureEntity
{
    public string Id { get; init; } = string.Empty;
    public string Name { get; init; } = string.Empty;
    public string Slug { get; init; } = string.Empty;
    public string Description { get; init; } = string.Empty;
    public string LongDescription { get; init; } = string.Empty;
    
    // Pricing
    public decimal Price { get; init; }
    public decimal? OriginalPrice { get; init; }
    public bool IsOnSale { get; init; }
    public int Discount { get; init; }
    
    // Media (JSONB array)
    public List<string> Images { get; init; } = new();
    public string? VideoUrl { get; init; }
    
    // Variants (JSONB arrays)
    public List<string> Sizes { get; init; } = new();
    public List<string> Colors { get; init; } = new();
    
    // Inventory
    public int Stock { get; init; }
    public string Sku { get; init; } = string.Empty;
    
    // Classification
    public string CategoryId { get; init; } = string.Empty;
    public string Category { get; init; } = string.Empty;
    public string? SubCategory { get; init; }
    public string Brand { get; init; } = string.Empty;
    public List<string> Tags { get; init; } = new();
    
    // Rating & Reviews
    public float Rating { get; init; }
    public int ReviewCount { get; init; }
    
    // Analytics (aggregated from product_analytics table)
    public int ViewCount { get; init; }
    public int ClickCount { get; init; }
    public int AddToCartCount { get; init; }
    public int PurchaseCount { get; init; }
    public int SalesCount { get; init; }
    public decimal TotalRevenue { get; init; }
    
    // Metadata
    public bool IsActive { get; init; } = true;
    public bool IsFeatured { get; init; }
    public DateTime? LastViewedAt { get; init; }
    public DateTime? LastPurchasedAt { get; init; }
    
    // ISecureEntity
    public DateTime CreatedAt { get; init; }
    public string CreatedBy { get; init; } = string.Empty;
    public DateTime? UpdatedAt { get; init; }
    public string? UpdatedBy { get; init; }
    public bool IsDeleted { get; init; }
    public DateTime? DeletedAt { get; init; }
    public string? DeletedBy { get; init; }
}
