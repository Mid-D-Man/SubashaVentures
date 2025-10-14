
namespace SubashaVentures.Models.Firebase;


/// <summary>
/// Firebase Firestore product model (read-heavy cache)
/// </summary>
public record ProductModel
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
    
    // Media
    public List<string> Images { get; init; } = new();
    public string? VideoUrl { get; init; }
    
    // Variants
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
    
    // Metadata
    public DateTime CreatedAt { get; init; }
    public DateTime? UpdatedAt { get; init; }
    public bool IsActive { get; init; } = true;
    public bool IsFeatured { get; init; }
    public int ViewCount { get; init; }
    public int SalesCount { get; init; }
}

