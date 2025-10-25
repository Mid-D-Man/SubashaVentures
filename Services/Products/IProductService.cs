// Services/Products/IProductService.cs
using SubashaVentures.Models.Firebase;
using SubashaVentures.Domain.Product;

namespace SubashaVentures.Services.Products;

/// <summary>
/// Product management service for admin operations
/// </summary>
public interface IProductService
{
    // READ
    Task<ProductViewModel?> GetProductAsync(string id);
    Task<List<ProductViewModel>> GetProductsAsync(int skip = 0, int take = 50);
    Task<List<ProductViewModel>> SearchProductsAsync(string query);
    Task<List<ProductViewModel>> GetProductsByCategoryAsync(string categoryId);
    Task<int> GetProductCountAsync();
    
    // CREATE
    Task<ProductViewModel?> CreateProductAsync(CreateProductRequest request);
    
    // UPDATE
    Task<bool> UpdateProductAsync(string id, UpdateProductRequest request);
    Task<bool> UpdateProductImagesAsync(string id, List<string> imageUrls);
    Task<bool> UpdateProductStockAsync(string id, int quantity);
    
    // DELETE
    Task<bool> DeleteProductAsync(string id);
    Task<bool> DeleteProductsAsync(List<string> ids);
    
    // IMAGE MANAGEMENT
    Task<ProductImageUploadResult> UploadProductImageAsync(Stream imageStream, string fileName);
    Task<List<ProductImageUploadResult>> UploadProductImagesAsync(List<(Stream stream, string fileName)> files);
    Task<bool> DeleteProductImageAsync(string imagePath);
}

/// <summary>
/// Request to create a product
/// </summary>
public class CreateProductRequest
{
    public string Name { get; set; } = string.Empty;
    public string Description { get; set; } = string.Empty;
    public string LongDescription { get; set; } = string.Empty;
    public decimal Price { get; set; }
    public decimal? OriginalPrice { get; set; }
    public int Stock { get; set; }
    public string Sku { get; set; } = string.Empty;
    public string CategoryId { get; set; } = string.Empty;
    public string Brand { get; set; } = string.Empty;
    public List<string>? Tags { get; set; }
    public List<string>? Sizes { get; set; }
    public List<string>? Colors { get; set; }
    public List<string>? ImageUrls { get; set; }
    public bool IsFeatured { get; set; }
}

/// <summary>
/// Request to update a product
/// </summary>
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
