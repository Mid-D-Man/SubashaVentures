namespace SubashaVentures.Domain.Product;

using SubashaVentures.Models.Supabase;

/// <summary>
/// Lightweight DTO for product cards in lists and grids
/// </summary>
public class ProductCardDto
{
    public string Id { get; set; } = string.Empty;
    public string Name { get; set; } = string.Empty;
    public string Slug { get; set; } = string.Empty;
    public string ThumbnailUrl { get; set; } = string.Empty;
    
    public decimal Price { get; set; }
    public decimal? OriginalPrice { get; set; }
    public bool IsOnSale { get; set; }
    public int Discount { get; set; }
    
    public string Category { get; set; } = string.Empty;
    public string Brand { get; set; } = string.Empty;
    
    public float Rating { get; set; }
    public int ReviewCount { get; set; }
    
    public bool IsInStock { get; set; }
    public bool IsFeatured { get; set; }
    public bool IsNew { get; set; }
    
    // Partnership
    public bool IsOwnedByStore { get; set; } = true;
    public string? PartnerName { get; set; }
    
    // Shipping
    public bool HasFreeShipping { get; set; }
    
    public string DisplayPrice => $"₦{Price:N0}";
    public string DisplayOriginalPrice => OriginalPrice.HasValue 
        ? $"₦{OriginalPrice.Value:N0}" 
        : string.Empty;
    
    // ==================== CONVERSION METHODS ====================
    
    public static ProductCardDto FromCloudModel(ProductModel model)
    {
        if (model == null)
            throw new ArgumentNullException(nameof(model));
            
        return new ProductCardDto
        {
            Id = model.Id.ToString(),
            Name = model.Name,
            Slug = model.Slug,
            ThumbnailUrl = model.Images?.FirstOrDefault() ?? string.Empty,
            Price = model.Price,
            OriginalPrice = model.OriginalPrice,
            IsOnSale = model.IsOnSale,
            Discount = model.Discount,
            Category = model.Category,
            Brand = model.Brand,
            Rating = model.Rating,
            ReviewCount = model.ReviewCount,
            IsInStock = model.Stock > 0,
            IsFeatured = model.IsFeatured,
            IsNew = (DateTime.UtcNow - model.CreatedAt).TotalDays <= 30,
            IsOwnedByStore = model.IsOwnedByStore,
            HasFreeShipping = model.HasFreeShipping
        };
    }
    
    public static ProductCardDto FromViewModel(ProductViewModel model)
    {
        if (model == null)
            throw new ArgumentNullException(nameof(model));
            
        return new ProductCardDto
        {
            Id = model.Id.ToString(),
            Name = model.Name,
            Slug = model.Slug,
            ThumbnailUrl = model.Images?.FirstOrDefault() ?? string.Empty,
            Price = model.Price,
            OriginalPrice = model.OriginalPrice,
            IsOnSale = model.IsOnSale,
            Discount = model.Discount,
            Category = model.Category,
            Brand = model.Brand,
            Rating = model.Rating,
            ReviewCount = model.ReviewCount,
            IsInStock = model.IsInStock,
            IsFeatured = model.IsFeatured,
            IsNew = (DateTime.UtcNow - model.CreatedAt).TotalDays <= 30,
            IsOwnedByStore = model.IsOwnedByStore,
            HasFreeShipping = model.HasFreeShipping
        };
    }
    
    public static List<ProductCardDto> FromCloudModels(IEnumerable<ProductModel> models)
    {
        if (models == null)
            return new List<ProductCardDto>();
            
        return models.Select(FromCloudModel).ToList();
    }
    
    public static List<ProductCardDto> FromViewModels(IEnumerable<ProductViewModel> models)
    {
        if (models == null)
            return new List<ProductCardDto>();
            
        return models.Select(FromViewModel).ToList();
    }
}