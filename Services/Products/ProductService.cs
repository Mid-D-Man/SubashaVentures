using SubashaVentures.Domain.Product;
using SubashaVentures.Services.Supabase;
using SubashaVentures.Services.Firebase;
using SubashaVentures.Services.SupaBase;
using System.Text;
using SubashaVentures.Utilities.HelperScripts;
using SupabaseProductModel = SubashaVentures.Models.Supabase.ProductModel;
using FirebaseCategoryModel = SubashaVentures.Models.Firebase.CategoryModel;
using LogLevel = SubashaVentures.Utilities.Logging.LogLevel;
namespace SubashaVentures.Services.Products;

public class ProductService : IProductService
{
    private readonly ISupabaseDatabaseService _supabaseDatabaseService;
    private readonly IFirestoreService _firestoreService;
    private readonly ISupabaseStorageService _storageService;
    private readonly ILogger<ProductService> _logger;
    
    private const string CategoriesCollection = "categories"; // Firebase

    public ProductService(
        ISupabaseDatabaseService supabaseDatabaseService,
        IFirestoreService firestoreService,
        ISupabaseStorageService storageService,
        ILogger<ProductService> logger)
    {
        _supabaseDatabaseService = supabaseDatabaseService;
        _firestoreService = firestoreService;
        _storageService = storageService;
        _logger = logger;
    }

    #region READ Operations

