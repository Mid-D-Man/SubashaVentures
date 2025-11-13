using SubashaVentures.Domain.Product;
using SubashaVentures.Services.Supabase;
using SubashaVentures.Services.Firebase;
using System.Text;
using SupabaseProductModel = SubashaVentures.Models.Supabase.ProductModel;
using FirebaseCategoryModel = SubashaVentures.Models.Firebase.CategoryModel;


namespace SubashaVentures.Services.Products;

public class ProductService : IProductService
{
    private readonly ISupabaseService _supabaseService;
    private readonly IFirestoreService _firestoreService;
    private readonly ISupabaseStorageService _storageService;
    private readonly ILogger<ProductService> _logger;
    
    private const string ProductsTable = "products";
    private const string CategoriesCollection = "categories"; // Firebase

    public ProductService(
        ISupabaseService supabaseService,
        IFirestoreService firestoreService,
        ISupabaseStorageService storageService,
        ILogger<ProductService> logger)
    {
        _supabaseService = supabaseService;
        _firestoreService = firestoreService;
        _storageService = storageService;
        _logger = logger;
    }

    public async Task<ProductViewModel?> GetProductAsync(string id)
    {
        try
        {
            var product = await _supabaseService.GetByIdAsync<SupabaseProductModel>(ProductsTable, id);
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
            var allProducts = await _supabaseService.GetAllAsync<SupabaseProductModel>(ProductsTable);
            
            var filtered = allProducts
                .Where(p => p.IsActive && !p.IsDeleted)
                .OrderByDescending(p => p.CreatedAt)
                .Skip(skip)
                .Take(take)
                .ToList();

            return filtered.Select(MapToViewModel).ToList();
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
            var allProducts = await _supabaseService.GetAllAsync<SupabaseProductModel>(ProductsTable);
            
            var searchLower = query.ToLower();
            var results = allProducts
                .Where(p => p.IsActive && !p.IsDeleted &&
                           (p.Name.ToLower().Contains(searchLower) ||
                            p.Description.ToLower().Contains(searchLower) ||
                            p.Sku.ToLower().Contains(searchLower)))
                .OrderByDescending(p => p.CreatedAt)
                .ToList();

            return results.Select(MapToViewModel).ToList();
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
            var allProducts = await _supabaseService.GetAllAsync<SupabaseProductModel>(ProductsTable);
            
            var filtered = allProducts
                .Where(p => p.IsActive && !p.IsDeleted && p.CategoryId == categoryId)
                .OrderByDescending(p => p.CreatedAt)
                .ToList();

            return filtered.Select(MapToViewModel).ToList();
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
            var allProducts = await _supabaseService.GetAllAsync<SupabaseProductModel>(ProductsTable);
            return allProducts.Count(p => p.IsActive && !p.IsDeleted);
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
            var allProducts = await _supabaseService.GetAllAsync<SupabaseProductModel>(ProductsTable);
            
            var trending = allProducts
                .Where(p => p.IsActive && !p.IsDeleted)
                .OrderByDescending(p => p.ViewCount)
                .ThenByDescending(p => p.PurchaseCount)
                .Take(count)
                .ToList();

            return trending.Select(MapToViewModel).ToList();
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
            var allProducts = await _supabaseService.GetAllAsync<SupabaseProductModel>(ProductsTable);
            
            var featured = allProducts
                .Where(p => p.IsActive && !p.IsDeleted && p.IsFeatured)
                .OrderByDescending(p => p.CreatedAt)
                .Take(count)
                .ToList();

            return featured.Select(MapToViewModel).ToList();
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

            var productModel = new SupabaseProductModel
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
                CreatedBy = "system",
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
            throw;
        }
    }

    public async Task<bool> UpdateProductAsync(string id, UpdateProductRequest request)
    {
        try
        {
            var product = await _supabaseService.GetByIdAsync<SupabaseProductModel>(ProductsTable, id);
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
                UpdatedBy = "system",
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
            var product = await _supabaseService.GetByIdAsync<SupabaseProductModel>(ProductsTable, id);
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
            var product = await _supabaseService.GetByIdAsync<SupabaseProductModel>(ProductsTable, id);
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
            var product = await _supabaseService.GetByIdAsync<SupabaseProductModel>(ProductsTable, id);
            if (product == null) return false;

            var deleted = product with
            {
                IsDeleted = true,
                DeletedAt = DateTime.UtcNow,
                DeletedBy = "system"
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

    // Get category name from Firebase
    private async Task<string> GetCategoryNameAsync(string categoryId)
    {
        try
        {
            if (string.IsNullOrEmpty(categoryId))
                return string.Empty;

            var category = await _firestoreService.GetDocumentAsync<FirebaseCategoryModel>(
                CategoriesCollection,
                categoryId);
            
            return category?.Name ?? string.Empty;
        }
        catch (Exception ex)
        {
            _logger.LogWarning(ex, "Error getting category name: {CategoryId}", categoryId);
            return string.Empty;
        }
    }

    private ProductViewModel MapToViewModel(SupabaseProductModel model)
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
