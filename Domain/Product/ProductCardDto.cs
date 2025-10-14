namespace SubashaVentures.Domain.Product;

/// <summary>
/// Lightweight DTO for product cards in lists and grids
/// </summary>
public class ProductCardDto
{
    public string Id { get; set; } = string.Empty;
    public string Name { get; set; } = string.Empty;
    public string Slug { get; set; } = string.Empty;
    public string ThumbnailUrl { get; set; } = string.Empty;
    
    // Pricing
    public decimal Price { get; set; }
    public decimal? OriginalPrice { get; set; }
    public bool IsOnSale { get; set; }
    public int Discount { get; set; }
    
    // Quick info
    public string Category { get; set; } = string.Empty;
    public string Brand { get; set; } = string.Empty;
    
    // Rating
    public float Rating { get; set; }
    public int ReviewCount { get; set; }
    
    // Status
    public bool IsInStock { get; set; }
    public bool IsFeatured { get; set; }
    public bool IsNew { get; set; }
    
    // Computed
    public string DisplayPrice => $"₦{Price:N0}";
    public string DisplayOriginalPrice => OriginalPrice.HasValue 
        ? $"₦{OriginalPrice.Value:N0}" 
        : string.Empty;
}
