// Domain/Product/ProductViewModel.cs - UPDATED: Removed analytics properties
namespace SubashaVentures.Domain.Product;

using SubashaVentures.Models.Supabase;

/// <summary>
/// View model for displaying product information in the UI
/// UPDATED: Analytics properties removed (use ProductAnalyticsModel separately)
/// </summary>
public class ProductViewModel
{
    public int Id { get; set; }
    public string Name { get; set; } = string.Empty;
    public string Slug { get; set; } = string.Empty;
    public string Description { get; set; } = string.Empty;
    public string LongDescription { get; set; } = string.Empty;
    
    // Partnership
    public bool IsOwnedByStore { get; set; } = true;
    public Guid? PartnerId { get; set; }
    public string? PartnerName { get; set; }
    public decimal? PartnerCommissionRate { get; set; }
    
    // Pricing 
    public decimal Price { get; set; }
    public decimal? OriginalPrice { get; set; }
    public bool IsOnSale { get; set; }
    public int Discount { get; set; }
    
    // Media
    public List<string> Images { get; set; } = new();
    public string? VideoUrl { get; set; }
    
    // Variants
    public Dictionary<string, ProductVariant> Variants { get; set; } = new();
    public List<string> Sizes { get; set; } = new();
    public List<string> Colors { get; set; } = new();
    
    // Inventory
    public int Stock { get; set; }
    public string Sku { get; set; } = string.Empty;
    
    // Base Shipping
    public decimal BaseWeight { get; set; } = 1.0m;
    public decimal BaseShippingCost { get; set; } = 2000m;
    public bool HasFreeShipping { get; set; } = false;
    
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
    
    // Display helpers
    public string DisplayPrice => $"₦{Price:N0}";
    public string DisplayOriginalPrice => OriginalPrice.HasValue ? $"₦{OriginalPrice.Value:N0}" : string.Empty;
    public string DisplayRating => $"{Rating:F1}/5";
    
    // ==================== CONVERSION METHODS ====================
    
    public static ProductViewModel FromCloudModel(ProductModel model)
    {
        if (model == null)
            throw new ArgumentNullException(nameof(model));
            
        return new ProductViewModel
        {
            Id = model.Id,
            Name = model.Name,
            Slug = model.Slug,
            Description = model.Description,
            LongDescription = model.LongDescription,
            
            IsOwnedByStore = model.IsOwnedByStore,
            PartnerId = model.PartnerId,
            
            Price = model.Price,
            OriginalPrice = model.OriginalPrice,
            IsOnSale = model.IsOnSale,
            Discount = model.Discount,
            
            Images = model.Images ?? new List<string>(),
            VideoUrl = model.VideoUrl,
            
            Variants = model.Variants ?? new Dictionary<string, ProductVariant>(),
            Sizes = model.Sizes ?? new List<string>(),
            Colors = model.Colors ?? new List<string>(),
            
            Stock = model.Stock,
            Sku = model.Sku,
            
            BaseWeight = model.BaseWeight,
            BaseShippingCost = model.BaseShippingCost,
            HasFreeShipping = model.HasFreeShipping,
            
            CategoryId = model.CategoryId,
            Category = model.Category,
            SubCategory = model.SubCategory,
            Brand = model.Brand,
            Tags = model.Tags ?? new List<string>(),
            
            Rating = model.Rating,
            ReviewCount = model.ReviewCount,
            
            CreatedAt = model.CreatedAt,
            UpdatedAt = model.UpdatedAt,
            IsActive = model.IsActive,
            IsFeatured = model.IsFeatured
        };
    }
    
    public ProductModel ToCloudModel()
    {
        return new ProductModel
        {
            Id = this.Id,
            Name = this.Name,
            Slug = this.Slug,
            Description = this.Description,
            LongDescription = this.LongDescription,
            
            IsOwnedByStore = this.IsOwnedByStore,
            PartnerId = this.PartnerId,
            
            Price = this.Price,
            OriginalPrice = this.OriginalPrice,
            IsOnSale = this.IsOnSale,
            Discount = this.Discount,
            
            Images = this.Images ?? new List<string>(),
            VideoUrl = this.VideoUrl,
            
            Variants = this.Variants ?? new Dictionary<string, ProductVariant>(),
            
            Sku = this.Sku,
            
            BaseWeight = this.BaseWeight,
            BaseShippingCost = this.BaseShippingCost,
            HasFreeShipping = this.HasFreeShipping,
            
            CategoryId = this.CategoryId,
            Category = this.Category,
            SubCategory = this.SubCategory,
            Brand = this.Brand,
            Tags = this.Tags ?? new List<string>(),
            
            Rating = this.Rating,
            ReviewCount = this.ReviewCount,
            
            CreatedAt = this.CreatedAt,
            UpdatedAt = this.UpdatedAt,
            IsActive = this.IsActive,
            IsFeatured = this.IsFeatured
        };
    }
    
    public static List<ProductViewModel> FromCloudModels(IEnumerable<ProductModel> models)
    {
        if (models == null)
            return new List<ProductViewModel>();
            
        return models.Select(FromCloudModel).ToList();
    }
    
    // Variant helper methods
    public decimal GetVariantPrice(string? variantKey) => 
        this.ToCloudModel().GetVariantPrice(variantKey);
    
    public decimal GetVariantShippingCost(string? variantKey) => 
        this.ToCloudModel().GetVariantShippingCost(variantKey);
    
    public int GetVariantStock(string? variantKey) => 
        this.ToCloudModel().GetVariantStock(variantKey);
    
    public string GetVariantImage(string? variantKey) => 
        this.ToCloudModel().GetVariantImage(variantKey);
}
