namespace SubashaVentures.Domain.Product;

/// <summary>
/// View model for displaying product information in the UI
/// </summary>
public class ProductViewModel
{
    public string Id { get; set; } = string.Empty;
    public string Name { get; set; } = string.Empty;
    public string Slug { get; set; } = string.Empty;
    public string Description { get; set; } = string.Empty;
    public string LongDescription { get; set; } = string.Empty;
    
    // Pricing
    public decimal Price { get; set; }
    public decimal? OriginalPrice { get; set; }
    public bool IsOnSale { get; set; }
    public int Discount { get; set; }
    
    // Media
    public List<string> Images { get; set; } = new();
    public string? VideoUrl { get; set; }
    
    // Variants
    public List<string> Sizes { get; set; } = new();
    public List<string> Colors { get; set; } = new();
    
    // Inventory
    public int Stock { get; set; }
    public string Sku { get; set; } = string.Empty;
    
    // Classification
    public string CategoryId { get; set; } = string.Empty;
    public string Category { get; set; } = string.Empty;
    public string? SubCategory { get; set; }
    public string Brand { get; set; } = string.Empty;
    public List<string> Tags { get; set; } = new();
    
    // Rating & Reviews
    public float Rating { get; set; }
    public int ReviewCount { get; set; }
    
    // Metadata
    public DateTime CreatedAt { get; set; }
    public DateTime? UpdatedAt { get; set; }
    public bool IsActive { get; set; } = true;
    public bool IsFeatured { get; set; }
    
    // Computed properties
    public decimal FinalPrice => IsOnSale && OriginalPrice.HasValue 
        ? Price 
        : Price;
    
    public bool IsInStock => Stock > 0;
    
    public string StockStatus => Stock switch
    {
        0 => "Out of Stock",
        > 0 and <= 5 => "Low Stock",
        _ => "In Stock"
    };
}
