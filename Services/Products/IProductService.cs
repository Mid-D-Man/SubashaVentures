using SubashaVentures.Models.Supabase;
using SubashaVentures.Domain.Product;

namespace SubashaVentures.Services.Products;

/// <summary>
/// Product management service for admin operations (Supabase)
/// </summary>
public interface IProductService
{
    Task<ProductViewModel?> CreateProductAsync(CreateProductRequest request);
    Task<bool> UpdateProductAsync(string productId, UpdateProductRequest request);
    Task<bool> DeleteProductAsync(string productId);
    Task<bool> DeleteProductsAsync(List<string> productIds);
    Task<ProductViewModel?> GetProductByIdAsync(string productId);
    Task<List<ProductViewModel>> GetProductsAsync(int skip = 0, int take = 100);
    Task<bool> UpdateProductStockAsync(string productId, int newStock);
    string GenerateUniqueSku();
}


// DTOs
public class CreateProductRequest
{
    public string? Id { get; set; }  // Optional - will be generated if null
    public string Name { get; set; } = "";
    public string? Description { get; set; }
    public string? LongDescription { get; set; }
    public decimal Price { get; set; }
    public decimal? OriginalPrice { get; set; }
    public int Stock { get; set; }
    public string Sku { get; set; } = "";
    public string CategoryId { get; set; } = "";
    public string? Category { get; set; }
    public string? SubCategory { get; set; }
    public string? Brand { get; set; }
    public List<string>? Tags { get; set; }
    public List<string>? Sizes { get; set; }
    public List<string>? Colors { get; set; }
    public List<string>? ImageUrls { get; set; }
    public string? VideoUrl { get; set; }
    public bool IsFeatured { get; set; }
}

public class UpdateProductRequest
{
    public string? Name { get; set; }
    public string? Description { get; set; }
    public string? LongDescription { get; set; }
    public decimal? Price { get; set; }
    public decimal? OriginalPrice { get; set; }
    public int? Stock { get; set; }
    public string? CategoryId { get; set; }
    public string? Brand { get; set; }
    public List<string>? Tags { get; set; }
    public List<string>? Sizes { get; set; }
    public List<string>? Colors { get; set; }
    public bool? IsFeatured { get; set; }
    public bool? IsActive { get; set; }
}

/// <summary>
/// Product image upload result
/// </summary>
public class ProductImageUploadResult
{
    public bool Success { get; set; }
    public string? Url { get; set; }
    public string? FilePath { get; set; }
    public long? FileSize { get; set; }
    public string? ErrorMessage { get; set; }
}
