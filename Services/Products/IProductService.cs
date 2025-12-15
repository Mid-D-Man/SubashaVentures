using SubashaVentures.Models.Supabase;
using SubashaVentures.Domain.Product;

namespace SubashaVentures.Services.Products;

public interface IProductService
{
    Task<ProductViewModel?> CreateProductAsync(CreateProductRequest request);
    Task<bool> UpdateProductAsync(int productId, UpdateProductRequest request);
    Task<bool> DeleteProductAsync(int productId);
    Task<bool> DeleteProductsAsync(List<int> productIds);
    Task<ProductViewModel?> GetProductByIdAsync(int productId);
    Task<ProductViewModel?> GetProductBySkuAsync(string sku);
    Task<List<ProductViewModel>> GetProductsAsync(int skip = 0, int take = 100);
    Task<List<ProductViewModel>> GetAllProductsAsync(); // Add this
    Task<List<ProductViewModel>> GetProductsByCategoryAsync(string categoryId); // Add this
    Task<bool> UpdateProductStockAsync(int productId, int newStock);
    string GenerateUniqueSku();
    
    Task<ProductAnalyticsModel?> GetProductAnalyticsAsync(int productId);
    Task<bool> UpdateProductAnalyticsAsync(int productId);
    
    List<string> GetCommonTags();
    List<string> GetCommonSizes();
    List<string> GetCommonColors();
}

public class CreateProductRequest
{
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
    public List<string>? ImageUrls { get; set; }
    public string? VideoUrl { get; set; }
    public bool? IsFeatured { get; set; }
    public bool? IsActive { get; set; }
}
