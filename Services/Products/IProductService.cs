using SubashaVentures.Models.Supabase;
using SubashaVentures.Domain.Product;

namespace SubashaVentures.Services.Products;

public interface IProductService
{
    // Core CRUD
    Task<ProductViewModel?> CreateProductAsync(CreateProductRequest request);
    Task<bool> UpdateProductAsync(int productId, UpdateProductRequest request);
    Task<bool> DeleteProductAsync(int productId);
    Task<bool> DeleteProductsAsync(List<int> productIds);
    
    // Retrieval
    Task<ProductViewModel?> GetProductByIdAsync(int productId);
    Task<ProductViewModel?> GetProductBySkuAsync(string sku);
    Task<List<ProductViewModel>> GetProductsAsync(int skip = 0, int take = 100);
    Task<List<ProductViewModel>> GetAllProductsAsync();
    Task<List<ProductViewModel>> GetProductsByCategoryAsync(string categoryId);
    Task<List<ProductViewModel>> GetProductsByPartnerAsync(Guid partnerId);
    
    // Stock Management
    Task<bool> UpdateProductStockAsync(int productId, int newStock);
    Task<bool> UpdateVariantStockAsync(int productId, string variantKey, int newStock);
    
    // Variant Management
    Task<bool> AddProductVariantAsync(int productId, string variantKey, ProductVariant variant);
    Task<bool> UpdateProductVariantAsync(int productId, string variantKey, ProductVariant variant);
    Task<bool> RemoveProductVariantAsync(int productId, string variantKey);
    Task<Dictionary<string, ProductVariant>?> GetProductVariantsAsync(int productId);
    
    // Analytics
    Task<ProductAnalyticsModel?> GetProductAnalyticsAsync(int productId);
    Task<bool> UpdateProductAnalyticsAsync(int productId);
    
    // Utilities
    string GenerateUniqueSku();
    List<string> GetCommonTags();
    List<string> GetCommonSizes();
    List<string> GetCommonColors();
}

public class CreateProductRequest
{
    public string Name { get; set; } = "";
    public string? Description { get; set; }
    public string? LongDescription { get; set; }
    
    // Partnership
    public bool IsOwnedByStore { get; set; } = true;
    public Guid? PartnerId { get; set; }
    
    // Pricing
    public decimal Price { get; set; }
    public decimal? OriginalPrice { get; set; }
    
    // Media
    public List<string>? ImageUrls { get; set; }
    public string? VideoUrl { get; set; }
    
    // Variants (JSONB - single source of truth)
    public Dictionary<string, ProductVariant>? Variants { get; set; }
    
    // DO NOT SET: sizes, colors, stock - these are auto-populated by triggers
    
    // Shipping
    public decimal BaseWeight { get; set; } = 1.0m;
    public decimal BaseShippingCost { get; set; } = 2000m;
    public bool HasFreeShipping { get; set; } = false;
    
    // Inventory
    public string Sku { get; set; } = "";
    
    // Classification
    public string CategoryId { get; set; } = "";
    public string? Category { get; set; }
    public string? SubCategory { get; set; }
    public string? Brand { get; set; }
    public List<string>? Tags { get; set; }
    
    // Settings
    public bool IsFeatured { get; set; }
}

public class UpdateProductRequest
{
    public string? Name { get; set; }
    public string? Description { get; set; }
    public string? LongDescription { get; set; }
    
    // Partnership
    public bool? IsOwnedByStore { get; set; }
    public Guid? PartnerId { get; set; }
    
    // Pricing
    public decimal? Price { get; set; }
    public decimal? OriginalPrice { get; set; }
    
    // Media
    public List<string>? ImageUrls { get; set; }
    public string? VideoUrl { get; set; }
    
    // Variants (JSONB)
    public Dictionary<string, ProductVariant>? Variants { get; set; }
    
    // DO NOT SET: sizes, colors, stock - auto-populated
    
    // Shipping
    public decimal? BaseWeight { get; set; }
    public decimal? BaseShippingCost { get; set; }
    public bool? HasFreeShipping { get; set; }
    
    // Classification
    public string? CategoryId { get; set; }
    public string? Brand { get; set; }
    public List<string>? Tags { get; set; }
    
    // Settings
    public bool? IsFeatured { get; set; }
    public bool? IsActive { get; set; }
}