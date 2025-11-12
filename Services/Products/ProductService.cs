using SubashaVentures.Domain.Product;
using SubashaVentures.Models.Supabase;
using SubashaVentures.Services.Supabase;
using System.Text;

namespace SubashaVentures.Services.Products;

public class ProductService : IProductService
{
    private readonly ISupabaseService _supabaseService;
    private readonly ISupabaseStorageService _storageService;
    private readonly ILogger<ProductService> _logger;
    
    private const string ProductsTable = "products";
    private const string CategoriesTable = "categories";

    public ProductService(
        ISupabaseService supabaseService,
        ISupabaseStorageService storageService,
        ILogger<ProductService> logger)
    {
        _supabaseService = supabaseService;
        _storageService = storageService;
        _logger = logger;
    }

    public async Task<ProductViewModel?> GetProductAsync(string id)
    {
        try
        {
            var product = await _supabaseService.GetByIdAsync<ProductModel>(ProductsTable, id);
            return product != null ? MapToViewModel(product) : null;
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error getting product: {Id}", id);
            return null;
        }
    }

    public async Task<List<ProductViewModel>> GetProductsAsync(int skip = 0, int take = 50)
    {
        try
        {
            var filter = $"is_active = true AND is_deleted = false ORDER BY created_at DESC LIMIT {take} OFFSET {skip}";
            var products = await _supabaseService.QueryAsync<ProductModel>(ProductsTable, filter);
            
            return products.Select(MapToViewModel).ToList();
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error getting products");
            return new List<ProductViewModel>();
        }
    }

    public async Task<List<ProductViewModel>> SearchProductsAsync(string query)
    {
        try
        {
            // Escape single quotes in query
            var escapedQuery = query.Replace("'", "''");
            
            var filter = $"search_vector @@ plainto_tsquery('english', '{escapedQuery}') " +
                        $"ORDER BY ts_rank(search_vector, plainto_tsquery('english', '{escapedQuery}')) DESC";
            
            var products = await _supabaseService.QueryAsync<ProductModel>(ProductsTable, filter);
            return products.Select(MapToViewModel).ToList();
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error searching products");
            return new List<ProductViewModel>();
        }
    }

    public async Task<List<ProductViewModel>> GetProductsByCategoryAsync(string categoryId)
    {
        try
        {
            var filter = $"category_id = '{categoryId}' AND is_active = true AND is_deleted = false";
            var products = await _supabaseService.QueryAsync<ProductModel>(ProductsTable, filter);
            
            return products.Select(MapToViewModel).ToList();
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error getting products by category");
            return new List<ProductViewModel>();
        }
    }

    public async Task<int> GetProductCountAsync()
    {
        try
        {
            var count = await _supabaseService.ExecuteScalarAsync<int>(
                $"SELECT COUNT(*) FROM {ProductsTable} WHERE is_active = true AND is_deleted = false"
            );
            return count;
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error getting product count");
            return 0;
        }
    }

    public async Task<List<ProductViewModel>> GetTrendingProductsAsync(int count = 10)
    {
        try
        {
            var filter = $"is_active = true AND is_deleted = false " +
                        $"ORDER BY view_count DESC, purchase_count DESC LIMIT {count}";
            
            var products = await _supabaseService.QueryAsync<ProductModel>(ProductsTable, filter);
            return products.Select(MapToViewModel).ToList();
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error getting trending products");
            return new List<ProductViewModel>();
        }
    }

    public async Task<List<ProductViewModel>> GetFeaturedProductsAsync(int count = 10)
    {
        try
        {
            var filter = $"is_active = true AND is_deleted = false AND is_featured = true " +
                        $"ORDER BY created_at DESC LIMIT {count}";
            
            var products = await _supabaseService.QueryAsync<ProductModel>(ProductsTable, filter);
            return products.Select(MapToViewModel).ToList();
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error getting featured products");
            return new List<ProductViewModel>();
        }
    }