    public async Task<ProductViewModel?> GetProductAsync(string id)
    {
        try
        {
            var allProducts = await _supabaseDatabaseService.GetAllAsync<SupabaseProductModel>();
            var product = allProducts.FirstOrDefault(p => p.Id == id);
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
            var allProducts = await _supabaseDatabaseService.GetAllAsync<SupabaseProductModel>();
            
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
            var allProducts = await _supabaseDatabaseService.GetAllAsync<SupabaseProductModel>();
            
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
            var allProducts = await _supabaseDatabaseService.GetAllAsync<SupabaseProductModel>();
            
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
            var allProducts = await _supabaseDatabaseService.GetAllAsync<SupabaseProductModel>();
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
            var allProducts = await _supabaseDatabaseService.GetAllAsync<SupabaseProductModel>();
            
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
            var allProducts = await _supabaseDatabaseService.GetAllAsync<SupabaseProductModel>();
            
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

    #endregion

    #region CREATE Operations

    // Services/Products/ProductService.cs - CreateProductAsync method
public async Task<ProductViewModel?> CreateProductAsync(CreateProductRequest request)
{
    try
    {
        await MID_HelperFunctions.DebugMessageAsync(
            $"Attempting to insert product with ID: {request.Id ?? "NULL"}, Name: {request.Name}",
            LogLevel.Info
        );

        // CRITICAL FIX: Generate ID if not provided
        if (string.IsNullOrEmpty(request.Id))
        {
            request.Id = GenerateProductId(request.Name);
            await MID_HelperFunctions.DebugMessageAsync(
                $"Generated new product ID: {request.Id}",
                LogLevel.Info
            );
        }

        // Generate SKU if not provided
        if (string.IsNullOrEmpty(request.Sku))
        {
            request.Sku = GenerateUniqueSku();
        }

        var productModel = new SupabaseProductModel
        {
            Id = request.Id, // CRITICAL: Set the ID
            Name = request.Name,
            Slug = GenerateSlug(request.Name),
            Description = request.Description ?? string.Empty,
            LongDescription = request.LongDescription ?? string.Empty,
            Price = request.Price,
            OriginalPrice = request.OriginalPrice,
            IsOnSale = request.OriginalPrice.HasValue && request.OriginalPrice > request.Price,
            Discount = CalculateDiscount(request.OriginalPrice,request.Price),
            Images = request.ImageUrls ?? new List<string>(),
            VideoUrl = null,
            Sizes = request.Sizes ?? new List<string>(),
            Colors = request.Colors ?? new List<string>(),
            Stock = request.Stock,
            Sku = request.Sku,
            CategoryId = request.CategoryId ?? string.Empty,
            Category = await GetCategoryNameAsync(request.CategoryId ?? string.Empty),
            SubCategory = null,
            Brand = request.Brand ?? string.Empty,
            Tags = request.Tags ?? new List<string>(),
            Rating = 0,
            ReviewCount = 0,
            ViewCount = 0,
            ClickCount = 0,
            AddToCartCount = 0,
            PurchaseCount = 0,
            SalesCount = 0,
            TotalRevenue = 0,
            IsActive = true,
            IsFeatured = request.IsFeatured,
            LastViewedAt = null,
            LastPurchasedAt = null,
            CreatedAt = DateTime.UtcNow,
            CreatedBy = "system",
            UpdatedAt = null,
            UpdatedBy = null,
            IsDeleted = false,
            DeletedAt = null,
            DeletedBy = null
        };

        var result = await _supabaseDatabaseService.InsertAsync(productModel);

        if (result == null || !result.Any())
        {
            await MID_HelperFunctions.DebugMessageAsync(
                "Failed to insert product - no result returned",
                LogLevel.Error
            );
            return null;
        }

        var createdProduct = result.First();

        await MID_HelperFunctions.DebugMessageAsync(
            $"✓ Product created successfully: {createdProduct.Name} (ID: {createdProduct.Id})",
            LogLevel.Info
        );

        return MapToViewModel(createdProduct);
    }
    catch (Exception ex)
    {
        await MID_HelperFunctions.LogExceptionAsync(ex, $"Creating product: {request.Name}");
        throw;
    }
}

// Add this helper method to generate readable product IDs
private string GenerateProductId(string productName)
{
    // Clean the product name
    var cleanName = new string(productName
        .ToLower()
        .Take(20) // Max 20 chars from name
        .Where(c => char.IsLetterOrDigit(c) || c == '-')
        .ToArray())
        .Replace(' ', '-');
    
    // Add timestamp for uniqueness
    var timestamp = DateTime.UtcNow.ToString("yyyyMMddHHmmss");
    
    // Add random suffix for extra uniqueness
    var random = new Random().Next(1000, 9999);
    
    return $"{cleanName}-{timestamp}-{random}";
}

    #endregion

    #region UPDATE Operations

    public async Task<bool> UpdateProductAsync(string id, UpdateProductRequest request)
    {
        try
        {
            // Get existing product
            var allProducts = await _supabaseDatabaseService.GetAllAsync<SupabaseProductModel>();
            var product = allProducts.FirstOrDefault(p => p.Id == id);
            
            if (product == null)
            {
                _logger.LogWarning("Product not found for update: {Id}", id);
                return false;
            }

            // Get category name if category changed
            var categoryName = !string.IsNullOrEmpty(request.CategoryId) 
                ? await GetCategoryNameAsync(request.CategoryId) 
                : product.Category;

            // Calculate new discount if price changed
            var newOriginalPrice = request.OriginalPrice ?? product.OriginalPrice;
            var newPrice = request.Price ?? product.Price;
            var newDiscount = CalculateDiscount(newOriginalPrice, newPrice);
            var isOnSale = newOriginalPrice.HasValue && newOriginalPrice > newPrice;

            // Update product properties
            product.Name = request.Name ?? product.Name;
            product.Slug = !string.IsNullOrEmpty(request.Name) ? GenerateSlug(request.Name) : product.Slug;
            product.Description = request.Description ?? product.Description;
            product.LongDescription = request.LongDescription ?? product.LongDescription;
            product.Price = newPrice;
            product.OriginalPrice = newOriginalPrice;
            product.Stock = request.Stock ?? product.Stock;
            product.CategoryId = request.CategoryId ?? product.CategoryId;
            product.Category = categoryName;
            product.Brand = request.Brand ?? product.Brand;
            product.Tags = request.Tags ?? product.Tags;
            product.Sizes = request.Sizes ?? product.Sizes;
            product.Colors = request.Colors ?? product.Colors;
            product.IsFeatured = request.IsFeatured ?? product.IsFeatured;
            product.IsActive = request.IsActive ?? product.IsActive;
            product.UpdatedAt = DateTime.UtcNow;
            product.UpdatedBy = "system";
            product.IsOnSale = isOnSale;
            product.Discount = newDiscount;

            var updatedProducts = await _supabaseDatabaseService.UpdateAsync(product);
            
            if (updatedProducts != null && updatedProducts.Any())
            {
                _logger.LogInformation("Product updated: {Id}", id);
                return true;
            }
            
            return false;
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
            var allProducts = await _supabaseDatabaseService.GetAllAsync<SupabaseProductModel>();
            var product = allProducts.FirstOrDefault(p => p.Id == id);
            
            if (product == null) return false;

            product.Images = imageUrls;
            product.UpdatedAt = DateTime.UtcNow;
            product.UpdatedBy = "system";

            var updatedProducts = await _supabaseDatabaseService.UpdateAsync(product);
            return updatedProducts != null && updatedProducts.Any();
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
            var allProducts = await _supabaseDatabaseService.GetAllAsync<SupabaseProductModel>();
            var product = allProducts.FirstOrDefault(p => p.Id == id);
            
            if (product == null) return false;

            product.Stock = quantity;
            product.UpdatedAt = DateTime.UtcNow;
            product.UpdatedBy = "system";

            var updatedProducts = await _supabaseDatabaseService.UpdateAsync(product);
            return updatedProducts != null && updatedProducts.Any();
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error updating product stock: {Id}", id);
            return false;
        }
    }

    #endregion

    #region DELETE Operations

    public async Task<bool> DeleteProductAsync(string id)
    {
        try
        {
            var allProducts = await _supabaseDatabaseService.GetAllAsync<SupabaseProductModel>();
            var product = allProducts.FirstOrDefault(p => p.Id == id);
            
            if (product == null) return false;

            // Soft delete
            product.IsDeleted = true;
            product.DeletedAt = DateTime.UtcNow;
            product.DeletedBy = "system";

            var result = await _supabaseDatabaseService.UpdateAsync(product);
            
            if (result != null && result.Any())
            {
                _logger.LogInformation("Product soft-deleted: {Id}", id);
                return true;
            }
            
            return false;
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

    #endregion

    #region IMAGE MANAGEMENT

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

    #endregion

    #region UTILITY Methods

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

    private int CalculateDiscount(decimal? originalPrice, decimal price)
    {
        if (!originalPrice.HasValue || originalPrice.Value <= price)
            return 0;

        return (int)Math.Round(((originalPrice.Value - price) / originalPrice.Value) * 100);
    }

    private void ValidateCreateRequest(CreateProductRequest request)
    {
        if (string.IsNullOrEmpty(request.Name))
            throw new ArgumentException("Product name is required");

        if (request.Price <= 0)
            throw new ArgumentException("Price must be greater than 0");

        if (string.IsNullOrEmpty(request.Sku))
            throw new ArgumentException("SKU is required");

        if (string.IsNullOrEmpty(request.CategoryId))
            throw new ArgumentException("Category is required");
    }

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

    #endregion
}