    public async Task<ProductViewModel?> CreateProductAsync(CreateProductRequest request)
    {
        try
        {
            if (string.IsNullOrEmpty(request.Name))
            {
                throw new ArgumentException("Product name is required");
            }

            if (request.Price <= 0)
            {
                throw new ArgumentException("Price must be greater than 0");
            }

            if (string.IsNullOrEmpty(request.Sku))
            {
                throw new ArgumentException("SKU is required");
            }

            if (string.IsNullOrEmpty(request.CategoryId))
            {
                throw new ArgumentException("Category is required");
            }

            var productId = Guid.NewGuid().ToString();
            var now = DateTime.UtcNow;

            var productModel = new ProductModel
            {
                Id = productId,
                Name = request.Name,
                Slug = GenerateSlug(request.Name),
                Description = request.Description,
                LongDescription = request.LongDescription,
                Price = request.Price,
                OriginalPrice = request.OriginalPrice,
                Stock = request.Stock,
                Sku = request.Sku,
                CategoryId = request.CategoryId,
                Category = await GetCategoryNameAsync(request.CategoryId),
                Brand = request.Brand ?? string.Empty,
                Tags = request.Tags ?? new List<string>(),
                Sizes = request.Sizes ?? new List<string>(),
                Colors = request.Colors ?? new List<string>(),
                Images = request.ImageUrls ?? new List<string>(),
                IsFeatured = request.IsFeatured,
                IsActive = true,
                CreatedAt = now,
                CreatedBy = "system", // TODO: Get from auth context
                ViewCount = 0,
                ClickCount = 0,
                AddToCartCount = 0,
                PurchaseCount = 0,
                SalesCount = 0,
                TotalRevenue = 0,
                Rating = 0,
                ReviewCount = 0,
                IsOnSale = request.OriginalPrice.HasValue && request.OriginalPrice > request.Price,
                Discount = request.OriginalPrice.HasValue 
                    ? (int)Math.Round(((request.OriginalPrice.Value - request.Price) / request.OriginalPrice.Value) * 100)
                    : 0,
                IsDeleted = false
            };

            var insertedId = await _supabaseService.InsertAsync(ProductsTable, productModel);
            
            if (string.IsNullOrEmpty(insertedId))
            {
                _logger.LogError("Failed to create product: No ID returned");
                return null;
            }

            _logger.LogInformation("Product created: {Name} (ID: {Id})", request.Name, insertedId);
            
            return await GetProductAsync(insertedId);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error creating product: {Name}", request.Name);
            throw; // Re-throw to let caller handle
        }
    }

    public async Task<bool> UpdateProductAsync(string id, UpdateProductRequest request)
    {
        try
        {
            var product = await _supabaseService.GetByIdAsync<ProductModel>(ProductsTable, id);
            if (product == null)
            {
                _logger.LogWarning("Product not found for update: {Id}", id);
                return false;
            }

            var updatedProduct = product with
            {
                Name = request.Name ?? product.Name,
                Slug = !string.IsNullOrEmpty(request.Name) ? GenerateSlug(request.Name) : product.Slug,
                Description = request.Description ?? product.Description,
                LongDescription = request.LongDescription ?? product.LongDescription,
                Price = request.Price ?? product.Price,
                OriginalPrice = request.OriginalPrice ?? product.OriginalPrice,
                Stock = request.Stock ?? product.Stock,
                CategoryId = request.CategoryId ?? product.CategoryId,
                Category = !string.IsNullOrEmpty(request.CategoryId) 
                    ? await GetCategoryNameAsync(request.CategoryId) 
                    : product.Category,
                Brand = request.Brand ?? product.Brand,
                Tags = request.Tags ?? product.Tags,
                Sizes = request.Sizes ?? product.Sizes,
                Colors = request.Colors ?? product.Colors,
                IsFeatured = request.IsFeatured ?? product.IsFeatured,
                IsActive = request.IsActive ?? product.IsActive,
                UpdatedAt = DateTime.UtcNow,
                UpdatedBy = "system", // TODO: Get from auth context
                IsOnSale = (request.OriginalPrice ?? product.OriginalPrice).HasValue && 
                          (request.OriginalPrice ?? product.OriginalPrice) > (request.Price ?? product.Price),
                Discount = (request.OriginalPrice ?? product.OriginalPrice).HasValue
                    ? (int)Math.Round((((request.OriginalPrice ?? product.OriginalPrice).Value - (request.Price ?? product.Price)) / 
                       (request.OriginalPrice ?? product.OriginalPrice).Value) * 100)
                    : product.Discount
            };

            var result = await _supabaseService.UpdateAsync(ProductsTable, id, updatedProduct);
            
            if (result)
            {
                _logger.LogInformation("Product updated: {Id}", id);
            }
            
            return result;
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error updating product: {Id}", id);
            return false;
        }
    }

    public async Task<bool> UpdateProductImagesAsync(string id, List<string> imageUrls)
    {
        try
        {
            var product = await _supabaseService.GetByIdAsync<ProductModel>(ProductsTable, id);
            if (product == null) return false;

            var updated = product with 
            { 
                Images = imageUrls, 
                UpdatedAt = DateTime.UtcNow,
                UpdatedBy = "system"
            };

            return await _supabaseService.UpdateAsync(ProductsTable, id, updated);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error updating product images: {Id}", id);
            return false;
        }
    }

    public async Task<bool> UpdateProductStockAsync(string id, int quantity)
    {
        try
        {
            var product = await _supabaseService.GetByIdAsync<ProductModel>(ProductsTable, id);
            if (product == null) return false;

            var updated = product with 
            { 
                Stock = quantity, 
                UpdatedAt = DateTime.UtcNow,
                UpdatedBy = "system"
            };

            return await _supabaseService.UpdateAsync(ProductsTable, id, updated);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error updating product stock: {Id}", id);
            return false;
        }
    }

    public async Task<bool> DeleteProductAsync(string id)
    {
        try
        {
            // Soft delete
            var product = await _supabaseService.GetByIdAsync<ProductModel>(ProductsTable, id);
            if (product == null) return false;

            var deleted = product with
            {
                IsDeleted = true,
                DeletedAt = DateTime.UtcNow,
                DeletedBy = "system" // TODO: Get from auth context
            };

            var result = await _supabaseService.UpdateAsync(ProductsTable, id, deleted);
            
            if (result)
            {
                _logger.LogInformation("Product soft-deleted: {Id}", id);
            }
            
            return result;
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error deleting product: {Id}", id);
            return false;
        }
    }

    public async Task<bool> DeleteProductsAsync(List<string> ids)
    {
        try
        {
            var tasks = ids.Select(id => DeleteProductAsync(id));
            var results = await Task.WhenAll(tasks);
            return results.All(r => r);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error deleting multiple products");
            return false;
        }
    }

    public async Task<ProductImageUploadResult> UploadProductImageAsync(Stream imageStream, string fileName)
    {
        try
        {
            var result = await _storageService.UploadImageAsync(imageStream, fileName, "products");
            
            return new ProductImageUploadResult
            {
                Success = result.Success,
                Url = result.PublicUrl,
                FilePath = result.FilePath,
                FileSize = result.FileSize,
                ErrorMessage = result.ErrorMessage
            };
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error uploading product image");
            return new ProductImageUploadResult
            {
                Success = false,
                ErrorMessage = ex.Message
            };
        }
    }

    public async Task<List<ProductImageUploadResult>> UploadProductImagesAsync(
        List<(Stream stream, string fileName)> files)
    {
        var results = new List<ProductImageUploadResult>();

        foreach (var (stream, fileName) in files)
        {
            var result = await UploadProductImageAsync(stream, fileName);
            results.Add(result);
        }

        return results;
    }

    public async Task<bool> DeleteProductImageAsync(string imagePath)
    {
        try
        {
            return await _storageService.DeleteImageAsync(imagePath);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error deleting product image: {Path}", imagePath);
            return false;
        }
    }

    public string GenerateUniqueSku()
    {
        // Generate format: PROD-YYYYMMDD-XXXX (e.g., PROD-20250514-A3B9)
        var datePart = DateTime.UtcNow.ToString("yyyyMMdd");
        var randomPart = GenerateRandomString(4);
        
        return $"PROD-{datePart}-{randomPart}";
    }

    private string GenerateRandomString(int length)
    {
        const string chars = "ABCDEFGHIJKLMNOPQRSTUVWXYZ0123456789";
        var random = new Random();
        var result = new StringBuilder(length);
        
        for (int i = 0; i < length; i++)
        {
            result.Append(chars[random.Next(chars.Length)]);
        }
        
        return result.ToString();
    }

    private string GenerateSlug(string name)
    {
        if (string.IsNullOrWhiteSpace(name))
            return string.Empty;

        return name
            .ToLower()
            .Replace(" ", "-")
            .Replace("--", "-")
            .Trim('-');
    }

    private async Task<string> GetCategoryNameAsync(string categoryId)
    {
        try
        {
            if (string.IsNullOrEmpty(categoryId))
                return string.Empty;

            var category = await _supabaseService.GetByIdAsync<CategoryModel>(
                CategoriesTable,
                categoryId);
            
            return category?.Name ?? string.Empty;
        }
        catch (Exception ex)
        {
            _logger.LogWarning(ex, "Error getting category name: {CategoryId}", categoryId);
            return string.Empty;
        }
    }

    private ProductViewModel MapToViewModel(ProductModel model)
    {
        return new ProductViewModel
        {
            Id = model.Id,
            Name = model.Name,
            Slug = model.Slug,
            Description = model.Description,
            LongDescription = model.LongDescription,
            Price = model.Price,
            OriginalPrice = model.OriginalPrice,
            Stock = model.Stock,
            Sku = model.Sku,
            CategoryId = model.CategoryId,
            Category = model.Category,
            SubCategory = model.SubCategory,
            Brand = model.Brand,
            Tags = model.Tags.ToList(),
            Sizes = model.Sizes.ToList(),
            Colors = model.Colors.ToList(),
            Images = model.Images.ToList(),
            VideoUrl = model.VideoUrl,
            Rating = model.Rating,
            ReviewCount = model.ReviewCount,
            ViewCount = model.ViewCount,
            SalesCount = model.SalesCount,
            IsActive = model.IsActive,
            IsFeatured = model.IsFeatured,
            IsOnSale = model.IsOnSale,
            Discount = model.Discount,
            CreatedAt = model.CreatedAt,
            UpdatedAt = model.UpdatedAt
        };
    }
}
